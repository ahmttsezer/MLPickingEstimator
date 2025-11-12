using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace MLPickingEstimator.Services.Health
{
    public class ModelAndDbHealthCheck : IHealthCheck
    {
        private readonly IConfiguration _config;
        public ModelAndDbHealthCheck(IConfiguration config)
        {
            _config = config;
        }

        public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            var modelsDir = _config["Paths:ModelsDir"] ?? "Models";
            var zipModel = _config["Paths:ZipModel"] ?? Path.Combine(modelsDir, "picking_model.zip");
            var dataDir = _config["Paths:DataDir"] ?? "Data";
            var dbPath = _config["Paths:Database"] ?? Path.Combine(dataDir, "telemetry.db");

            var modelExists = File.Exists(zipModel);
            var dbDirExists = Directory.Exists(Path.GetDirectoryName(dbPath)!);

            if (modelExists && dbDirExists)
            {
                return Task.FromResult(HealthCheckResult.Healthy("Model ve DB dizini hazır"));
            }
            var desc = $"ModelExists={modelExists}, DbDirExists={dbDirExists}";
            return Task.FromResult(HealthCheckResult.Degraded(desc));
        }
    }
}