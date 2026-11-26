using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BlackIceGPUTemp.Tray;
using Xunit;

namespace BlackIceGPUTemp.Tests
{
    public class MonitoringReadGateTests
    {
        [Fact]
        public void TryBeginRead_FirstCall_ReturnsTrue()
        {
            var gate = new MonitoringReadGate();

            bool result = gate.TryBeginRead();

            Assert.True(result);
        }

        [Fact]
        public void TryBeginRead_SecondCallDuringActiveRead_ReturnsFalse()
        {
            var gate = new MonitoringReadGate();

            bool first = gate.TryBeginRead();
            bool second = gate.TryBeginRead();

            Assert.True(first);
            Assert.False(second);
        }

        [Fact]
        public void TryBeginRead_AfterEndRead_ReturnsTrue()
        {
            var gate = new MonitoringReadGate();

            gate.TryBeginRead();
            gate.EndRead();
            bool result = gate.TryBeginRead();

            Assert.True(result);
        }

        [Fact]
        public void TryBeginRead_AfterStop_ReturnsFalse()
        {
            var gate = new MonitoringReadGate();

            gate.Stop();
            bool result = gate.TryBeginRead();

            Assert.False(result);
        }

        [Fact]
        public void Stop_DuringActiveRead_DoesNotThrow_AndBlocksSubsequentReads()
        {
            var gate = new MonitoringReadGate();

            gate.TryBeginRead();
            gate.Stop();
            gate.EndRead();

            bool result = gate.TryBeginRead();

            Assert.False(result);
        }

