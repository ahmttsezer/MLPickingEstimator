using ClosedXML.Excel;
using MLPickingEstimator.Models;

namespace MLPickingEstimator.Services
{
    public class PersonnelDispatchService
    {
        private readonly MLPickingService _ml;
        private readonly LocationResolver _loc;
        public PersonnelDispatchService(MLPickingService ml, LocationResolver loc)
        {
            _ml = ml;
            _loc = loc;
        }

        private static double ComputeUrgency(PickingTask task, DateTime now, AssignmentCriteria c)
        {
            // Zaman bileşeni: tolerans sonrası kalan sürenin ölçeklenmiş tersine normalize edilmesi
            double timeComponent = 0;
            if (task.EndTime.HasValue)
            {
                var remaining = (task.EndTime.Value - now).TotalMinutes;
                var adjusted = remaining - c.TimeWindowToleranceMinutes;
                var norm = 1 - (adjusted / c.TimeWindowScaleMinutes);
                timeComponent = Math.Clamp(norm, 0, 1.5);
            }
            else if (task.StartTime.HasValue)
            {
                var elapsed = (now - task.StartTime.Value).TotalMinutes;
                timeComponent = Math.Clamp(elapsed / c.TimeWindowScaleMinutes, 0, 1.0);
            }

            // Öncelik bileşeni: 0–3 arası PriorityLevel -> 0–1 arası normalize
            double priorityComponent = 0;
            if (task.PriorityLevel.HasValue)
            {
                priorityComponent = Math.Clamp(task.PriorityLevel.Value / 3.0, 0, 1.0);
            }
            else
            {
                string[] vipHints = new[] { "VIP", "PRIORITY", "ÖNCELİK", "ONCELIK", "EXPRESS" };
                bool hasHint(string? s) => !string.IsNullOrWhiteSpace(s) && vipHints.Any(h => s.Contains(h, StringComparison.OrdinalIgnoreCase));
                var hintScore = 0.0;
                if (hasHint(task.Customer)) hintScore += 1.0;
                if (hasHint(task.TaskStatus)) hintScore += 0.5;
                if (hasHint(task.TransactionType)) hintScore += 0.5;
                priorityComponent = Math.Clamp(hintScore, 0, 1.0);
            }

            // Acil bayrağı katkısı
            bool urgent = (string.Equals(task.TransactionType, "Acil", StringComparison.OrdinalIgnoreCase) || string.Equals(task.TaskStatus, "Yüksek", StringComparison.OrdinalIgnoreCase));
            double urgentBoost = urgent && c.PrioritizeUrgent ? c.UrgentBaseBoost : 0;

            return timeComponent + priorityComponent + urgentBoost;
        }

        private (double x, double y) ResolveLocation(string? code)
        {
            var p = _loc.Resolve(code);
            return (p.X, p.Y);
        }

        private static double Distance((double x,double y) a, (double x,double y) b)
        {
            var dx = a.x - b.x; var dy = a.y - b.y; return Math.Sqrt(dx*dx + dy*dy);
        }

        private ProductPickingData MakeFeatures(PickingTask t, PersonnelInfo p, double dist)
        {
            return new ProductPickingData
            {
                ItemCount = (float)(t.TodoQuantity ?? t.Quantity ?? 1),
                Weight = 1f,
                Volume = 1f,
                Distance = (float)dist,
                PickerExperience = p.PickerExperience,
                StockDensity = 1f
            };
        }

