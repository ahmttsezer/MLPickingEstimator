using Microsoft.ML;
using Microsoft.ML.AutoML;
using MLPickingEstimator.Models;

namespace MLPickingEstimator.Services
{
    /// <summary>
    /// AutoML ile otomatik model seçimi servisi
    /// </summary>
    public class AutoMLService
    {
        private readonly MLContext _mlContext;

        public AutoMLService()
        {
            _mlContext = new MLContext(seed: 0);
        }

        /// <summary>
        /// AutoML ile en iyi modeli bulur
        /// </summary>
        public Task<Microsoft.ML.AutoML.ExperimentResult<Microsoft.ML.Data.RegressionMetrics>> FindBestModelAsync(string dataPath, int maxExperimentTimeInSeconds = 60)
        {
            Console.WriteLine("🔍 AutoML ile en iyi model aranıyor...");

            try
            {
                // Veriyi yükle
                IDataView data = _mlContext.Data.LoadFromTextFile<ProductPickingData>(
                    path: dataPath,
                    hasHeader: true,
                    separatorChar: ',');

                // AutoML deney ayarları
                var experimentSettings = new RegressionExperimentSettings
                {
                    MaxExperimentTimeInSeconds = (uint)maxExperimentTimeInSeconds,
                    OptimizingMetric = RegressionMetric.RSquared,
                    CacheDirectoryName = "AutoMLCache"
                };

                // Deneyi çalıştır
                var experiment = _mlContext.Auto().CreateRegressionExperiment(experimentSettings);
                var result = experiment.Execute(data, labelColumnName: "PickingTime");

                Console.WriteLine($"✅ En iyi model bulundu: {result.BestRun.TrainerName}");
                Console.WriteLine($"📊 R² Skoru: {result.BestRun.ValidationMetrics.RSquared:F4}");

                return Task.FromResult(result);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ AutoML hatası: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// AutoML sonuçlarını analiz eder
        /// </summary>
        public void AnalyzeResults(Microsoft.ML.AutoML.ExperimentResult<Microsoft.ML.Data.RegressionMetrics> result)
        {
            Console.WriteLine("\n📈 AutoML Sonuç Analizi:");
            Console.WriteLine(new string('=', 50));

            var runs = result.RunDetails.OrderByDescending(r => r.ValidationMetrics.RSquared).Take(5);

            foreach (var run in runs)
            {
                Console.WriteLine($"🏆 Trainer: {run.TrainerName}");
                Console.WriteLine($"   R²: {run.ValidationMetrics.RSquared:F4}");
                Console.WriteLine($"   MAE: {run.ValidationMetrics.MeanAbsoluteError:F4}");
                Console.WriteLine($"   RMSE: {run.ValidationMetrics.RootMeanSquaredError:F4}");
                Console.WriteLine($"   Süre: {run.RuntimeInSeconds:F2} saniye");
                Console.WriteLine();
            }
        }
    }
}
