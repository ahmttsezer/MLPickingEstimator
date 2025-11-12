namespace MLPickingEstimator.Models
{
    public class Vehicle
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public LocationPoint CurrentLocation { get; set; } = new();
        public string? CurrentLocationCode { get; set; }
        public double? CapacityKg { get; set; }
        public int? CapacityCases { get; set; } // Koli kapasitesi (adet)
        public string? Zone { get; set; } // Bölge/Zone bilgisi (örn: A1, MZ)
    }

    public class JobTask
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public LocationPoint Location { get; set; } = new();
        public double? WeightKg { get; set; }
        public int Priority { get; set; } = 0; // Daha yüksek değer daha öncelikli
        public double? EarliestTime { get; set; } // Zaman penceresi başlangıcı (soyut birim)
        public double? LatestTime { get; set; }   // Zaman penceresi bitişi (soyut birim)
        public string? Zone { get; set; } // İşin Zone bilgisi (raf-ID’den türetilebilir)
        public int? Quantity { get; set; } // Yapılacak Miktar (adet/koli)
        public string? OrderId { get; set; } // Depo Çıkış Siparişi veya iş emri grubu
        public string? RackId { get; set; } // PX-*-*-* gibi raf adresi
        public string? Corridor { get; set; } // Raf adresinden türetilen koridor (örn: D08)
    }

    public class DispatchRequest
    {
        public List<Vehicle> Vehicles { get; set; } = new();
        public List<JobTask> Jobs { get; set; } = new();
        public double? AverageSpeedMps { get; set; } // Ortalama hız (m/s)
        public double? HandlingSecondsPerTask { get; set; } // Her görev için işlem süresi
    }

    public class DispatchAssignmentResult
    {
        public string VehicleId { get; set; } = string.Empty;
        public List<LocationPoint> OrderedLocations { get; set; } = new();
        public double TotalDistance { get; set; }
        public double EstimatedTimeSeconds { get; set; }
        public int AssignedJobCount { get; set; }
    }

    public class DispatchResult
    {
        public List<DispatchAssignmentResult> Results { get; set; } = new();
    }
}