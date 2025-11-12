using MLPickingEstimator.Models;

namespace MLPickingEstimator.Services
{
    public class LocationResolver
    {
        public LocationPoint Resolve(string? code)
        {
            if (string.IsNullOrWhiteSpace(code)) return new LocationPoint { X = 200, Y = 200 };

            // Örn: PX-MZ-D08-171F -> Zone=MZ, Corridor=D08
            var zone = "MZ";
            var aisleNum = 8;
            try
            {
                var parts = code.Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (parts.Length >= 3)
                {
                    zone = parts[1];
                    var corr = parts[2];
                    var numStr = new string(corr.Where(char.IsDigit).ToArray());
                    if (int.TryParse(numStr, out var num)) aisleNum = num;
                }
            }
            catch { }

            var x = zone switch
            {
                "MZ" => 460,
                "MC" => 110,
                "PM02" => 200,
                _ => 810
            };
            var y = 60 + aisleNum * 10; // basit ölçekleme
            return new LocationPoint { X = x, Y = y };
        }

        public string ExtractCorridor(string? code)
        {
            if (string.IsNullOrWhiteSpace(code)) return string.Empty;
            try
            {
                var parts = code.Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (parts.Length >= 3) return parts[2];
            }
            catch { }
            return string.Empty;
        }
    }
}