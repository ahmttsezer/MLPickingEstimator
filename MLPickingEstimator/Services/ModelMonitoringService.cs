using Microsoft.ML;
using Microsoft.ML.Data;
using MLPickingEstimator.Models;

namespace MLPickingEstimator.Services
{
    /// <summary>
    /// Model performans izleme ve analiz servisi
    /// </summary>
    public class ModelMonitoringService
    {
        private readonly MLContext _mlContext;
        private readonly List<PredictionRecord> _predictionHistory;
        private readonly List<ModelPerformanceRecord> _performanceHistory;

        public ModelMonitoringService()
        {
            _mlContext = new MLContext(seed: 0);
            _predictionHistory = new List<PredictionRecord>();
            _performanceHistory = new List<ModelPerformanceRecord>();
        }

        /// <summary>
        /// Tahmin kaydını loglar
        /// </summary>
        public void LogPrediction(ProductPickingData input, PickingTimePrediction prediction, float actualTime = 0)
        {
            var record = new PredictionRecord
            {
                Timestamp = DateTime.UtcNow,
                Input = input,
                PredictedTime = prediction.PredictedTime,
                ActualTime = actualTime,
                Error = actualTime > 0 ? Math.Abs(prediction.PredictedTime - actualTime) : 0,
                ModelVersion = "1.0"
            };

            _predictionHistory.Add(record);

            // Basit rotasyon politikası: en fazla 10,000 kayıt tut, fazlasını sil
            const int maxRecords = 10000;
            if (_predictionHistory.Count > maxRecords)
            {
                var removeCount = _predictionHistory.Count - maxRecords;
                _predictionHistory.RemoveRange(0, removeCount);
            }
        }

        /// <summary>
        /// Model performansını analiz eder
        /// </summary>
        public ModelAnalysisResult AnalyzeModelPerformance()
        {
            if (!_predictionHistory.Any())
                return new ModelAnalysisResult { Status = "No data available" };

            var recentPredictions = _predictionHistory
                .Where(p => p.ActualTime > 0)
                .TakeLast(100)
                .ToList();

            if (!recentPredictions.Any())
                return new ModelAnalysisResult { Status = "No actual values for comparison" };

            var analysis = new ModelAnalysisResult
            {
                TotalPredictions = _predictionHistory.Count,
                PredictionsWithActualValues = recentPredictions.Count,
                AverageError = recentPredictions.Average(p => p.Error),
                MaxError = recentPredictions.Max(p => p.Error),
                MinError = recentPredictions.Min(p => p.Error),
                RMSE = Math.Sqrt(recentPredictions.Average(p => p.Error * p.Error)),
                Status = "Analysis completed"
            };

            // Model drift detection
            var recentErrors = recentPredictions.TakeLast(20).Average(p => p.Error);
            var olderErrors = recentPredictions.SkipLast(20).TakeLast(20).Average(p => p.Error);
            
            if (Math.Abs(recentErrors - olderErrors) > 0.5)
            {
                analysis.DriftDetected = true;
                analysis.DriftMessage = $"Model drift detected. Recent error: {recentErrors:F2}, Older error: {olderErrors:F2}";
            }

            return analysis;
        }

        /// <summary>
        /// Tahmin dağılımını analiz eder
        /// </summary>
        public PredictionDistribution AnalyzePredictionDistribution()
        {
            var predictions = _predictionHistory.Select(p => p.PredictedTime).ToList();

            if (predictions.Count == 0)
            {
                return new PredictionDistribution
                {
                    Min = 0,
                    Max = 0,
                    Mean = 0,
                    Median = 0,
                    StandardDeviation = 0,
                    Percentile25 = 0,
                    Percentile75 = 0
                };
            }

            return new PredictionDistribution
            {
                Min = predictions.Min(),
                Max = predictions.Max(),
                Mean = predictions.Average(),
                Median = predictions.OrderBy(x => x).Skip(predictions.Count / 2).First(),
                StandardDeviation = Math.Sqrt(predictions.Average(x => Math.Pow(x - predictions.Average(), 2))),
                Percentile25 = predictions.OrderBy(x => x).Skip(predictions.Count / 4).First(),
                Percentile75 = predictions.OrderBy(x => x).Skip(3 * predictions.Count / 4).First()
            };
        }

