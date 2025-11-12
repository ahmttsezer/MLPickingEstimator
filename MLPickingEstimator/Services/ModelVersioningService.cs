using System.Text.Json;
using MLPickingEstimator.Models;

namespace MLPickingEstimator.Services
{
    /// <summary>
    /// Model ve veri sürümleme, arşivleme ve rotasyon politikaları
    /// </summary>
    public class ModelVersioningService
    {
        private readonly string _modelsDir;
        private readonly string _archiveDir;
        private readonly string _versionsFile;
        private readonly int _retentionCount;

        public ModelVersioningService(string modelsDir, string archiveDir, string versionsFile, int retentionCount = 5)
        {
            _modelsDir = modelsDir;
            _archiveDir = archiveDir;
            _versionsFile = versionsFile;
            _retentionCount = retentionCount <= 0 ? 5 : retentionCount;
            Directory.CreateDirectory(_modelsDir);
            Directory.CreateDirectory(_archiveDir);
        }

        public void SaveVersion(ModelMetrics metrics, string modelFilePath)
        {
            var version = new ModelVersionInfo
            {
                Version = DateTime.UtcNow.ToString("yyyyMMddHHmmss"),
                Timestamp = DateTime.UtcNow,
                TrainerName = metrics.TrainerName,
                RSquared = metrics.RSquared,
                MAE = metrics.MeanAbsoluteError,
                RMSE = metrics.RootMeanSquaredError
            };

            // Arşivle
            var archivePath = Path.Combine(_archiveDir, $"picking_model_{version.Version}.zip");
            if (File.Exists(modelFilePath))
            {
                File.Copy(modelFilePath, archivePath, overwrite: true);
            }

            // Versiyon dosyasını güncelle
            var versions = LoadVersions();
            versions.Insert(0, version);
            // Rotasyon: son N sürümü tut
            if (versions.Count > _retentionCount)
            {
                foreach (var old in versions.Skip(_retentionCount).ToList())
                {
                    var oldArchive = Path.Combine(_archiveDir, $"picking_model_{old.Version}.zip");
                    if (File.Exists(oldArchive))
                    {
                        try { File.Delete(oldArchive); } catch { /* ignore */ }
                    }
                }
                versions = versions.Take(_retentionCount).ToList();
            }

            var json = JsonSerializer.Serialize(versions, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_versionsFile, json);
        }

        public List<ModelVersionInfo> LoadVersions()
        {
            if (!File.Exists(_versionsFile))
                return new List<ModelVersionInfo>();
            try
            {
                var json = File.ReadAllText(_versionsFile);
                return JsonSerializer.Deserialize<List<ModelVersionInfo>>(json) ?? new List<ModelVersionInfo>();
            }
            catch
            {
                return new List<ModelVersionInfo>();
            }
        }
    }

    public class ModelVersionInfo
    {
        public string Version { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public string TrainerName { get; set; } = string.Empty;
        public double RSquared { get; set; }
        public double MAE { get; set; }
        public double RMSE { get; set; }
    }
}