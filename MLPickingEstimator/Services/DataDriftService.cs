using Microsoft.ML;
using Microsoft.ML.Data;
using MLPickingEstimator.Models;

namespace MLPickingEstimator.Services
{
    // Yalnızca eğitim CSV şemasını yüklemek için yardımcı sınıf
    internal class TrainingRow
    {
        [LoadColumn(0)] public float ItemCount { get; set; }
        [LoadColumn(1)] public float Weight { get; set; }
        [LoadColumn(2)] public float Volume { get; set; }
        [LoadColumn(3)] public float Distance { get; set; }
        [LoadColumn(4)] public float PickerExperience { get; set; }
        [LoadColumn(5)] public float StockDensity { get; set; }
        [LoadColumn(6)] public float PickingTime { get; set; }
    }
    public class FeatureMeans
    {
        public double ItemCount { get; set; }
        public double Weight { get; set; }
        public double Volume { get; set; }
        public double Distance { get; set; }
        public double PickerExperience { get; set; }
        public double StockDensity { get; set; }
    }

    public class DriftStatus
    {
        public FeatureMeans TrainingMeans { get; set; } = new();
        public FeatureMeans LiveMeans { get; set; } = new();
        public Dictionary<string, double> DriftRatio { get; set; } = new();
        public bool Alarm { get; set; }
        public double ThresholdPercent { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    public class DataDriftService
    {
        private readonly MLContext _ml = new(seed: 0);

        public FeatureMeans ComputeTrainingMeans(string trainingPath)
        {
            try
            {
                var loader = _ml.Data.CreateTextLoader(new TextLoader.Options
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
                        new TextLoader.Column(nameof(ProductPickingData.PickingTime), DataKind.Single, 6),
                    }
                });
                var data = loader.Load(trainingPath);
                var rows = _ml.Data.CreateEnumerable<TrainingRow>(data, reuseRowObject: false).ToList();
                if (!rows.Any()) return new FeatureMeans();
                return new FeatureMeans
                {
                    ItemCount = rows.Average(x => x.ItemCount),
                    Weight = rows.Average(x => x.Weight),
                    Volume = rows.Average(x => x.Volume),
                    Distance = rows.Average(x => x.Distance),
                    PickerExperience = rows.Average(x => x.PickerExperience),
                    StockDensity = rows.Average(x => x.StockDensity)
                };
            }
            catch
            {
                return new FeatureMeans();
            }
        }

        public FeatureMeans ComputeMeans(IEnumerable<ProductPickingData> rows)
        {
            var list = rows.ToList();
            if (!list.Any()) return new FeatureMeans();
            return new FeatureMeans
            {
                ItemCount = list.Average(x => x.ItemCount),
                Weight = list.Average(x => x.Weight),
                Volume = list.Average(x => x.Volume),
                Distance = list.Average(x => x.Distance),
                PickerExperience = list.Average(x => x.PickerExperience),
                StockDensity = list.Average(x => x.StockDensity)
            };
        }

        public DriftStatus Evaluate(FeatureMeans baseline, FeatureMeans live, double thresholdPercent = 20)
        {
            double Ratio(double a, double b) => a == 0 ? 0 : Math.Abs(b - a) / Math.Abs(a) * 100.0;
            var ratios = new Dictionary<string, double>
            {
                [nameof(FeatureMeans.ItemCount)] = Ratio(baseline.ItemCount, live.ItemCount),
                [nameof(FeatureMeans.Weight)] = Ratio(baseline.Weight, live.Weight),
                [nameof(FeatureMeans.Volume)] = Ratio(baseline.Volume, live.Volume),
                [nameof(FeatureMeans.Distance)] = Ratio(baseline.Distance, live.Distance),
                [nameof(FeatureMeans.PickerExperience)] = Ratio(baseline.PickerExperience, live.PickerExperience),
                [nameof(FeatureMeans.StockDensity)] = Ratio(baseline.StockDensity, live.StockDensity),
            };

            var alarm = ratios.Values.Any(r => r >= thresholdPercent);
            return new DriftStatus
            {
                TrainingMeans = baseline,
                LiveMeans = live,
                DriftRatio = ratios,
                Alarm = alarm,
                ThresholdPercent = thresholdPercent,
                Message = alarm ? "Data drift alarm: bir veya daha fazla özellik eşik üzerinde." : "Drift tespit edilmedi."
            };
        }
    }
}