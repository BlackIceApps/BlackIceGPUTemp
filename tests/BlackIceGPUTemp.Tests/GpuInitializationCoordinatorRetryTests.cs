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

        private class ThrowingSessionFactory : IGpuMonitoringSessionFactory
        {
            private readonly Exception _exceptionToThrow;
            private readonly GpuMonitoringInitializationResult _resultAfterThrow;
            private bool _hasThrown = false;
            public int CallCount { get; private set; }

            public ThrowingSessionFactory(Exception exceptionToThrow, GpuMonitoringInitializationResult resultAfterThrow)
            {
                _exceptionToThrow = exceptionToThrow;
                _resultAfterThrow = resultAfterThrow;
            }

            public GpuMonitoringInitializationResult Create(CancellationToken cancellationToken)
            {
                CallCount++;
                if (!_hasThrown)
                {
                    _hasThrown = true;
                    throw _exceptionToThrow;
                }
                return _resultAfterThrow;
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

        private class CancellingThenSuccessFactory : IGpuMonitoringSessionFactory
        {
            private readonly CancellationTokenSource _cts;
            private readonly GpuMonitoringInitializationResult _successResult;
            private bool _firstCall = true;
            public int CallCount { get; private set; }

            public CancellingThenSuccessFactory(CancellationTokenSource cts, GpuMonitoringInitializationResult successResult)
            {
                _cts = cts;
                _successResult = successResult;
            }

            public GpuMonitoringInitializationResult Create(CancellationToken cancellationToken)
            {
                CallCount++;
                if (_firstCall)
                {
                    _firstCall = false;
                    _cts.Cancel();
                }
                return _successResult;
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

        private class FakeLifecycle : IGpuProviderLifecycle
        {
            public int DisconnectCallCount { get; private set; }

            public bool Connect(out string? error)
            {
                error = null;
                return true;
            }

            public void Disconnect()
            {
                DisconnectCallCount++;
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
        public async Task FactoryException_IsReportedAsFailure_AndNextAttemptIsAllowed()
        {
            var exception = new InvalidOperationException("Factory error");
            var factory = new ThrowingSessionFactory(exception, CreateSuccessResult());
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
            Assert.Equal("Initialization error", callbackResults[0].Message);
            Assert.True(callbackResults[1].IsSuccess);
        }

        [Fact]
        public async Task CallbackException_Propagates_AndReleasesInitializationGate()
        {
            var factory = new FakeSessionFactory(new[]
            {
                CreateSuccessResult(),
                CreateSuccessResult()
            });
            var coordinator = new GpuInitializationCoordinator(factory);
            var callbackCount = 0;

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                await coordinator.TryInitializeAsync(
                    CancellationToken.None,
                    onAttemptStart: () => { },
                    onResult: (result, _) =>
                    {
                        callbackCount++;
                        throw new InvalidOperationException("Callback error");
                    });
            });

            Assert.Equal("Callback error", ex.Message);
            Assert.Equal(1, callbackCount);

            var secondCallbackResults = new List<GpuMonitoringInitializationResult>();
            await coordinator.TryInitializeAsync(
                CancellationToken.None,
                onAttemptStart: () => { },
                onResult: (result, _) =>
                {
                    secondCallbackResults.Add(result);
                    return Task.CompletedTask;
                });

            Assert.Equal(2, factory.CallCount);
            Assert.Single(secondCallbackResults);
            Assert.True(secondCallbackResults[0].IsSuccess);
        }

        [Fact]
        public async Task CancellationAfterFactorySuccess_DisconnectsLifecycle_AndSkipsCallback()
        {
            var cts = new CancellationTokenSource();
            var lifecycle = new FakeLifecycle();
            var fakeDevice = new FakeDevice("device-1", "Test GPU");
            var monitorService = new HardwareMonitorService(fakeDevice);
            var successResult = new GpuMonitoringInitializationResult(monitorService, "provider-1", "device-1", "Test GPU", lifecycle);
            var factory = new CancellingThenSuccessFactory(cts, successResult);
            var coordinator = new GpuInitializationCoordinator(factory);
            var callbackCount = 0;

            await coordinator.TryInitializeAsync(
                cts.Token,
                onAttemptStart: () => { },
                onResult: (result, _) =>
                {
                    callbackCount++;
                    return Task.CompletedTask;
                });

            Assert.Equal(1, factory.CallCount);
            Assert.Equal(1, lifecycle.DisconnectCallCount);
            Assert.Equal(0, callbackCount);

            var secondCallbackResults = new List<GpuMonitoringInitializationResult>();
            await coordinator.TryInitializeAsync(
                CancellationToken.None,
                onAttemptStart: () => { },
                onResult: (result, _) =>
                {
                    secondCallbackResults.Add(result);
                    return Task.CompletedTask;
                });

            Assert.Equal(2, factory.CallCount);
            Assert.Single(secondCallbackResults);
            Assert.True(secondCallbackResults[0].IsSuccess);
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