using System;
using System.Threading;
using System.Threading.Tasks;
using BlackIceGPUTemp.Monitoring.Abstractions;
using BlackIceGPUTemp.Tray;
using Xunit;

namespace BlackIceGPUTemp.Tests
{
    public class MonitoringLifecycleOwnerTests
    {
        private sealed class FakeLifecycle : IGpuProviderLifecycle
        {
            public int ConnectCallCount { get; private set; }
            public int DisconnectCallCount { get; private set; }
            public bool ConnectResult { get; set; } = true;
            public string? ConnectError { get; set; }

            public bool Connect(out string? error)
            {
                ConnectCallCount++;
                error = ConnectError;
                return ConnectResult;
            }

            public void Disconnect()
            {
                DisconnectCallCount++;
            }
        }

        [Fact]
        public void TryRegister_ThenStopAndTake_ReturnsProviderExactlyOnce()
        {
            var owner = new MonitoringLifecycleOwner();
            var lifecycle = new FakeLifecycle();

            bool registered = owner.TryRegister(lifecycle);
            Assert.True(registered);

            var taken = owner.StopAndTake();
            Assert.Same(lifecycle, taken);

            var takenAgain = owner.StopAndTake();
            Assert.Null(takenAgain);
        }

        [Fact]
        public void StopAndTake_BeforeRegister_LaterRegisterFails()
        {
            var owner = new MonitoringLifecycleOwner();

            var taken = owner.StopAndTake();
            Assert.Null(taken);

            var lifecycle = new FakeLifecycle();
            bool registered = owner.TryRegister(lifecycle);
            Assert.False(registered);
        }

        [Fact]
        public void MultipleStopAndTake_OnlyFirstReturnsProvider()
        {
            var owner = new MonitoringLifecycleOwner();
            var lifecycle = new FakeLifecycle();

            owner.TryRegister(lifecycle);

            var first = owner.StopAndTake();
            var second = owner.StopAndTake();
            var third = owner.StopAndTake();

            Assert.Same(lifecycle, first);
            Assert.Null(second);
            Assert.Null(third);
        }

        [Fact]
        public void TryRegister_Null_DoesNotThrow_AndDoesNotCaptureResource()
        {
            var owner = new MonitoringLifecycleOwner();

            bool registered = owner.TryRegister(null);
            Assert.True(registered);

            var taken = owner.StopAndTake();
            Assert.Null(taken);
        }

        [Fact]
        public void Race_RegisterVsStopAndTake_ExactlyOneSucceeds()
        {
            const int iterations = 1000;

            var registerResults = new bool?[iterations];
            var stopResults = new IGpuProviderLifecycle?[iterations];
            var registerExceptions = new Exception?[iterations];
            var stopExceptions = new Exception?[iterations];

            var startBarrier = new Barrier(2);
            var endBarrier = new Barrier(2);
            var owners = new MonitoringLifecycleOwner[iterations];
            var lifecycles = new FakeLifecycle[iterations];
            int stopRequested = 0;

            Thread? registerThread = null;
            Thread? stopThread = null;

            try
            {
                registerThread = new Thread(() =>
                {
                    for (int i = 0; i < iterations; i++)
                    {
                        bool startSync;
                        try
                        {
                            startSync = startBarrier.SignalAndWait(TimeSpan.FromSeconds(5));
                        }
                        catch (BarrierPostPhaseException)
                        {
                            break; // Main thread broke the barrier
                        }

                        if (!startSync || Volatile.Read(ref stopRequested) != 0)
                        {
                            break;
                        }

                        bool hadException = false;
                        try
                        {
                            registerResults[i] = owners[i].TryRegister(lifecycles[i]);
                        }
                        catch (Exception ex)
                        {
                            registerExceptions[i] = ex;
                            hadException = true;
                        }

                        try
                        {
                            endBarrier.SignalAndWait(TimeSpan.FromSeconds(5));
                        }
                        catch (BarrierPostPhaseException)
                        {
                            break;
                        }

                        if (hadException)
                        {
                            Interlocked.Exchange(ref stopRequested, 1);
                        }

                        if (Volatile.Read(ref stopRequested) != 0 || hadException)
                        {
                            break;
                        }
                    }
                });

                stopThread = new Thread(() =>
                {
                    for (int i = 0; i < iterations; i++)
                    {
                        bool startSync;
                        try
                        {
                            startSync = startBarrier.SignalAndWait(TimeSpan.FromSeconds(5));
                        }
                        catch (BarrierPostPhaseException)
                        {
                            break; // Main thread broke the barrier
                        }

                        if (!startSync || Volatile.Read(ref stopRequested) != 0)
                        {
                            break;
                        }

                        bool hadException = false;
                        try
                        {
                            stopResults[i] = owners[i].StopAndTake();
                        }
                        catch (Exception ex)
                        {
                            stopExceptions[i] = ex;
                            hadException = true;
                        }

                        try
                        {
                            endBarrier.SignalAndWait(TimeSpan.FromSeconds(5));
                        }
                        catch (BarrierPostPhaseException)
                        {
                            break;
                        }

                        if (hadException)
                        {
                            Interlocked.Exchange(ref stopRequested, 1);
                        }

                        if (Volatile.Read(ref stopRequested) != 0 || hadException)
                        {
                            break;
                        }
                    }
                });

                registerThread.IsBackground = true;
                stopThread.IsBackground = true;
                registerThread.Start();
                stopThread.Start();

                for (int i = 0; i < iterations; i++)
                {
                    owners[i] = new MonitoringLifecycleOwner();
                    lifecycles[i] = new FakeLifecycle();

                    bool startSync = startBarrier.SignalAndWait(TimeSpan.FromSeconds(5));
                    if (!startSync)
                    {
                        Interlocked.Exchange(ref stopRequested, 1);
                        throw new InvalidOperationException($"Start barrier timed out at iteration {i}");
                    }

                    bool endSync = endBarrier.SignalAndWait(TimeSpan.FromSeconds(5));
                    if (!endSync)
                    {
                        Interlocked.Exchange(ref stopRequested, 1);
                        throw new InvalidOperationException($"End barrier timed out at iteration {i}");
                    }
                }

                bool registerThreadDone = registerThread.Join(TimeSpan.FromSeconds(30));
                bool stopThreadDone = stopThread.Join(TimeSpan.FromSeconds(30));

                Assert.True(registerThreadDone, "Register thread did not complete in time");
                Assert.True(stopThreadDone, "Stop thread did not complete in time");

                for (int i = 0; i < iterations; i++)
                {
                    if (registerExceptions[i] != null)
                    {
                        throw new InvalidOperationException($"Register thread exception at iteration {i}", registerExceptions[i]);
                    }
                    if (stopExceptions[i] != null)
                    {
                        throw new InvalidOperationException($"Stop thread exception at iteration {i}", stopExceptions[i]);
                    }

                    bool registerSuccess = registerResults[i] == true;

                    if (registerSuccess)
                    {
                        // Registration won - StopAndTake must return the same provider
                        Assert.Same(lifecycles[i], stopResults[i]);
                        var cleanup = owners[i].StopAndTake();
                        Assert.Null(cleanup);
                    }
                    else
                    {
                        // Stop won - registration failed, StopAndTake returned null
                        Assert.Null(stopResults[i]);
                        bool lateRegister = owners[i].TryRegister(lifecycles[i]);
                        Assert.False(lateRegister);
                    }

                    // After race completes, any further registration must fail regardless of winner
                    bool lateRegisterResult = owners[i].TryRegister(new FakeLifecycle());
                    Assert.False(lateRegisterResult, $"Late register should always fail after stop at iteration {i}");
                }
            }
            finally
            {
                if (registerThread != null && registerThread.IsAlive)
                {
                    registerThread.Join(TimeSpan.FromSeconds(5));
                }
                if (stopThread != null && stopThread.IsAlive)
                {
                    stopThread.Join(TimeSpan.FromSeconds(5));
                }
            }
        }

