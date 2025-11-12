using System.Collections.Concurrent;

namespace MLPickingEstimator.Services
{
    public class TelemetryService
    {
        private readonly ConcurrentDictionary<string, LiveMetrics> _live = new();

        public void Update(string personId, double itemsPicked, double hoursWorked, double errors = 0, double distanceMeters = 0, DateTime? timestamp = null)
        {
            _live[personId] = new LiveMetrics
            {
                ItemsPicked = itemsPicked,
                HoursWorked = hoursWorked,
                Errors = errors,
                DistanceMeters = distanceMeters,
                Timestamp = timestamp ?? DateTime.UtcNow
            };
        }

        public IReadOnlyDictionary<string, double> GetLivePF(double targetRatePerHour, IReadOnlyDictionary<string, double>? baseline = null)
        {
            var dict = new Dictionary<string, double>();
            foreach (var kv in _live)
            {
                var b = baseline != null && baseline.TryGetValue(kv.Key, out var bl) ? bl : (double?)null;
                dict[kv.Key] = ComputePF(kv.Value, targetRatePerHour, b);
            }
            return dict;
        }

        private static double ComputePF(LiveMetrics m, double targetRatePerHour, double? baseline)
        {
            var rate = m.HoursWorked > 0 ? m.ItemsPicked / m.HoursWorked : 0;
            var pf = targetRatePerHour > 0 ? rate / targetRatePerHour : 0;
            if (baseline.HasValue)
            {
                // Baseline ile canlı PF’i birleştir (örnek: %50-%50 ağırlık)
                pf = 0.5 * baseline.Value + 0.5 * pf;
            }
            // PF’yi makul sınırlar içinde tut ve 2 ondalık basamağa yuvarla
            pf = Math.Clamp(pf, 0.2, 2.0);
            return Math.Round(pf, 2);
        }

        public class LiveMetrics
        {
            public double ItemsPicked { get; set; }
            public double HoursWorked { get; set; }
            public double Errors { get; set; }
            public double DistanceMeters { get; set; }
            public DateTime Timestamp { get; set; }
        }
    }
}