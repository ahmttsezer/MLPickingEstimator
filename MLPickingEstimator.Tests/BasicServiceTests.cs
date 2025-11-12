using MLPickingEstimator.Services;
using MLPickingEstimator.Models;
using Xunit;

namespace MLPickingEstimator.Tests
{
    public class BasicServiceTests
    {
        [Fact]
        public void TrainModel_ProducesMetrics_AndSavesModel()
        {
            var service = new MLPickingService();
            var dataPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "MLPickingEstimator", "Data", "picking_data.csv");
            dataPath = Path.GetFullPath(dataPath);

            var metrics = service.TrainModelAsync(dataPath).Result;
            Assert.NotNull(metrics);
            Assert.True(metrics.RSquared > 0);

            var modelFile = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "MLPickingEstimator", "Models", "picking_model.zip");
            modelFile = Path.GetFullPath(modelFile);
            Assert.True(File.Exists(modelFile));
        }

        [Fact]
        public void Predict_ReturnsValue()
        {
            var service = new MLPickingService();
            var dataPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "MLPickingEstimator", "Data", "picking_data.csv");
            dataPath = Path.GetFullPath(dataPath);
            service.TrainModelAsync(dataPath).Wait();

            var input = new ProductPickingData
            {
                ItemCount = 10,
                Weight = 5,
                Volume = 0.5f,
                Distance = 100,
                PickerExperience = 5,
                StockDensity = 0.7f
            };

            var prediction = service.Predict(input);
            Assert.True(prediction.PredictedTime > 0);
        }

        [Fact]
        public void RouteSimulation_ComputesDistance()
        {
            var svc = new RouteSimulationService();
            var req = new SimulationRequest
            {
                LastLocation = new LocationPoint { X = 0, Y = 0 },
                TaskLocations = new List<LocationPoint>
                {
                    new LocationPoint{ X = 3, Y = 4 },
                    new LocationPoint{ X = 6, Y = 8 },
                }
            };

            var res = svc.Simulate(req);
            Assert.Equal(2, res.OrderedLocations.Count);
            Assert.True(res.TotalDistance > 0);
        }
    }
}