using Microsoft.ML;
using Microsoft.ML.Data;
using MLPickingEstimator.Models;

namespace MLPickingEstimator.Services
{
    /// <summary>
    /// Gelişmiş ML.NET model eğitim ve tahmin servisi
    /// </summary>
    public class AdvancedMLPickingService : MLPickingService
    {
        private readonly Dictionary<string, ITransformer> _models;
        private readonly Dictionary<string, ModelMetrics> _modelMetrics;

        public AdvancedMLPickingService() : base()
        {
            _models = new Dictionary<string, ITransformer>();
            _modelMetrics = new Dictionary<string, ModelMetrics>();
        }

        /// <summary>
        /// Çoklu algoritma ile model eğitimi
        /// </summary>
        public Task<Dictionary<string, ModelMetrics>> TrainMultipleModelsAsync(string dataPath)
        {
            Console.WriteLine("🚀 Çoklu algoritma ile model eğitimi başlatılıyor...");

            var results = new Dictionary<string, ModelMetrics>();
            var algorithms = new Dictionary<string, IEstimator<ITransformer>>
            {
                ["FastTree"] = _mlContext.Regression.Trainers.FastTree(
                    labelColumnName: "PickingTime",
                    numberOfLeaves: 20,
                    numberOfTrees: 100,
                    minimumExampleCountPerLeaf: 2),
                
                ["FastForest"] = _mlContext.Regression.Trainers.FastForest(
                    labelColumnName: "PickingTime",
                    numberOfLeaves: 20,
                    numberOfTrees: 100),
                
                ["LinearRegression"] = _mlContext.Regression.Trainers.Sdca(
                    labelColumnName: "PickingTime"),
                
                ["SdcaRegression"] = _mlContext.Regression.Trainers.Sdca(
                    labelColumnName: "PickingTime")
            };

            // Veriyi yükle (şemayı açıkça tanımla)
            var loader = _mlContext.Data.CreateTextLoader(new TextLoader.Options
            {
                HasHeader = true,
                Separators = new[] { ',' },
                Columns = new[]
                {
                    new TextLoader.Column(nameof(ProductPickingData.ItemCount), DataKind.Single, 0),
                    new TextLoader.Column(nameof(ProductPickingData.Weight), DataKind.Single, 1),
                    new TextLoader.Column(nameof(ProductPickingData.Volume), DataKind.Single, 2),
                    new TextLoader.Column(nameof(ProductPickingData.Distance), DataKind.Single, 3),
                    new TextLoader.Column(nameof(ProductPickingData.PickerExperience), DataKind.Single, 4),
                    new TextLoader.Column(nameof(ProductPickingData.StockDensity), DataKind.Single, 5),
                    new TextLoader.Column("PickingTime", DataKind.Single, 6),
                }
            });
            IDataView data = loader.Load(dataPath);

            var split = _mlContext.Data.TrainTestSplit(data, testFraction: 0.2);

            foreach (var algorithm in algorithms)
            {
                Console.WriteLine($"📊 {algorithm.Key} algoritması eğitiliyor...");
                
                var pipeline = _mlContext.Transforms.Concatenate("Features",
                                nameof(ProductPickingData.ItemCount),
                                nameof(ProductPickingData.Weight),
                                nameof(ProductPickingData.Volume),
                                nameof(ProductPickingData.Distance),
                                nameof(ProductPickingData.PickerExperience),
                                nameof(ProductPickingData.StockDensity))
                            .Append(_mlContext.Transforms.NormalizeMinMax("Features"))
                            .Append(algorithm.Value);

                var startTime = DateTime.Now;
                var model = pipeline.Fit(split.TrainSet);
                var trainingTime = DateTime.Now - startTime;

                var predictions = model.Transform(split.TestSet);
                var metrics = _mlContext.Regression.Evaluate(predictions, labelColumnName: "PickingTime");

                _models[algorithm.Key] = model;
                
                var modelMetrics = new ModelMetrics
                {
                    RSquared = metrics.RSquared,
                    MeanAbsoluteError = metrics.MeanAbsoluteError,
                    RootMeanSquaredError = metrics.RootMeanSquaredError,
                    Loss = metrics.LossFunction,
                    TrainerName = algorithm.Key,
                    TrainingTime = trainingTime
                };

                _modelMetrics[algorithm.Key] = modelMetrics;
                results[algorithm.Key] = modelMetrics;

                Console.WriteLine($"✅ {algorithm.Key} tamamlandı - R²: {metrics.RSquared:F4}");
            }

            return Task.FromResult(results);
        }

        /// <summary>
        /// Ensemble tahmin yapar
        /// </summary>
        public PickingTimePrediction PredictEnsemble(ProductPickingData input)
        {
            if (!_models.Any())
                throw new InvalidOperationException("Hiç model eğitilmemiş.");

            var predictions = new List<float>();
            var weights = new List<float>();

            foreach (var model in _models)
            {
                var engine = _mlContext.Model.CreatePredictionEngine<ProductPickingData, PickingTimePrediction>(model.Value);
                var prediction = engine.Predict(input);
                predictions.Add(prediction.PredictedTime);
                
                // Model performansına göre ağırlık
                var rSquared = _modelMetrics[model.Key].RSquared;
                weights.Add((float)rSquared);
            }

            // Ağırlıklı ortalama
            var weightedSum = predictions.Zip(weights, (p, w) => p * w).Sum();
            var totalWeight = weights.Sum();
            var ensemblePrediction = weightedSum / totalWeight;

            return new PickingTimePrediction { PredictedTime = ensemblePrediction };
        }

        /// <summary>
        /// Model performans karşılaştırması
        /// </summary>
        public void CompareModels()
        {
            Console.WriteLine("\n📊 Model Performans Karşılaştırması:");
            Console.WriteLine(new string('=', 80));
            Console.WriteLine($"{"Algoritma",-20} {"R²",-10} {"MAE",-10} {"RMSE",-10} {"Süre (s)",-10}");
            Console.WriteLine(new string('-', 80));

            var sortedModels = _modelMetrics.OrderByDescending(m => m.Value.RSquared);

            foreach (var model in sortedModels)
            {
                Console.WriteLine($"{model.Key,-20} {model.Value.RSquared,-10:F4} " +
                                $"{model.Value.MeanAbsoluteError,-10:F4} " +
                                $"{model.Value.RootMeanSquaredError,-10:F4} " +
                                $"{model.Value.TrainingTime.TotalSeconds,-10:F2}");
            }

            var bestModel = sortedModels.First();
            Console.WriteLine($"\n🏆 En İyi Model: {bestModel.Key} (R²: {bestModel.Value.RSquared:F4})");
        }

        /// <summary>
        /// Model önerisi yapar
        /// </summary>
        public string RecommendModel(string criteria = "accuracy")
        {
            if (!_modelMetrics.Any())
                return "Hiç model eğitilmemiş.";

            return criteria.ToLower() switch
            {
                "accuracy" => _modelMetrics.OrderByDescending(m => m.Value.RSquared).First().Key,
                "speed" => _modelMetrics.OrderBy(m => m.Value.TrainingTime).First().Key,
                "balanced" => _modelMetrics.OrderByDescending(m => m.Value.RSquared / m.Value.TrainingTime.TotalSeconds).First().Key,
                _ => _modelMetrics.OrderByDescending(m => m.Value.RSquared).First().Key
            };
        }
    }
}
