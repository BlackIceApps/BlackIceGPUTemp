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

        private class BlockingSessionFactory : IGpuMonitoringSessionFactory
        {
            private readonly ManualResetEventSlim _startSignal;
            private readonly ManualResetEventSlim _proceedSignal;
            private readonly GpuMonitoringInitializationResult _result;
            public int CallCount { get; private set; }
            public bool FactoryStarted { get; private set; }
            public bool FactoryCompleted { get; private set; }

            public BlockingSessionFactory(ManualResetEventSlim startSignal, ManualResetEventSlim proceedSignal, GpuMonitoringInitializationResult result)
            {
                _startSignal = startSignal;
                _proceedSignal = proceedSignal;
                _result = result;
            }

            public GpuMonitoringInitializationResult Create(CancellationToken cancellationToken)
            {
                CallCount++;
                FactoryStarted = true;
                _startSignal.Set();
                _proceedSignal.Wait(cancellationToken);
                FactoryCompleted = true;
                return _result;
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

        [Fact]
        public async Task ParallelAttemptRejected_FactoryCalledOnce_SecondCallReturnsImmediately()
        {
            var startSignal = new ManualResetEventSlim(false);
            var proceedSignal = new ManualResetEventSlim(false);
            var factory = new BlockingSessionFactory(startSignal, proceedSignal, CreateSuccessResult());
            var coordinator = new GpuInitializationCoordinator(factory);
            var attemptStartCount = 0;
            var callbackCount = 0;

            var firstTask = coordinator.TryInitializeAsync(
                CancellationToken.None,
                onAttemptStart: () => attemptStartCount++,
                onResult: (result, _) =>
                {
                    callbackCount++;
                    return Task.CompletedTask;
                });

            try
            {
                Assert.True(startSignal.Wait(TimeSpan.FromSeconds(5)), "Factory did not start within timeout");

                var secondTask = coordinator.TryInitializeAsync(
                    CancellationToken.None,
                    onAttemptStart: () => attemptStartCount++,
                    onResult: (result, _) =>
                    {
                        callbackCount++;
                        return Task.CompletedTask;
                    });

                Assert.True(secondTask.IsCompleted, "Second call should complete immediately when rejected");
            }
            finally
            {
                proceedSignal.Set();
            }

            var completedTask = await Task.WhenAny(firstTask, Task.Delay(TimeSpan.FromSeconds(5)));
            Assert.Same(firstTask, completedTask);
            await firstTask;

            Assert.Equal(1, factory.CallCount);
            Assert.Equal(1, attemptStartCount);
            Assert.Equal(1, callbackCount);
            Assert.True(factory.FactoryStarted);
            Assert.True(factory.FactoryCompleted);
        }

        [Fact]
        public async Task FactoryRunsOffCallerThread_CallerThreadNotEqualFactoryThread()
        {
            int callerThreadId = 0;
            int factoryThreadId = 0;
            var factory = new TestFactoryWithThreadCapture(r => factoryThreadId = r);
            var coordinator = new GpuInitializationCoordinator(factory);
            var taskTcs = new TaskCompletionSource<Task>();

            var thread = new Thread(() =>
            {
                callerThreadId = Thread.CurrentThread.ManagedThreadId;
                try
                {
                    var task = coordinator.TryInitializeAsync(
                        CancellationToken.None,
                        onAttemptStart: () => { },
                        onResult: (result, _) =>
                        {
                            return Task.CompletedTask;
                        });
                    taskTcs.TrySetResult(task);
                }
                catch (Exception ex)
                {
                    taskTcs.TrySetException(ex);
                }
            });

            thread.Start();
            var task = await Task.WhenAny(taskTcs.Task, Task.Delay(TimeSpan.FromSeconds(10)));
            Assert.Same(taskTcs.Task, task);
            task = await taskTcs.Task;

            var completed = await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(10)));
            Assert.Same(task, completed);
            await task;

            Assert.True(thread.Join(TimeSpan.FromSeconds(5)), "Test thread did not finish within timeout");

            Assert.NotEqual(callerThreadId, factoryThreadId);
        }

        private class TestFactoryWithThreadCapture : IGpuMonitoringSessionFactory
        {
            private readonly Action<int> _captureThreadId;

            public TestFactoryWithThreadCapture(Action<int> captureThreadId)
            {
                _captureThreadId = captureThreadId;
            }

            public GpuMonitoringInitializationResult Create(CancellationToken cancellationToken)
            {
                _captureThreadId(Thread.CurrentThread.ManagedThreadId);
                return CreateSuccessResult();
            }
        }
    }
}