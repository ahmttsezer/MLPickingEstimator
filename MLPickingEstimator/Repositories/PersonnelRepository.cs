using System.Text.Json;
using MLPickingEstimator.Models;

namespace MLPickingEstimator.Repositories
{
    public interface IPersonnelRepository
    {
        IReadOnlyList<PersonnelInfo> GetPersonnel();
        IReadOnlyDictionary<string, (double x,double y)> GetLatestLocations();
        IReadOnlyDictionary<string, double> GetWeeklyPerformanceFactor();
    }

    public class PersonnelRepository : IPersonnelRepository
    {
        private readonly string _dataPath;
        private readonly object _lock = new();
        private DateTime _lastRead = DateTime.MinValue;
        private List<PersonnelInfo> _cache = new();
        private Dictionary<string,(double x,double y)> _loc = new();
        private Dictionary<string,double> _perf = new();

        public PersonnelRepository(string dataDir)
        {
            _dataPath = Path.Combine(dataDir, "personnel.json");
            EnsureSeedFile();
            Read();
        }

        private void EnsureSeedFile()
        {
            var dir = Path.GetDirectoryName(_dataPath)!;
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            if (!File.Exists(_dataPath))
            {
                var seed = new
                {
                    personnel = new []
                    {
                        new { id = "p1", name = "Ayşe", lastLocationCode = "PX-MZ-D08-171F", pickerExperience = 3, speedFactor = 1.0, zone = "MZ" },
                        new { id = "p2", name = "Mehmet", lastLocationCode = "PX-MC-A106", pickerExperience = 2, speedFactor = 0.95, zone = "MC" },
                        new { id = "p3", name = "Elif", lastLocationCode = "PX-PM02-199", pickerExperience = 4, speedFactor = 1.05, zone = "PM02" }
                    },
                    locations = new Dictionary<string,object>
                    {
                        { "p1", new { x = 460.0, y = 140.0 } },
                        { "p2", new { x = 110.0, y = 166.0 } },
                        { "p3", new { x = 200.0, y = 250.0 } }
                    },
                    weeklyPerformance = new Dictionary<string,double>
                    {
                        { "p1", 1.08 },
                        { "p2", 0.94 },
                        { "p3", 1.12 }
                    }
                };
                var json = JsonSerializer.Serialize(seed, new JsonSerializerOptions{ WriteIndented = true });
                File.WriteAllText(_dataPath, json);
            }
        }

        private void Read()
        {
            lock (_lock)
            {
                if ((DateTime.UtcNow - _lastRead).TotalSeconds < 2) return;
                if (!File.Exists(_dataPath)) { _cache = new(); _loc = new(); _perf = new(); return; }
                var doc = JsonDocument.Parse(File.ReadAllText(_dataPath));
                _cache = new();
                _loc = new();
                _perf = new();
                if (doc.RootElement.TryGetProperty("personnel", out var pEl) && pEl.ValueKind == JsonValueKind.Array)
                {
                    foreach (var e in pEl.EnumerateArray())
                    {
                        _cache.Add(new PersonnelInfo
                        {
                            Id = e.TryGetProperty("id", out var idEl) ? idEl.GetString() ?? Guid.NewGuid().ToString() : Guid.NewGuid().ToString(),
                            Name = e.TryGetProperty("name", out var nEl) ? nEl.GetString() ?? "" : "",
                            LastLocationCode = e.TryGetProperty("lastLocationCode", out var lEl) ? lEl.GetString() ?? "" : "",
                            PickerExperience = e.TryGetProperty("pickerExperience", out var exEl) ? exEl.GetInt32() : 1,
                            SpeedFactor = e.TryGetProperty("speedFactor", out var sEl) ? sEl.GetDouble() : (double?)null,
                            Zone = e.TryGetProperty("zone", out var zEl) ? zEl.GetString() : null
                        });
                    }
                }
                if (doc.RootElement.TryGetProperty("locations", out var lmap) && lmap.ValueKind == JsonValueKind.Object)
                {
                    foreach (var prop in lmap.EnumerateObject())
                    {
                        var x = prop.Value.TryGetProperty("x", out var xEl) ? xEl.GetDouble() : 0;
                        var y = prop.Value.TryGetProperty("y", out var yEl) ? yEl.GetDouble() : 0;
                        _loc[prop.Name] = (x,y);
                    }
                }
                if (doc.RootElement.TryGetProperty("weeklyPerformance", out var wmap) && wmap.ValueKind == JsonValueKind.Object)
                {
                    foreach (var prop in wmap.EnumerateObject())
                    {
                        _perf[prop.Name] = prop.Value.GetDouble();
                    }
                }
                _lastRead = DateTime.UtcNow;
            }
        }

        public IReadOnlyList<PersonnelInfo> GetPersonnel() { Read(); return _cache; }
        public IReadOnlyDictionary<string, (double x,double y)> GetLatestLocations() { Read(); return _loc; }
        public IReadOnlyDictionary<string, double> GetWeeklyPerformanceFactor() { Read(); return _perf; }
    }
}