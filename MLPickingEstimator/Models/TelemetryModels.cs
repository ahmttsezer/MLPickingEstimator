namespace MLPickingEstimator.Models
{
    public class TelemetryInput
    {
        public string PersonId { get; set; } = string.Empty;
        public double ItemsPicked { get; set; }
        public double HoursWorked { get; set; }
        public double Errors { get; set; }
        public double TravelDistanceMeters { get; set; }
        public DateTime? Timestamp { get; set; }
    }
}