        /// <summary>
        /// Son N giriş verisini döner (drift analizi için)
        /// </summary>
        public List<ProductPickingData> GetRecentInputs(int count = 100)
        {
            return _predictionHistory
                .TakeLast(count)
                .Select(p => p.Input)
                .ToList();
        }

        /// <summary>
        /// Performans raporu oluşturur
        /// </summary>
        public void GeneratePerformanceReport()
        {
            Console.WriteLine("\n📊 Model Performans Raporu");
            Console.WriteLine(new string('=', 50));

            var analysis = AnalyzeModelPerformance();
            var distribution = AnalyzePredictionDistribution();

            Console.WriteLine($"Toplam Tahmin Sayısı: {analysis.TotalPredictions}");
            Console.WriteLine($"Gerçek Değerle Karşılaştırılan: {analysis.PredictionsWithActualValues}");
            Console.WriteLine($"Ortalama Hata: {analysis.AverageError:F2} dakika");
            Console.WriteLine($"Maksimum Hata: {analysis.MaxError:F2} dakika");
            Console.WriteLine($"Minimum Hata: {analysis.MinError:F2} dakika");
            Console.WriteLine($"RMSE: {analysis.RMSE:F2} dakika");

            if (analysis.DriftDetected)
            {
                Console.WriteLine($"⚠️ {analysis.DriftMessage}");
            }

            Console.WriteLine("\n📈 Tahmin Dağılımı:");
            Console.WriteLine($"Ortalama: {distribution.Mean:F2} dakika");
            Console.WriteLine($"Medyan: {distribution.Median:F2} dakika");
            Console.WriteLine($"Standart Sapma: {distribution.StandardDeviation:F2}");
            Console.WriteLine($"25. Persentil: {distribution.Percentile25:F2} dakika");
            Console.WriteLine($"75. Persentil: {distribution.Percentile75:F2} dakika");
        }

        /// <summary>
        /// Tahmin geçmişini temizler
        /// </summary>
        public void ClearHistory()
        {
            _predictionHistory.Clear();
            _performanceHistory.Clear();
            Console.WriteLine("✅ Tahmin geçmişi temizlendi.");
        }

        /// <summary>
        /// Tahmin geçmişini CSV dosyasına yazar
        /// </summary>
        public void ExportHistoryCsv(string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            using var sw = new StreamWriter(path, false);
            sw.WriteLine("Timestamp,ItemCount,Weight,Volume,Distance,PickerExperience,StockDensity,PredictedTime,ActualTime,Error,ModelVersion");
            foreach (var r in _predictionHistory)
            {
                sw.WriteLine($"{r.Timestamp:o},{r.Input.ItemCount},{r.Input.Weight},{r.Input.Volume},{r.Input.Distance},{r.Input.PickerExperience},{r.Input.StockDensity},{r.PredictedTime},{r.ActualTime},{r.Error},{r.ModelVersion}");
            }
            sw.Flush();
        }
    }

    /// <summary>
    /// Tahmin kaydı
    /// </summary>
    public class PredictionRecord
    {
        public DateTime Timestamp { get; set; }
        public ProductPickingData Input { get; set; } = new();
        public float PredictedTime { get; set; }
        public float ActualTime { get; set; }
        public float Error { get; set; }
        public string ModelVersion { get; set; } = string.Empty;
    }

    /// <summary>
    /// Model performans kaydı
    /// </summary>
    public class ModelPerformanceRecord
    {
        public DateTime Timestamp { get; set; }
        public string ModelVersion { get; set; } = string.Empty;
        public double RSquared { get; set; }
        public double MAE { get; set; }
        public double RMSE { get; set; }
    }

    /// <summary>
    /// Model analiz sonucu
    /// </summary>
    public class ModelAnalysisResult
    {
        public int TotalPredictions { get; set; }
        public int PredictionsWithActualValues { get; set; }
        public double AverageError { get; set; }
        public double MaxError { get; set; }
        public double MinError { get; set; }
        public double RMSE { get; set; }
        public bool DriftDetected { get; set; }
        public string DriftMessage { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
    }

    /// <summary>
    /// Tahmin dağılımı
    /// </summary>
    public class PredictionDistribution
    {
        public float Min { get; set; }
        public float Max { get; set; }
        public double Mean { get; set; }
        public float Median { get; set; }
        public double StandardDeviation { get; set; }
        public float Percentile25 { get; set; }
        public float Percentile75 { get; set; }
    }
}
