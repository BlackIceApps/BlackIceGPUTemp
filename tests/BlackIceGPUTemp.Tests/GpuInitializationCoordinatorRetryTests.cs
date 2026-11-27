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

        private class SuccessResult : GpuMonitoringInitializationResult
        {
            public SuccessResult()
                : base(new HardwareMonitorService(null), "provider-1", "device-1", "Test GPU")
            {
            }
        }

        private class FailureResult : GpuMonitoringInitializationResult
        {
            public FailureResult(string message = "Initialization failed")
                : base(message)
            {
            }
        }

        [Fact]
        public async Task FailureFollowedBySuccess_CallbackInvokedTwice_FactoryCalledTwice()
        {
            var factory = new FakeSessionFactory(new[]
            {
                new FailureResult("First attempt failed"),
                new SuccessResult()
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
                new FailureResult("First attempt failed"),
                new FailureResult("Second attempt failed")
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