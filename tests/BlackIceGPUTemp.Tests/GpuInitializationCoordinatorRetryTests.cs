using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BlackIceGPUTemp.Monitoring;
using BlackIceGPUTemp.Monitoring.Abstractions;
using BlackIceGPUTemp.Tray;
using Xunit;

namespace BlackIceGPUTemp.Tests
{
    public class GpuInitializationCoordinatorRetryTests
    {
        private class FakeSessionFactory : IGpuMonitoringSessionFactory
        {
            private readonly Queue<GpuMonitoringInitializationResult> _results;
            public int CallCount { get; private set; }

            public FakeSessionFactory(IEnumerable<GpuMonitoringInitializationResult> results)
            {
                _results = new Queue<GpuMonitoringInitializationResult>(results);
            }

            public GpuMonitoringInitializationResult Create(CancellationToken cancellationToken)
            {
                CallCount++;
                if (_results.Count > 0)
                {
                    return _results.Dequeue();
                }
                return new GpuMonitoringInitializationResult("No more results configured");
            }
        }

        private static GpuMonitoringInitializationResult CreateFailureResult(string message = "Initialization failed")
        {
            return new GpuMonitoringInitializationResult(message);
        }

        private static GpuMonitoringInitializationResult CreateSuccessResult()
        {
            var fakeDevice = new FakeDevice("device-1", "Test GPU");
            var monitorService = new HardwareMonitorService(fakeDevice);
            return new GpuMonitoringInitializationResult(monitorService, "provider-1", "device-1", "Test GPU");
        }

        private class FakeDevice : IGpuDevice
        {
            public string Id { get; }
            public string ProviderId => "provider-1";
            public string Name { get; }
            public GpuVendor Vendor => GpuVendor.Amd;
            public GpuKind Kind => GpuKind.Discrete;

            public FakeDevice(string id, string name)
            {
                Id = id;
                Name = name;
            }

            public bool TryReadTemperature(out TemperatureReading? reading, out string? error, CancellationToken cancellationToken = default)
            {
                reading = new TemperatureReading(50.0, "GPU", DateTimeOffset.UtcNow);
                error = null;
                return true;
            }
        }

        [Fact]
        public async Task FailureFollowedBySuccess_CallbackInvokedTwice_FactoryCalledTwice()
        {
            var factory = new FakeSessionFactory(new[]
            {
                CreateFailureResult("First attempt failed"),
                CreateSuccessResult()
            });

            var coordinator = new GpuInitializationCoordinator(factory);
            var callbackResults = new List<GpuMonitoringInitializationResult>();
            var attemptStartCount = 0;

            await coordinator.TryInitializeAsync(
                CancellationToken.None,
                onAttemptStart: () => attemptStartCount++,
                onResult: (result, _) =>
                {
                    callbackResults.Add(result);
                    return Task.CompletedTask;
                });

            await coordinator.TryInitializeAsync(
                CancellationToken.None,
                onAttemptStart: () => attemptStartCount++,
                onResult: (result, _) =>
                {
                    callbackResults.Add(result);
                    return Task.CompletedTask;
                });

            Assert.Equal(2, factory.CallCount);
            Assert.Equal(2, attemptStartCount);
            Assert.Equal(2, callbackResults.Count);
            Assert.False(callbackResults[0].IsSuccess);
            Assert.True(callbackResults[1].IsSuccess);
        }

        [Fact]
        public async Task ConsecutiveFailuresAllowSubsequentRetries_CallbackInvokedTwice_FactoryCalledTwice()
        {
            var factory = new FakeSessionFactory(new[]
            {
                CreateFailureResult("First attempt failed"),
                CreateFailureResult("Second attempt failed")
            });

            var coordinator = new GpuInitializationCoordinator(factory);
            var callbackResults = new List<GpuMonitoringInitializationResult>();
            var attemptStartCount = 0;

            await coordinator.TryInitializeAsync(
                CancellationToken.None,
                onAttemptStart: () => attemptStartCount++,
                onResult: (result, _) =>
                {
                    callbackResults.Add(result);
                    return Task.CompletedTask;
                });

            await coordinator.TryInitializeAsync(
                CancellationToken.None,
                onAttemptStart: () => attemptStartCount++,
                onResult: (result, _) =>
                {
                    callbackResults.Add(result);
                    return Task.CompletedTask;
                });

            Assert.Equal(2, factory.CallCount);
            Assert.Equal(2, attemptStartCount);
            Assert.Equal(2, callbackResults.Count);
            Assert.False(callbackResults[0].IsSuccess);
            Assert.False(callbackResults[1].IsSuccess);
        }
    }
}