using System;
using System.Collections.Generic;
using System.Linq;
using MLPickingEstimator.Models;

namespace MLPickingEstimator.Services
{
    public class RouteSimulationService
    {
        private readonly LocationResolver _resolver;

        public RouteSimulationService(LocationResolver resolver)
        {
            _resolver = resolver;
        }

        public SimulationResult Simulate(SimulationRequest request)
        {
            var ordered = new List<LocationPoint>();
            List<LocationPoint> remaining;
            LocationPoint current;

            if (request.TaskLocations != null && request.TaskLocations.Count > 0)
            {
                remaining = new List<LocationPoint>(request.TaskLocations);
                current = request.LastLocation;
            }
            else
            {
                var codes = request.TaskLocationCodes ?? new List<string>();
                remaining = codes.Select(code => _resolver.Resolve(code)).ToList();
                current = _resolver.Resolve(request.LastLocationCode);
            }
            double total = 0.0;

            while (remaining.Count > 0)
            {
                var next = remaining.OrderBy(p => Distance(current, p)).First();
                total += Distance(current, next);
                ordered.Add(next);
                current = next;
                remaining.Remove(next);
            }

            return new SimulationResult
            {
                OrderedLocations = ordered,
                TotalDistance = total
            };
        }

        public double Distance(LocationPoint a, LocationPoint b)
        {
            var dx = a.X - b.X;
            var dy = a.Y - b.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }
    }
}