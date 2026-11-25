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
            int registerWins = 0;
            int stopWins = 0;

            for (int i = 0; i < iterations; i++)
            {
                var owner = new MonitoringLifecycleOwner();
                var lifecycle = new FakeLifecycle();
                var startBarrier = new Barrier(2);
                var registerDone = new ManualResetEventSlim(false);
                var stopDone = new ManualResetEventSlim(false);
                bool? registerResult = null;
                IGpuProviderLifecycle? stopResult = null;

                var registerTask = Task.Run(() =>
                {
                    startBarrier.SignalAndWait(TimeSpan.FromSeconds(5));
                    registerResult = owner.TryRegister(lifecycle);
                    registerDone.Set();
                });

                var stopTask = Task.Run(() =>
                {
                    startBarrier.SignalAndWait(TimeSpan.FromSeconds(5));
                    stopResult = owner.StopAndTake();
                    stopDone.Set();
                });

                bool registerCompleted = registerDone.Wait(TimeSpan.FromSeconds(5));
                bool stopCompleted = stopDone.Wait(TimeSpan.FromSeconds(5));
                Assert.True(registerCompleted, "Register task timed out");
                Assert.True(stopCompleted, "Stop task timed out");

                registerTask.Wait(TimeSpan.FromSeconds(5));
                stopTask.Wait(TimeSpan.FromSeconds(5));

                bool registerSuccess = registerResult == true;
                bool stopSuccess = stopResult != null;

                Assert.True(registerSuccess ^ stopSuccess, "Exactly one should succeed");

                if (registerSuccess)
                {
                    registerWins++;
                    var cleanup = owner.StopAndTake();
                    Assert.Same(lifecycle, cleanup);
                }
                else
                {
                    stopWins++;
                    bool lateRegister = owner.TryRegister(lifecycle);
                    Assert.False(lateRegister);
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
        public void TryRegister_MultipleBeforeStop_OnlyLastStored()
        {
            var owner = new MonitoringLifecycleOwner();
            var first = new FakeLifecycle();
            var second = new FakeLifecycle();
            var third = new FakeLifecycle();

            owner.TryRegister(first);
            owner.TryRegister(second);
            owner.TryRegister(third);

            var taken = owner.StopAndTake();
            Assert.Same(third, taken);
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

        [Fact]
        public void ConcurrentRegisterAndStop_WithMultipleRegisters_ExactlyOneGetsProvider()
        {
            const int iterations = 500;
            int registerWins = 0;
            int stopWins = 0;

            for (int i = 0; i < iterations; i++)
            {
                var owner = new MonitoringLifecycleOwner();
                var lifecycle = new FakeLifecycle();
                var startBarrier = new Barrier(3);
                var registerDone = new ManualResetEventSlim(false);
                var stopDone = new ManualResetEventSlim(false);
                var lateRegisterDone = new ManualResetEventSlim(false);
                bool? earlyRegisterResult = null;
                IGpuProviderLifecycle? stopResult = null;
                bool? lateRegisterResult = null;

                var earlyRegisterTask = Task.Run(() =>
                {
                    startBarrier.SignalAndWait(TimeSpan.FromSeconds(5));
                    earlyRegisterResult = owner.TryRegister(lifecycle);
                    registerDone.Set();
                });

                var stopTask = Task.Run(() =>
                {
                    startBarrier.SignalAndWait(TimeSpan.FromSeconds(5));
                    stopResult = owner.StopAndTake();
                    stopDone.Set();
                });

                var lateRegisterTask = Task.Run(() =>
                {
                    startBarrier.SignalAndWait(TimeSpan.FromSeconds(5));
                    lateRegisterResult = owner.TryRegister(new FakeLifecycle());
                    lateRegisterDone.Set();
                });

                bool earlyRegisterCompleted = registerDone.Wait(TimeSpan.FromSeconds(5));
                bool stopCompleted = stopDone.Wait(TimeSpan.FromSeconds(5));
                bool lateRegisterCompleted = lateRegisterDone.Wait(TimeSpan.FromSeconds(5));
                Assert.True(earlyRegisterCompleted, "Early register task timed out");
                Assert.True(stopCompleted, "Stop task timed out");
                Assert.True(lateRegisterCompleted, "Late register task timed out");

                earlyRegisterTask.Wait(TimeSpan.FromSeconds(5));
                stopTask.Wait(TimeSpan.FromSeconds(5));
                lateRegisterTask.Wait(TimeSpan.FromSeconds(5));

                bool earlyRegisterSuccess = earlyRegisterResult == true;
                bool stopSuccess = stopResult != null;
                bool lateRegisterSuccess = lateRegisterResult == true;

                Assert.True(earlyRegisterSuccess ^ stopSuccess, "Exactly one of early register or stop should succeed");
                Assert.False(lateRegisterSuccess, "Late register should always fail after stop");

                if (earlyRegisterSuccess)
                {
                    registerWins++;
                    var cleanup = owner.StopAndTake();
                    Assert.Same(lifecycle, cleanup);
                }
                else
                {
                    stopWins++;
                }
            }
        }
    }
}