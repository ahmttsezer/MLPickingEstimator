using Microsoft.ML;
using Microsoft.ML.Data;
using MLPickingEstimator.Models;

namespace MLPickingEstimator.Services
{
    /// <summary>
    /// ML.NET model eğitim ve tahmin servisi
    /// </summary>
    public class MLPickingService
    {
        protected readonly MLContext _mlContext;
        private ITransformer? _model;
        private PredictionEngine<ProductPickingData, PickingTimePrediction>? _predictionEngine;
        public bool IsModelLoaded => _model != null;

        public MLPickingService()
        {
            _mlContext = new MLContext(seed: 0);
        }

        /// <summary>
        /// Modeli eğitir ve kaydeder
        /// </summary>
        public Task<ModelMetrics> TrainModelAsync(string dataPath)
        {
            Console.WriteLine("📊 Model eğitimi başlatılıyor...");

            // Veriyi yükle (TextLoader.Options ile şemayı açıkça tanımla)
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

            // Eğitim/Test bölme
            var split = _mlContext.Data.TrainTestSplit(data, testFraction: 0.2);

            // Pipeline oluştur
            var pipeline = _mlContext.Transforms.Concatenate("Features",
                            nameof(ProductPickingData.ItemCount),
                            nameof(ProductPickingData.Weight),
                            nameof(ProductPickingData.Volume),
                            nameof(ProductPickingData.Distance),
                            nameof(ProductPickingData.PickerExperience),
                            nameof(ProductPickingData.StockDensity))
                        .Append(_mlContext.Transforms.NormalizeMinMax("Features"))
                        .Append(_mlContext.Regression.Trainers.FastTree(
                            labelColumnName: "PickingTime",
                            numberOfLeaves: 20,
                            numberOfTrees: 100,
                            minimumExampleCountPerLeaf: 2));

            // Model eğit
            var startTime = DateTime.Now;
            _model = pipeline.Fit(split.TrainSet);
            var trainingTime = DateTime.Now - startTime;

            // Değerlendirme
            var predictions = _model.Transform(split.TestSet);
            var metrics = _mlContext.Regression.Evaluate(predictions, labelColumnName: "PickingTime");

            // Prediction engine oluştur
            _predictionEngine = _mlContext.Model.CreatePredictionEngine<ProductPickingData, PickingTimePrediction>(_model);

            // Modeli kaydet (eğitim veri şemasını kullan)
            var modelPath = Path.Combine("Models", "picking_model.zip");
            Directory.CreateDirectory(Path.GetDirectoryName(modelPath)!);
            _mlContext.Model.Save(_model, split.TrainSet.Schema, modelPath);

            return Task.FromResult(new ModelMetrics
            {
                RSquared = metrics.RSquared,
                MeanAbsoluteError = metrics.MeanAbsoluteError,
                RootMeanSquaredError = metrics.RootMeanSquaredError,
                Loss = metrics.LossFunction,
                TrainerName = "FastTree",
                TrainingTime = trainingTime
            });
        }

        /// <summary>
        /// Kaydedilmiş modeli yükler
        /// </summary>
        public Task LoadModelAsync(string modelPath)
        {
            if (!File.Exists(modelPath))
                throw new FileNotFoundException($"Model dosyası bulunamadı: {modelPath}");

            _model = _mlContext.Model.Load(modelPath, out _);
            _predictionEngine = _mlContext.Model.CreatePredictionEngine<ProductPickingData, PickingTimePrediction>(_model);
            return Task.CompletedTask;
        }

        /// <summary>
        /// Eğitilmiş modeli belirtilen dosya yoluna kaydeder
        /// </summary>
        public void SaveModel(string modelPath)
        {
            if (_model == null)
                throw new InvalidOperationException("Model yüklenmemiş, önce eğitilmeli veya yüklenmeli.");

            Directory.CreateDirectory(Path.GetDirectoryName(modelPath)!);
            var schemaSource = _mlContext.Data.LoadFromEnumerable(new List<ProductPickingData>());
            _mlContext.Model.Save(_model, schemaSource.Schema, modelPath);
        }

        /// <summary>
        /// Tahmin yapar
        /// </summary>
        public PickingTimePrediction Predict(ProductPickingData input)
        {
            if (_model == null)
                throw new InvalidOperationException("Model yüklenmemiş. Önce LoadModelAsync veya TrainModelAsync çağırın.");

            // PredictionEngine thread-safe değildir; her çağrıda yeni engine oluştur
            using var engine = _mlContext.Model.CreatePredictionEngine<ProductPickingData, PickingTimePrediction>(_model);
            return engine.Predict(input);
        }

        /// <summary>
        /// Modeli kaydeder
        /// </summary>
        private Task SaveModelAsync(string path)
        {
            if (_model == null)
                throw new InvalidOperationException("Kaydedilecek model bulunamadı.");

            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            // Şemayı veri sınıfından türet (boş enumerable ile)
            var emptyData = _mlContext.Data.LoadFromEnumerable(new List<ProductPickingData>());
            _mlContext.Model.Save(_model, emptyData.Schema, path);
            return Task.CompletedTask;
        }
    }
}