        public List<PickingTask> ParseExcel(Stream excelStream)
        {
            using var wb = new XLWorkbook(excelStream);
            var ws = wb.Worksheets.First();
            var tasks = new List<PickingTask>();
            var header = ws.Row(1).Cells().Select((c,i) => (i, c.GetString().Trim())).ToDictionary(x => x.i+1, x => x.Item2);
            int lastRow = ws.LastRowUsed().RowNumber();
            for (int r = 2; r <= lastRow; r++)
            {
                var t = new PickingTask();
                foreach (var cell in ws.Row(r).Cells())
                {
                    var col = cell.Address.ColumnNumber;
                    var name = header.ContainsKey(col) ? header[col] : string.Empty;
                    var val = cell.GetString();
                    switch (name)
                    {
                        case "Görev No": if (long.TryParse(val, out var id)) t.TaskId = id; break;
                        case "Müşteri": t.Customer = val; break;
                        case "Marka": t.Brand = val; break;
                        case "İş Emri No": t.WorkOrderNo = val; break;
                        case "Depo Çıkış Siparişi": t.WarehouseOrder = val; break;
                        case "Hareket Tipi": t.TransactionType = val; break;
                        case "Görev Durumu": t.TaskStatus = val; break;
                        case "Malzeme Kodu": t.MaterialCode = val; break;
                        case "Malzeme": t.Material = val; break;
                        case "Barkod": t.Barcode = val; break;
                        case "Dağılım No": t.DistributionNo = val; break;
                        case "Zone": t.Zone = val; break;
                        case "Koridor": t.Corridor = val; break;
                        case "Mega İş Listedi ID": t.MegaListId = val; break;
                        case "Split Tamamlandı Mı?": t.SplitCompleted = val.Equals("1") || val.Equals("Evet", StringComparison.OrdinalIgnoreCase); break;
                        case "Miktar": if (double.TryParse(val, out var q)) t.Quantity = q; break;
                        case "Yapılacak Miktar": if (double.TryParse(val, out var tq)) t.TodoQuantity = tq; break;
                        case "Tamamlanan Miktar": if (double.TryParse(val, out var cq)) t.CompletedQuantity = cq; break;
                        case "PK İş İstasyonu": t.FirstStation = val; break;
                        case "İlk Lokasyonu": t.FirstLocation = val; break;
                        case "Son Lokasyonu": t.LastLocation = val; break;
                        case "İş İstasyonu": t.WorkStation = val; break;
                        case "Rack": t.Rack = val; break;
                        case "Görev Başlama Zamanı": if (DateTime.TryParse(val, out var st)) t.StartTime = st; break;
                        case "Görev Bitiş Zamanı": if (DateTime.TryParse(val, out var et)) t.EndTime = et; break;
                        case "İlk Paleti": t.FirstPallet = val; break;
                        case "Son Paleti": t.LastPallet = val; break;
                        case "İlk Kolisi": t.FirstBox = val; break;
                        case "Orjinal Sipariş No": t.OriginalOrderNo = val; break;
                        case "Son Kolisi": t.LastBox = val; break;
                        case "İlk Paket Tipi": t.FirstPackageType = val; break;
                        case "Son Paket Tipi": t.LastPackageType = val; break;
                    }
                }
                if ((t.TaskId != 0) || !string.IsNullOrWhiteSpace(t.FirstLocation))
                    tasks.Add(t);
            }
            return tasks;
        }

        public List<PickingTask> ParseCsv(Stream csvStream)
        {
            using var reader = new StreamReader(csvStream);
            var tasks = new List<PickingTask>();
            string? headerLine = reader.ReadLine();
            if (string.IsNullOrWhiteSpace(headerLine)) return tasks;
            var headers = headerLine.Split(',').Select(h => h.Trim()).ToList();
            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                var cols = line.Split(',');
                var t = new PickingTask();
                for (int i = 0; i < cols.Length && i < headers.Count; i++)
                {
                    var name = headers[i];
                    var val = cols[i].Trim();
                    switch (name)
                    {
                        case "Görev No": if (long.TryParse(val, out var id)) t.TaskId = id; break;
                        case "Müşteri": t.Customer = val; break;
                        case "Marka": t.Brand = val; break;
                        case "İş Emri No": t.WorkOrderNo = val; break;
                        case "Depo Çıkış Siparişi": t.WarehouseOrder = val; break;
                        case "Hareket Tipi": t.TransactionType = val; break;
                        case "Görev Durumu": t.TaskStatus = val; break;
                        case "Malzeme Kodu": t.MaterialCode = val; break;
                        case "Malzeme": t.Material = val; break;
                        case "Barkod": t.Barcode = val; break;
                        case "Dağılım No": t.DistributionNo = val; break;
                        case "Zone": t.Zone = val; break;
                        case "Koridor": t.Corridor = val; break;
                        case "Mega İş Listedi ID": t.MegaListId = val; break;
                        case "Split Tamamlandı Mı?": t.SplitCompleted = val.Equals("1") || val.Equals("Evet", StringComparison.OrdinalIgnoreCase); break;
                        case "Miktar": if (double.TryParse(val, out var q)) t.Quantity = q; break;
                        case "Yapılacak Miktar": if (double.TryParse(val, out var tq)) t.TodoQuantity = tq; break;
                        case "Tamamlanan Miktar": if (double.TryParse(val, out var cq)) t.CompletedQuantity = cq; break;
                        case "PK İş İstasyonu": t.FirstStation = val; break;
                        case "İlk Lokasyonu": t.FirstLocation = val; break;
                        case "Son Lokasyonu": t.LastLocation = val; break;
                        case "İş İstasyonu": t.WorkStation = val; break;
                        case "Rack": t.Rack = val; break;
                        case "Görev Başlama Zamanı": if (DateTime.TryParse(val, out var st)) t.StartTime = st; break;
                        case "Görev Bitiş Zamanı": if (DateTime.TryParse(val, out var et)) t.EndTime = et; break;
                        case "İlk Paleti": t.FirstPallet = val; break;
                        case "Son Paleti": t.LastPallet = val; break;
                        case "İlk Kolisi": t.FirstBox = val; break;
                        case "Orjinal Sipariş No": t.OriginalOrderNo = val; break;
                        case "Son Kolisi": t.LastBox = val; break;
                        case "İlk Paket Tipi": t.FirstPackageType = val; break;
                        case "Son Paket Tipi": t.LastPackageType = val; break;
                    }
                }
                if ((t.TaskId != 0) || !string.IsNullOrWhiteSpace(t.FirstLocation))
                    tasks.Add(t);
            }
            return tasks;
        }

