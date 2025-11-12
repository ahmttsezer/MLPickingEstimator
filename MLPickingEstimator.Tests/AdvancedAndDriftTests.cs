using MLPickingEstimator.Services;
using MLPickingEstimator.Models;
using Xunit;

namespace MLPickingEstimator.Tests
{
    public class AdvancedAndDriftTests
    {
        [Fact]
        public void TrainMultipleModels_ReturnsMetricsForAlgorithms()
        {
            var svc = new AdvancedMLPickingService();
            var dataPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "MLPickingEstimator", "Data", "picking_data.csv");
            dataPath = Path.GetFullPath(dataPath);

            var results = svc.TrainMultipleModelsAsync(dataPath).Result;
            Assert.NotNull(results);
            Assert.True(results.Count >= 2);
            Assert.All(results.Values, m => Assert.True(m.RSquared >= 0));
        }

        [Fact]
        public void DataDrift_EvaluateTriggersAlarmWhenThresholdExceeded()
        {
            var drift = new DataDriftService();
            var baseline = new FeatureMeans
            {
                ItemCount = 10,
                Weight = 5,
                Volume = 1,
                Distance = 100,
                PickerExperience = 5,
                StockDensity = 0.8
            };

            var live = new FeatureMeans
            {
                ItemCount = 20, // %100 artış
                Weight = 5,
                Volume = 1,
                Distance = 100,
                PickerExperience = 5,
                StockDensity = 0.8
            };

            var status = drift.Evaluate(baseline, live, thresholdPercent: 20);
            Assert.True(status.Alarm);
            Assert.Contains(nameof(FeatureMeans.ItemCount), status.DriftRatio.Keys);
            Assert.True(status.DriftRatio[nameof(FeatureMeans.ItemCount)] >= 100);
        }
    }
}