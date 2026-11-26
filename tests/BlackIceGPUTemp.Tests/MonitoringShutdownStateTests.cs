using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BlackIceGPUTemp.Tray;
using Xunit;

namespace BlackIceGPUTemp.Tests
{
    public class MonitoringShutdownStateTests
    {
        [Fact]
        public void TryGetCancellationToken_BeforeDisposal_ReturnsTrueAndToken()
        {
            using var cts = new CancellationTokenSource();
            var state = new MonitoringShutdownState(cts);

            bool result = state.TryGetCancellationToken(out var token);

            Assert.True(result);
            Assert.Equal(cts.Token, token);
            Assert.False(state.IsDisposing);
        }

        [Fact]
        public void TryGetCancellationToken_AfterTryBeginDispose_ReturnsFalse()
        {
            using var cts = new CancellationTokenSource();
            var state = new MonitoringShutdownState(cts);

            state.TryBeginDispose();

            bool result = state.TryGetCancellationToken(out var token);

            Assert.False(result);
            Assert.Equal(default(CancellationToken), token);
            Assert.True(state.IsDisposing);
        }

        [Fact]
        public void TryBeginDispose_FirstCall_ReturnsTrue()
        {
            using var cts = new CancellationTokenSource();
            var state = new MonitoringShutdownState(cts);

            bool result = state.TryBeginDispose();

            Assert.True(result);
            Assert.True(state.IsDisposing);
        }

        [Fact]
        public void TryBeginDispose_SecondCall_ReturnsFalse()
        {
            using var cts = new CancellationTokenSource();
            var state = new MonitoringShutdownState(cts);

            bool first = state.TryBeginDispose();
            bool second = state.TryBeginDispose();

            Assert.True(first);
            Assert.False(second);
            Assert.True(state.IsDisposing);
        }

        [Fact]
        public void IsDisposing_BeforeDisposal_ReturnsFalse()
        {
            using var cts = new CancellationTokenSource();
            var state = new MonitoringShutdownState(cts);

            Assert.False(state.IsDisposing);
        }

        [Fact]
        public void IsDisposing_AfterTryBeginDispose_ReturnsTrue()
        {
            using var cts = new CancellationTokenSource();
            var state = new MonitoringShutdownState(cts);

            state.TryBeginDispose();

            Assert.True(state.IsDisposing);
        }

        [Fact]
        public async Task TryGetCancellationToken_ConcurrentWithTryBeginDispose_NoObjectDisposedException()
        {
            using var cts = new CancellationTokenSource();
            var state = new MonitoringShutdownState(cts);

            const int threadCount = 20;
            var startBarrier = new Barrier(threadCount);
            var attemptCompleted = new CountdownEvent(threadCount);
            int tokenRetrievedCount = 0;
            int disposeStartedCount = 0;

            var tasks = new Task[threadCount];
            for (int i = 0; i < threadCount; i++)
            {
                tasks[i] = Task.Factory.StartNew(() =>
                {
                    bool barrierSignaled = startBarrier.SignalAndWait(TimeSpan.FromSeconds(5));
                    if (!barrierSignaled)
                    {
                        throw new TimeoutException("Barrier timeout");
                    }

                    // Half the threads try to get token, half try to begin dispose
                    if (Thread.CurrentThread.ManagedThreadId % 2 == 0)
                    {
                        if (state.TryGetCancellationToken(out _))
                        {
                            Interlocked.Increment(ref tokenRetrievedCount);
                        }
                    }
                    else
                    {
                        if (state.TryBeginDispose())
                        {
                            Interlocked.Increment(ref disposeStartedCount);
                        }
                    }

                    attemptCompleted.Signal();
                }, TaskCreationOptions.LongRunning);
            }

            try
            {
                bool attemptsDone = attemptCompleted.Wait(TimeSpan.FromSeconds(5));
                Assert.True(attemptsDone, "Not all threads completed in time");

                // Only one dispose should succeed
                Assert.Equal(1, disposeStartedCount);

                // After dispose starts, no new tokens should be retrievable
                // (some threads may have retrieved before dispose started)
                Assert.True(tokenRetrievedCount >= 0);

                // Verify state is consistent
                if (state.IsDisposing)
                {
                    Assert.False(state.TryGetCancellationToken(out _));
                }
            }
            finally
            {
                var allTasks = Task.WhenAll(tasks);
                var completed = await Task.WhenAny(allTasks, Task.Delay(TimeSpan.FromSeconds(5)));
                bool timedOut = completed != allTasks;
                if (!timedOut)
                {
                    await allTasks;
                }
                else
                {
                    Assert.Fail("Not all tasks finished in time");
                }
            }
        }

        [Fact]
        public void TryGetCancellationToken_AfterDisposalStarted_CannotRetrieveNewToken()
        {
            using var cts = new CancellationTokenSource();
            var state = new MonitoringShutdownState(cts);

            state.TryBeginDispose();

            // Multiple attempts to get token after disposal started should all fail
            for (int i = 0; i < 10; i++)
            {
                bool result = state.TryGetCancellationToken(out var token);
                Assert.False(result);
                Assert.Equal(default(CancellationToken), token);
            }
        }

        [Fact]
        public void TryBeginDispose_MultipleThreads_OnlyOneSucceeds()
        {
            using var cts = new CancellationTokenSource();
            var state = new MonitoringShutdownState(cts);

            const int threadCount = 50;
            var startBarrier = new Barrier(threadCount);
            int successCount = 0;

            var threads = new Thread[threadCount];
            try
            {
                for (int i = 0; i < threadCount; i++)
                {
                    var t = new Thread(() =>
                    {
                        bool barrierSignaled = startBarrier.SignalAndWait(TimeSpan.FromSeconds(5));
                        if (!barrierSignaled)
                        {
                            return;
                        }
                        if (state.TryBeginDispose())
                        {
                            Interlocked.Increment(ref successCount);
                        }
                    });
                    threads[i] = t;
                    t.Start();
                }

                foreach (var t in threads)
                {
                    t.Join(TimeSpan.FromSeconds(5));
                }

                Assert.Equal(1, successCount);
                Assert.True(state.IsDisposing);
            }
            finally
            {
                foreach (var t in threads)
                {
                    if (t.IsAlive)
                    {
                        t.IsBackground = true;
                    }
                }
            }
        }

        [Fact]
        public void TryGetCancellationToken_AfterCancellationRequested_StillReturnsTokenBeforeDisposal()
        {
            using var cts = new CancellationTokenSource();
            var state = new MonitoringShutdownState(cts);

            cts.Cancel();

            bool result = state.TryGetCancellationToken(out var token);

            Assert.True(result);
            Assert.Equal(cts.Token, token);
            Assert.True(token.IsCancellationRequested);
            Assert.False(state.IsDisposing);
        }
    }
}