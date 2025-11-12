using System;
using System.Collections.Generic;
using System.Linq;
using MLPickingEstimator.Models;

namespace MLPickingEstimator.Services
{
    public class DispatchOptimizationService
    {
        private readonly RouteSimulationService _route;
        private readonly LocationResolver _resolver;

        public DispatchOptimizationService(RouteSimulationService route, LocationResolver resolver)
        {
            _route = route;
            _resolver = resolver;
        }

        // Basit açgözlü optimizasyon: her görevi, kapasite ve kısıtlara uyan ve maliyeti en düşük araca ata
        public DispatchResult Optimize(DispatchRequest request)
        {
            var result = new DispatchResult();
            if (request.Vehicles == null || request.Vehicles.Count == 0)
                return result;

            var vehicles = request.Vehicles.Select(v => new Vehicle
            {
                Id = v.Id,
                CurrentLocation = !string.IsNullOrWhiteSpace(v.CurrentLocationCode) ? _resolver.Resolve(v.CurrentLocationCode) : v.CurrentLocation,
                CapacityKg = v.CapacityKg,
                CapacityCases = v.CapacityCases,
                Zone = v.Zone
            }).ToList();

            var assignments = vehicles.ToDictionary(v => v.Id, v => new DispatchAssignmentResult
            {
                VehicleId = v.Id,
                OrderedLocations = new List<LocationPoint>(),
                TotalDistance = 0,
                EstimatedTimeSeconds = 0,
                AssignedJobCount = 0
            });

            // Araç durumları
            var lastLocation = vehicles.ToDictionary(v => v.Id, v => v.CurrentLocation);
            var remainingKg = vehicles.ToDictionary(v => v.Id, v => v.CapacityKg ?? double.MaxValue);
            var remainingCases = vehicles.ToDictionary(v => v.Id, v => v.CapacityCases ?? int.MaxValue);
            var lastCorridor = vehicles.ToDictionary(v => v.Id, v => (string?)null);

            // Parametreler
            var speed = Math.Max(0.2, request.AverageSpeedMps ?? 1.2); // m/s
            var handleSec = Math.Max(0, request.HandlingSecondsPerTask ?? 15);

            // İşleri öncelik ve zaman penceresine göre sırala
            var jobs = (request.Jobs ?? new List<JobTask>()).OrderByDescending(j => j.Priority)
                .ThenBy(j => j.EarliestTime ?? 0)
                .ThenBy(j => j.LatestTime ?? double.MaxValue)
                .ThenBy(j => (j.Quantity ?? 0) + (j.WeightKg ?? 0))
                .ToList();

            foreach (var job in jobs)
            {
                double bestCost = double.PositiveInfinity;
                string? bestVehicle = null;

                foreach (var v in vehicles)
                {
                    // Bölge uyumu (varsa)
                    if (!string.IsNullOrWhiteSpace(job.Zone) && !string.IsNullOrWhiteSpace(v.Zone))
                    {
                        if (!string.Equals(job.Zone, v.Zone, StringComparison.OrdinalIgnoreCase))
                            continue;
                    }

                    // Kapasite kısıtları
                    if (job.WeightKg.HasValue && remainingKg[v.Id] < job.WeightKg.Value) continue;
                    if (job.Quantity.HasValue && remainingCases[v.Id] < job.Quantity.Value) continue;

                    // Mesafe ve koridor maliyeti
                    var from = lastLocation[v.Id];
                    var to = job.Location;
                    if (!string.IsNullOrWhiteSpace(job.RackId))
                    {
                        to = _resolver.Resolve(job.RackId);
                        job.Corridor ??= _resolver.ExtractCorridor(job.RackId);
                    }
                    var d = _route.Distance(from, to);

                    double corridorPenalty = 0;
                    if (!string.IsNullOrWhiteSpace(lastCorridor[v.Id]) && !string.IsNullOrWhiteSpace(job.Corridor))
                    {
                        if (!string.Equals(lastCorridor[v.Id], job.Corridor, StringComparison.OrdinalIgnoreCase))
                            corridorPenalty = 5; // koridor değişimi cezası (metre cinsinden eşdeğer)
                    }

                    var timeCost = d / speed + handleSec; // saniye
                    var cost = d + corridorPenalty + timeCost * 0.1; // karma maliyet

                    // Zaman penceresi kaba kontrol (mevcut modelde global saat izlemiyoruz)
                    // Burada basit bir uygunluk kontrolü bırakıyoruz.
                    if (job.LatestTime.HasValue)
                    {
                        // Zaman penceresi çok dar ise ve iş uzun sürüyorsa maliyeti arttır
                        var remainingWindow = job.LatestTime.Value - (job.EarliestTime ?? 0);
                        if (remainingWindow > 0)
                        {
                            var predictedMinutes = timeCost / 60.0;
                            cost += Math.Max(0, predictedMinutes - remainingWindow) * 2.0;
                        }
                    }

                    if (cost < bestCost)
                    {
                        bestCost = cost;
                        bestVehicle = v.Id;
                    }
                }

                if (bestVehicle != null)
                {
                    // Ata ve durumları güncelle
                    var selected = vehicles.First(v => v.Id == bestVehicle);
                    var target = job.Location;
                    if (!string.IsNullOrWhiteSpace(job.RackId))
                    {
                        target = _resolver.Resolve(job.RackId);
                    }
                    var d = _route.Distance(lastLocation[selected.Id], target);
                    var t = d / speed + handleSec;

                    var ar = assignments[selected.Id];
                    ar.AssignedJobCount += 1;
                    ar.TotalDistance += (int)Math.Round(d);
                    ar.EstimatedTimeSeconds += (int)Math.Round(t);
                    ar.OrderedLocations.Add(target);

                    if (job.WeightKg.HasValue) remainingKg[selected.Id] -= job.WeightKg.Value;
                    if (job.Quantity.HasValue) remainingCases[selected.Id] -= job.Quantity.Value;
                    lastLocation[selected.Id] = target;
                    lastCorridor[selected.Id] = job.Corridor;
                }
            }

            result.Results = assignments.Values.ToList();
            return result;
        }
    }
}