        [Fact]
        public void ConcurrentStopAndTake_OnlyOneGetsProvider()
        {
            const int threadCount = 20;
            var owner = new MonitoringLifecycleOwner();
            var lifecycle = new FakeLifecycle();
            owner.TryRegister(lifecycle);

            var startBarrier = new Barrier(threadCount);
            var results = new IGpuProviderLifecycle?[threadCount];
            var tasks = new Task[threadCount];

            for (int i = 0; i < threadCount; i++)
            {
                int index = i;
                tasks[i] = Task.Run(() =>
                {
                    startBarrier.SignalAndWait(TimeSpan.FromSeconds(5));
                    results[index] = owner.StopAndTake();
                });
            }

            Task.WaitAll(tasks, TimeSpan.FromSeconds(10));

            int nonNullCount = 0;
            for (int i = 0; i < threadCount; i++)
            {
                if (results[i] != null)
                {
                    nonNullCount++;
                    Assert.Same(lifecycle, results[i]);
                }
            }

            Assert.Equal(1, nonNullCount);
        }

        [Fact]
        public void TryRegister_AfterStopAndTake_ReturnsFalse()
        {
            var owner = new MonitoringLifecycleOwner();

            owner.StopAndTake();

            var lifecycle = new FakeLifecycle();
            bool registered = owner.TryRegister(lifecycle);

            Assert.False(registered);
        }

        [Fact]
        public void TryRegister_SecondRegistration_ReturnsFalseAndPreservesFirstProvider()
        {
            var owner = new MonitoringLifecycleOwner();
            var first = new FakeLifecycle();
            var second = new FakeLifecycle();

            bool firstResult = owner.TryRegister(first);
            bool secondResult = owner.TryRegister(second);

            Assert.True(firstResult);
            Assert.False(secondResult);

            var taken = owner.StopAndTake();
            Assert.Same(first, taken);
        }

        [Fact]
        public void TryRegister_FirstNull_SecondProvider_SecondRejected()
        {
            var owner = new MonitoringLifecycleOwner();
            var provider = new FakeLifecycle();

            bool firstResult = owner.TryRegister(null);
            bool secondResult = owner.TryRegister(provider);

            Assert.True(firstResult);
            Assert.False(secondResult);

            var taken = owner.StopAndTake();
            Assert.Null(taken);
        }

        [Fact]
        public void StopAndTake_WithoutRegister_ReturnsNull()
        {
            var owner = new MonitoringLifecycleOwner();

            var result = owner.StopAndTake();

            Assert.Null(result);
        }

        [Fact]
        public void Register_Null_ThenStopAndTake_ReturnsNull()
        {
            var owner = new MonitoringLifecycleOwner();

            owner.TryRegister(null);

            var result = owner.StopAndTake();

            Assert.Null(result);
        }
    }
}