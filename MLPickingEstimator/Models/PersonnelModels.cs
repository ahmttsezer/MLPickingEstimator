using System.Text.Json.Serialization;

namespace MLPickingEstimator.Models
{
    public class PersonnelInfo
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public string LastLocationCode { get; set; } = string.Empty; // Örn: PX-MZ-D08-171F
        public int PickerExperience { get; set; } = 1;              // 1-5 arası tecrübe
        public double? SpeedFactor { get; set; }                     // İsteğe bağlı hız katsayısı
        public string? Zone { get; set; }
    }

    public class PickingTask
    {
        public long TaskId { get; set; } = 0;                        // Görev No
        public string? Customer { get; set; }
        public int? PriorityLevel { get; set; }                      // 0–3 arası müşteri öncelik seviyesi
        public string? Brand { get; set; }
        public string? WorkOrderNo { get; set; }                    // İş Emri No
        public string? WarehouseOrder { get; set; }                 // Depo Çıkış Siparişi
        public string? TransactionType { get; set; }                // Hareket Tipi
        public string? TaskStatus { get; set; }                     // Görev Durumu
        public string? MaterialCode { get; set; }                   // Malzeme Kodu
        public string? Material { get; set; }                       // Malzeme
        public string? Barcode { get; set; }
        public string? DistributionNo { get; set; }                 // Dağılım No
        public string? Zone { get; set; }
        public string? Corridor { get; set; }
        public string? MegaListId { get; set; }
        public bool? SplitCompleted { get; set; }
        public string? Imei { get; set; }
        public string? SerialNo { get; set; }
        public string? LotNo { get; set; }
        public string? Skt { get; set; }
        public double? Quantity { get; set; }                       // Miktar
        public double? TodoQuantity { get; set; }                   // Yapılacak Miktar
        public double? CompletedQuantity { get; set; }              // Tamamlanan Miktar
        public string? FirstStation { get; set; }                   // PK İş İstasyonu
        public string? FirstLocation { get; set; }                  // İlk Lokasyonu (start)
        public string? LastLocation { get; set; }                   // Son Lokasyonu (end)
        public string? WorkStation { get; set; }                    // İş İstasyonu
        public string? Rack { get; set; }
        public DateTime? StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public string? FirstPallet { get; set; }
        public string? LastPallet { get; set; }
        public string? FirstBox { get; set; }
        public string? OriginalOrderNo { get; set; }
        public string? LastBox { get; set; }
        public string? FirstPackageType { get; set; }
        public string? LastPackageType { get; set; }
    }

    public class PickingAssignment
    {
        public long TaskId { get; set; } = 0;
        public string PersonnelId { get; set; } = string.Empty;
        public string PersonnelName { get; set; } = string.Empty;
        public string StartLocationCode { get; set; } = string.Empty;
        public double DistanceFromLast { get; set; }
        public double PredictedTimeSeconds { get; set; }
        public Dictionary<string,double> Criteria { get; set; } = new();
    }

    public class AssignmentSummary
    {
        public string PersonnelId { get; set; } = string.Empty;
        public string PersonnelName { get; set; } = string.Empty;
        public List<PickingAssignment> Assignments { get; set; } = new();
        public double TotalDistance { get; set; }
        public double TotalPredictedTimeSeconds { get; set; }
    }

    public class AssignmentResponse
    {
        public List<AssignmentSummary> Results { get; set; } = new();
    }

    // Atama kriterleri ve ağırlıkları
    public class AssignmentCriteria
    {
        public double MlTimeWeight { get; set; } = 1.0;           // ML tahmin süresi ağırlığı
        public double DistanceWeight { get; set; } = 0.3;          // Mesafe cezası
        public double TimeWindowWeight { get; set; } = 0.0;        // Zaman penceresi öncelik ağırlığı
        public double CustomerPriorityWeight { get; set; } = 0.0;  // Müşteri önceliği ağırlığı
        public double UrgencyWeight { get; set; } = 0.0;            // Birleşik aciliyet ağırlığı (zaman+p.öncelik+acil bayrak)
        public double TimeWindowToleranceMinutes { get; set; } = 10; // Zaman toleransı (erken bitişe düşük etki)
        public double TimeWindowScaleMinutes { get; set; } = 60;     // Zaman ölçeklemesi (60 dk içinde normalize)
        public double UrgentBaseBoost { get; set; } = 0.5;           // Acil durum temel katkısı
        public double ExperienceWeight { get; set; } = 0.1;        // Tecrübe bonusu
        public double SpeedWeight { get; set; } = 0.2;             // Hız katsayısı bonusu
        public double BalanceLoadWeight { get; set; } = 0.05;      // Yük dengeleme cezası (toplam süre)
        public double ZoneMatchBonus { get; set; } = 0.5;          // Zone eşleşme bonusu
        public double CorridorClusterBonus { get; set; } = 0.3;    // Aynı koridora yakın görev bonusu
        public int MaxTasksPerPerson { get; set; } = 0;            // 0: sınırsız
        public bool ClusterByCorridor { get; set; } = true;        // Koridor kümelenmesi
        public bool PrioritizeUrgent { get; set; } = true;         // Önceliklendirme (TransactionType/TaskStatus)
    }
}