using System.Collections.Generic;

namespace MLPickingEstimator.Models
{
    public class SimulationRequest
    {
        public LocationPoint LastLocation { get; set; } = new LocationPoint();
        public List<LocationPoint> TaskLocations { get; set; } = new List<LocationPoint>();
        public string? LastLocationCode { get; set; }
        public List<string>? TaskLocationCodes { get; set; }
    }

    public class SimulationResult
    {
        public List<LocationPoint> OrderedLocations { get; set; } = new List<LocationPoint>();
        public double TotalDistance { get; set; }
    }
}