        private string ExtractCorridor(string? code) => _loc.ExtractCorridor(code);

        public AssignmentResponse AssignTasks(List<PersonnelInfo> personnel, List<PickingTask> tasks, AssignmentCriteria? criteria = null)
        {
            var c = criteria ?? new AssignmentCriteria();
            // Geliştirilmiş greedy: ML süresi + mesafe + tecrübe/hız bonusları + yük dengeleme + koridor/zone yakınlığı
            var summaries = personnel.ToDictionary(p => p.Id, p => new AssignmentSummary { PersonnelId = p.Id, PersonnelName = p.Name });
            var lastPositions = personnel.ToDictionary(p => p.Id, p => ResolveLocation(p.LastLocationCode));
            var lastCorridors = personnel.ToDictionary(p => p.Id, p => ExtractCorridor(p.LastLocationCode));

            foreach (var task in tasks)
            {
                var start = ResolveLocation(task.FirstLocation);
                var taskCorridor = ExtractCorridor(task.FirstLocation);
                string? bestPid = null; double bestTime = double.MaxValue; double bestDist = 0; PersonnelInfo? bestPers = null; double bestTimeWindowBonus = 0; double bestCustomerPriority = 0;
                foreach (var p in personnel)
                {
                    // Max görev sınırı
                    if (c.MaxTasksPerPerson > 0 && summaries[p.Id].Assignments.Count >= c.MaxTasksPerPerson) continue;
                    var last = lastPositions[p.Id];
                    var dist = Distance(last, start);
                    var features = MakeFeatures(task, p, dist);
                    var pred = _ml.Predict(features);
                    var time = (double)pred.PredictedTime;
                    // hız katsayısı varsa uygula
                    if (p.SpeedFactor.HasValue && p.SpeedFactor.Value > 0) time /= p.SpeedFactor.Value;

                    // Ek kriterler ve ağırlıklar
                    var zoneMatch = (!string.IsNullOrWhiteSpace(p.Zone) && !string.IsNullOrWhiteSpace(task.Zone) && p.Zone == task.Zone);
                    var corridorMatch = (c.ClusterByCorridor && !string.IsNullOrWhiteSpace(taskCorridor) && lastCorridors[p.Id] == taskCorridor);
                    var urgent = c.PrioritizeUrgent && (string.Equals(task.TransactionType, "Acil", StringComparison.OrdinalIgnoreCase) || string.Equals(task.TaskStatus, "Yüksek", StringComparison.OrdinalIgnoreCase));

                    // Zaman penceresi bonusu: penceredeki ilerlemeye göre skoru azalt
                    double timeWindowBonus = 0;
                    if (c.TimeWindowWeight > 0)
                    {
                        var now = DateTime.Now;
                        if (task.StartTime.HasValue && task.EndTime.HasValue && task.EndTime > task.StartTime)
                        {
                            var total = (task.EndTime.Value - task.StartTime.Value).TotalMinutes;
                            if (total > 1)
                            {
                                var elapsed = (now - task.StartTime.Value).TotalMinutes;
                                var progress = Math.Clamp(elapsed / total, 0, 1.2);
                                timeWindowBonus = c.TimeWindowWeight * progress;
                            }
                        }
                        else if (task.EndTime.HasValue)
                        {
                            var remaining = (task.EndTime.Value - now).TotalMinutes;
                            var urgency = remaining <= 0 ? 1.2 : Math.Clamp(1 - (remaining / 60.0), 0, 1);
                            timeWindowBonus = c.TimeWindowWeight * urgency;
                        }
                    }

                    // Müşteri önceliği: VIP/PRIORITY/EXPRESS ipuçlarına göre skoru azalt
                    double customerPriority = 0;
                    if (c.CustomerPriorityWeight > 0)
                    {
                        string[] vipHints = new[] { "VIP", "PRIORITY", "ÖNCELİK", "ONCELIK", "EXPRESS" };
                        bool hasHint(string? s) => !string.IsNullOrWhiteSpace(s) && vipHints.Any(h => s.Contains(h, StringComparison.OrdinalIgnoreCase));
                        if (hasHint(task.Customer)) customerPriority += 1.0;
                        if (hasHint(task.TaskStatus)) customerPriority += 0.5;
                        if (hasHint(task.TransactionType)) customerPriority += 0.5;
                    }

                    double score = 0;
                    // Ana maliyet: ML zamanı ve mesafe
                    score += c.MlTimeWeight * time;
                    score += c.DistanceWeight * dist;
                    // Bonuslar: tecrübe, hız, zone/corridor uyumu
                    score -= c.ExperienceWeight * p.PickerExperience;
                    if (p.SpeedFactor.HasValue)
                        score -= c.SpeedWeight * Math.Max(0, p.SpeedFactor.Value - 1); // 1 üzeri hız bonus
                    if (zoneMatch) score -= c.ZoneMatchBonus;
                    if (corridorMatch) score -= c.CorridorClusterBonus;
                    // Aciliyet: birleşik (zaman pencere + müşteri öncelik + acil bayrak)
                    if (c.UrgencyWeight > 0)
                    {
                        var urgencyScore = ComputeUrgency(task, DateTime.Now, c);
                        score -= c.UrgencyWeight * urgencyScore;
                    }
                    else
                    {
                        // Geriye dönük uyumluluk: ayrı zaman penceresi ve müşteri önceliği ağırlıkları
                        score -= timeWindowBonus;
                        score -= c.CustomerPriorityWeight * customerPriority;
                        if (urgent) score -= 0.5; // sabit bonus, istenirse kritere eklenebilir
                    }
                    // Yük dengeleme cezası: toplam süreyi büyüten personele ek maliyet
                    score += c.BalanceLoadWeight * summaries[p.Id].TotalPredictedTimeSeconds;
                    // Not: acil sabit bonusu birleşik aciliyet modunda ComputeUrgency tarafından kapsanır

                    if (score < bestTime) { bestTime = score; bestPid = p.Id; bestDist = dist; bestPers = p; bestTimeWindowBonus = timeWindowBonus; bestCustomerPriority = customerPriority; }
                }
                if (bestPid != null && bestPers != null)
                {
                    lastPositions[bestPid] = start; // konum güncelle
                    lastCorridors[bestPid] = taskCorridor;
                    summaries[bestPid].Assignments.Add(new PickingAssignment
                    {
                        TaskId = task.TaskId,
                        PersonnelId = bestPid,
                        PersonnelName = bestPers.Name,
                        StartLocationCode = task.FirstLocation ?? string.Empty,
                        DistanceFromLast = bestDist,
                        PredictedTimeSeconds = bestTime,
                        Criteria = new Dictionary<string,double>
                        {
                            { "Mesafe", bestDist },
                            { "Tecrübe", bestPers.PickerExperience },
                            { "Hız", bestPers.SpeedFactor ?? 1 },
                            { "Zone Uyum", (!string.IsNullOrWhiteSpace(bestPers.Zone) && !string.IsNullOrWhiteSpace(task.Zone) && bestPers.Zone == task.Zone) ? 1 : 0 },
                            { "Koridor Uyum", (lastCorridors[bestPid] == taskCorridor && !string.IsNullOrWhiteSpace(taskCorridor)) ? 1 : 0 },
                            { "Zaman Penceresi", c.UrgencyWeight > 0 ? ComputeUrgency(task, DateTime.Now, c) : bestTimeWindowBonus },
                            { "Müşteri Önceliği", c.UrgencyWeight > 0 ? (task.PriorityLevel.HasValue ? Math.Clamp(task.PriorityLevel.Value / 3.0, 0, 1.0) : bestCustomerPriority) : bestCustomerPriority },
                            { "Aciliyet", c.UrgencyWeight > 0 ? ComputeUrgency(task, DateTime.Now, c) : (bestTimeWindowBonus + bestCustomerPriority + (c.PrioritizeUrgent && (string.Equals(task.TransactionType, "Acil", StringComparison.OrdinalIgnoreCase) || string.Equals(task.TaskStatus, "Yüksek", StringComparison.OrdinalIgnoreCase)) ? c.UrgentBaseBoost : 0)) },
                            { "Yapılacak Miktar", task.TodoQuantity ?? task.Quantity ?? 1 }
                        }
                    });
                    summaries[bestPid].TotalDistance += bestDist;
                    summaries[bestPid].TotalPredictedTimeSeconds += bestTime;
                }
            }
            return new AssignmentResponse { Results = summaries.Values.ToList() };
        }
    }
}