        [Fact]
        public async Task TryBeginRead_ConcurrentCalls_OnlyOneSucceeds()
        {
            var gate = new MonitoringReadGate();
            const int threadCount = 10;
            var startBarrier = new Barrier(threadCount);
            var releaseEvent = new ManualResetEventSlim(false);
            var attemptCompletedEvent = new CountdownEvent(threadCount);
            int successCount = 0;

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
                    if (gate.TryBeginRead())
                    {
                        Interlocked.Increment(ref successCount);
                        attemptCompletedEvent.Signal();
                        try
                        {
                            bool released = releaseEvent.Wait(TimeSpan.FromSeconds(5));
                            if (!released)
                            {
                                throw new TimeoutException("Release event timeout");
                            }
                        }
                        finally
                        {
                            gate.EndRead();
                        }
                    }
                    else
                    {
                        attemptCompletedEvent.Signal();
                    }
                }, TaskCreationOptions.LongRunning);
            }

            try
            {
                bool attemptsDone = attemptCompletedEvent.Wait(TimeSpan.FromSeconds(5));
                Assert.True(attemptsDone, "Not all threads completed TryBeginRead attempt in time");

                Assert.Equal(1, successCount);
            }
            finally
            {
                releaseEvent.Set();

                var allTasks = Task.WhenAll(tasks);
                var completed = await Task.WhenAny(allTasks, Task.Delay(TimeSpan.FromSeconds(5)));
                bool timedOut = completed != allTasks;
                if (!timedOut)
                {
                    // All tasks completed within timeout - await to propagate exceptions
                    await allTasks;
                }
                else
                {
                    // Timeout: tasks did not complete in time.
                    // Give them a brief additional moment after releaseEvent.Set(),
                    // but do not wait indefinitely.
                    var completed2 = await Task.WhenAny(allTasks, Task.Delay(TimeSpan.FromSeconds(1)));
                    if (completed2 == allTasks)
                    {
                        // All tasks finished during the additional wait - propagate exceptions
                        await allTasks;
                    }
                    else
                    {
                        // Still not done - report timeout
                        Assert.Fail("Not all tasks finished in time");
                    }
                }
            }
        }

        [Fact]
        public void TryBeginRead_AfterStop_AlwaysReturnsFalse()
        {
            var gate = new MonitoringReadGate();

            gate.Stop();

            const int threadCount = 20;
            var startBarrier = new Barrier(threadCount);
            int successCount = 0;

            var threads = new Thread[threadCount];
            var started = new bool[threadCount];
            try
            {
                for (int i = 0; i < threadCount; i++)
                {
                    var t = new Thread(() =>
                    {
                        bool barrierSignaled = startBarrier.SignalAndWait(TimeSpan.FromSeconds(5));
                        if (!barrierSignaled)
                        {
                            return; // Worker exits gracefully on barrier timeout
                        }
                        if (gate.TryBeginRead())
                        {
                            Interlocked.Increment(ref successCount);
                            gate.EndRead();
                        }
                    });
                    threads[i] = t;
                    t.Start();
                    started[i] = true;
                }

                // Wait for all threads to complete with timeout
                var timeout = TimeSpan.FromSeconds(5);
                var start = DateTime.UtcNow;
                bool allDone = false;
                while (DateTime.UtcNow - start < timeout)
                {
                    allDone = true;
                    foreach (var t in threads)
                    {
                        if (t != null && t.IsAlive)
                        {
                            allDone = false;
                            break;
                        }
                    }
                    if (allDone)
                    {
                        break;
                    }
                    Thread.Sleep(10);
                }

                Assert.True(allDone, "Not all threads finished in time");
                Assert.Equal(0, successCount);
            }
            finally
            {
                // Ensure all started threads are joined with timeout
                // Use a shared deadline for the entire cleanup group
                var cleanupDeadline = DateTime.UtcNow.AddSeconds(7);
                var failedThreads = new List<int>();

                for (int i = 0; i < threadCount; i++)
                {
                    var t = threads[i];
                    if (started[i] && t != null && t.IsAlive)
                    {
                        var remaining = cleanupDeadline - DateTime.UtcNow;
                        if (remaining <= TimeSpan.Zero)
                        {
                            remaining = TimeSpan.Zero;
                        }
                        bool joined = t.Join(remaining);
                        if (!joined)
                        {
                            failedThreads.Add(i);
                        }
                    }
                }

                // After attempting to join all threads, handle any that didn't finish
                foreach (var i in failedThreads)
                {
                    var t = threads[i];
                    try
                    {
                        if (!t.IsBackground)
                        {
                            t.IsBackground = true;
                        }
                    }
                    catch (ThreadStateException)
                    {
                        // Thread finished between Join and IsBackground check - ignore
                    }
                }

                if (failedThreads.Count > 0)
                {
                    Assert.Fail($"Threads {string.Join(", ", failedThreads)} did not finish within cleanup timeout");
                }
            }
        }

        [Fact]
        public void MultipleCycles_ReadEndReadRepeat_WorksCorrectly()
        {
            var gate = new MonitoringReadGate();

            for (int i = 0; i < 5; i++)
            {
                Assert.True(gate.TryBeginRead());
                gate.EndRead();
            }
        }

        [Fact]
        public void EndRead_WithoutBeginRead_DoesNotThrow()
        {
            var gate = new MonitoringReadGate();

            gate.EndRead();

            Assert.True(gate.TryBeginRead());
        }

        [Fact]
        public void WaitForIdle_NoActiveRead_ReturnsImmediately()
        {
            var gate = new MonitoringReadGate();

            gate.WaitForIdle();

            Assert.True(true); // If we reach here, WaitForIdle returned without blocking
        }

        [Fact]
        public void WaitForIdle_ActiveRead_WaitsUntilEndRead()
        {
            var gate = new MonitoringReadGate();
            var waitCompleted = new ManualResetEventSlim(false);
            var waitThreadStarted = new ManualResetEventSlim(false);

            // First, start an active read
            Assert.True(gate.TryBeginRead());

            // Then start the wait thread
            var waitThread = new Thread(() =>
            {
                waitThreadStarted.Set();
                gate.WaitForIdle();
                waitCompleted.Set();
            });

            waitThread.Start();
            waitThreadStarted.Wait(TimeSpan.FromSeconds(5));

            // WaitForIdle should not complete while read is active
            Assert.False(waitCompleted.Wait(TimeSpan.FromMilliseconds(100)));

            // End the read
            gate.EndRead();

            // WaitForIdle should now complete
            Assert.True(waitCompleted.Wait(TimeSpan.FromSeconds(5)));
            
            // Clean up thread
            bool joined = waitThread.Join(TimeSpan.FromSeconds(5));
            Assert.True(joined, "Wait thread did not finish in time");
        }

        [Fact]
        public void EndRead_PulsesWaitingThreads()
        {
            var gate = new MonitoringReadGate();
            var waitCompleted = new ManualResetEventSlim(false);
            var waitThreadStarted = new ManualResetEventSlim(false);

            // First, start an active read
            Assert.True(gate.TryBeginRead());

            // Then start the wait thread
            var waitThread = new Thread(() =>
            {
                waitThreadStarted.Set();
                gate.WaitForIdle();
                waitCompleted.Set();
            });

            waitThread.Start();
            waitThreadStarted.Wait(TimeSpan.FromSeconds(5));

            // WaitForIdle should not complete yet
            Assert.False(waitCompleted.Wait(TimeSpan.FromMilliseconds(100)));

            // End the read - this should pulse waiting threads
            gate.EndRead();

            // WaitForIdle should now complete
            Assert.True(waitCompleted.Wait(TimeSpan.FromSeconds(5)));
            
            // Clean up thread
            bool joined = waitThread.Join(TimeSpan.FromSeconds(5));
            Assert.True(joined, "Wait thread did not finish in time");
        }

        [Fact]
        public void Stop_BlocksNewReads_EvenDuringWaitForIdle()
        {
            var gate = new MonitoringReadGate();
            var waitCompleted = new ManualResetEventSlim(false);
            var waitThreadStarted = new ManualResetEventSlim(false);

            // First, start an active read
            Assert.True(gate.TryBeginRead());

            // Then start the wait thread
            var waitThread = new Thread(() =>
            {
                waitThreadStarted.Set();
                gate.WaitForIdle();
                waitCompleted.Set();
            });

            waitThread.Start();
            waitThreadStarted.Wait(TimeSpan.FromSeconds(5));

            // WaitForIdle should not complete while read is active
            Assert.False(waitCompleted.Wait(TimeSpan.FromMilliseconds(100)));

            // Stop the gate while WaitForIdle is waiting (read is still active)
            gate.Stop();

            // WaitForIdle should still wait for the active read to complete
            Assert.False(waitCompleted.Wait(TimeSpan.FromMilliseconds(100)));

            // End the active read
            gate.EndRead();

            // Now WaitForIdle should complete
            Assert.True(waitCompleted.Wait(TimeSpan.FromSeconds(5)));
            
            // Clean up thread
            bool joined = waitThread.Join(TimeSpan.FromSeconds(5));
            Assert.True(joined, "Wait thread did not finish in time");

            // New reads should be blocked after Stop
            Assert.False(gate.TryBeginRead());
        }

        [Fact]
        public void WaitForIdle_AfterReadCompletes_SubsequentReadsBlockedAfterStop()
        {
            var gate = new MonitoringReadGate();

            // Start and complete a read
            Assert.True(gate.TryBeginRead());
            gate.EndRead();

            // Stop the gate
            gate.Stop();

            // WaitForIdle should return immediately (no active read)
            gate.WaitForIdle();

            // New reads should still be blocked
            Assert.False(gate.TryBeginRead());
        }
    }
}