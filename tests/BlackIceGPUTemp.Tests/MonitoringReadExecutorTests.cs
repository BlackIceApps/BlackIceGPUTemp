using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BlackIceGPUTemp.Tray;
using Xunit;

namespace BlackIceGPUTemp.Tests
{
    public class MonitoringReadExecutorTests
    {
        [Fact]
        public void TryExecute_FirstOperation_Accepted()
        {
            var capturedActions = new List<Action>();
            var executor = new MonitoringReadExecutor(action => capturedActions.Add(action));

            bool accepted = executor.TryExecute(_ => { }, CancellationToken.None);

            Assert.True(accepted);
            Assert.Single(capturedActions);
        }

        [Fact]
        public void TryExecute_SecondOperationDuringFirst_Rejected()
        {
            var capturedActions = new List<Action>();
            var executor = new MonitoringReadExecutor(action => capturedActions.Add(action));

            bool first = executor.TryExecute(_ => { }, CancellationToken.None);
            bool second = executor.TryExecute(_ => { }, CancellationToken.None);

            Assert.True(first);
            Assert.False(second);
            Assert.Single(capturedActions);
        }

        [Fact]
        public void TryExecute_AfterFirstCompletes_NextAccepted()
        {
            var capturedActions = new List<Action>();
            var executor = new MonitoringReadExecutor(action => capturedActions.Add(action));

            bool first = executor.TryExecute(_ => { }, CancellationToken.None);
            Assert.True(first);

            capturedActions[0]();

            bool second = executor.TryExecute(_ => { }, CancellationToken.None);

            Assert.True(second);
            Assert.Equal(2, capturedActions.Count);
        }

        [Fact]
        public void TryExecute_AfterStop_Rejected()
        {
            var capturedActions = new List<Action>();
            var executor = new MonitoringReadExecutor(action => capturedActions.Add(action));

            executor.Stop();
            bool accepted = executor.TryExecute(_ => { }, CancellationToken.None);

            Assert.False(accepted);
            Assert.Empty(capturedActions);
        }

        [Fact]
        public void TryExecute_OperationThrows_GateReleased()
        {
            var capturedActions = new List<Action>();
            var executor = new MonitoringReadExecutor(action => capturedActions.Add(action));

            bool first = executor.TryExecute(_ => throw new InvalidOperationException("test"), CancellationToken.None);
            Assert.True(first);

            Assert.Throws<InvalidOperationException>(() => capturedActions[0]());

            bool second = executor.TryExecute(_ => { }, CancellationToken.None);

            Assert.True(second);
            Assert.Equal(2, capturedActions.Count);
        }

        [Fact]
        public void TryExecute_CancellationBeforeExecution_OperationNotRun_GateReleased()
        {
            var capturedActions = new List<Action>();
            var executor = new MonitoringReadExecutor(action => capturedActions.Add(action));
            var cts = new CancellationTokenSource();
            bool operationInvoked = false;

            bool first = executor.TryExecute(_ => { operationInvoked = true; }, cts.Token);
            Assert.True(first);

            cts.Cancel();
            capturedActions[0]();

            Assert.False(operationInvoked);

            bool second = executor.TryExecute(_ => { }, CancellationToken.None);

            Assert.True(second);
            Assert.Equal(2, capturedActions.Count);
        }

        [Fact]
        public void TryExecute_SchedulerThrowsSynchronously_GateReleased()
        {
            var capturedActions = new List<Action>();
            bool firstCall = true;

            var executor = new MonitoringReadExecutor(action =>
            {
                if (firstCall)
                {
                    firstCall = false;
                    throw new InvalidOperationException("scheduler failed");
                }
                else
                {
                    capturedActions.Add(action);
                }
            });

            Assert.Throws<InvalidOperationException>(() => executor.TryExecute(_ => { }, CancellationToken.None));

            bool accepted = executor.TryExecute(_ => { }, CancellationToken.None);

            Assert.True(accepted);
            Assert.Single(capturedActions);

            capturedActions[0]();

            bool third = executor.TryExecute(_ => { }, CancellationToken.None);

            Assert.True(third);
            Assert.Equal(2, capturedActions.Count);
        }

        [Fact]
        public void TryExecute_SchedulerExecutesSynchronouslyThenThrows_GateReleased()
        {
            var capturedActions = new List<Action>();
            bool firstCall = true;

            var executor = new MonitoringReadExecutor(action =>
            {
                if (firstCall)
                {
                    firstCall = false;
                    action();
                    throw new InvalidOperationException("scheduler failed after execution");
                }
                else
                {
                    capturedActions.Add(action);
                }
            });

            Assert.Throws<InvalidOperationException>(() => executor.TryExecute(_ => { }, CancellationToken.None));

            bool accepted = executor.TryExecute(_ => { }, CancellationToken.None);

            Assert.True(accepted);
            Assert.Single(capturedActions);

            capturedActions[0]();

            bool third = executor.TryExecute(_ => { }, CancellationToken.None);

            Assert.True(third);
            Assert.Equal(2, capturedActions.Count);
        }

        [Fact]
        public void TryExecute_MultipleSequentialReads_AllAccepted()
        {
            var capturedActions = new List<Action>();
            var executor = new MonitoringReadExecutor(action => capturedActions.Add(action));

            bool first = executor.TryExecute(_ => { }, CancellationToken.None);
            Assert.True(first);
            capturedActions[0]();

            bool second = executor.TryExecute(_ => { }, CancellationToken.None);
            Assert.True(second);
            capturedActions[1]();

            bool third = executor.TryExecute(_ => { }, CancellationToken.None);

            Assert.True(third);
            Assert.Equal(3, capturedActions.Count);
        }

        [Fact]
        public void WaitForIdle_NoActiveRead_ReturnsImmediately()
        {
            var executor = new MonitoringReadExecutor(action => Task.Run(action));

            executor.WaitForIdle();

            Assert.True(true); // If we reach here, WaitForIdle returned without blocking
        }

        [Fact]
        public void WaitForIdle_ActiveRead_WaitsUntilEndRead()
        {
            var capturedActions = new List<Action>();
            var executor = new MonitoringReadExecutor(action => capturedActions.Add(action));
            var waitCompleted = new ManualResetEventSlim(false);

            var waitThread = new Thread(() =>
            {
                executor.WaitForIdle();
                waitCompleted.Set();
            });

            waitThread.Start();

            // Start a read
            bool first = executor.TryExecute(_ => { }, CancellationToken.None);
            Assert.True(first);

            // WaitForIdle should not complete yet
            Assert.False(waitCompleted.Wait(TimeSpan.FromMilliseconds(100)));

            // Complete the read
            capturedActions[0]();

            // WaitForIdle should now complete
            Assert.True(waitCompleted.Wait(TimeSpan.FromSeconds(5)));
            waitThread.Join(TimeSpan.FromSeconds(5));
        }

        [Fact]
        public void WaitForIdle_AfterStop_BlocksNewReads()
        {
            var capturedActions = new List<Action>();
            var executor = new MonitoringReadExecutor(action => capturedActions.Add(action));

            // Start and complete a read
            bool first = executor.TryExecute(_ => { }, CancellationToken.None);
            Assert.True(first);
            capturedActions[0]();

            // Stop the executor
            executor.Stop();

            // WaitForIdle should return immediately (no active read)
            executor.WaitForIdle();

            // New reads should be blocked after Stop
            Assert.False(executor.TryExecute(_ => { }, CancellationToken.None));
        }

        [Fact]
        public void WaitForIdle_DuringActiveRead_CompletesAfterReadFinishes()
        {
            var capturedActions = new List<Action>();
            var executor = new MonitoringReadExecutor(action => capturedActions.Add(action));
            var waitCompleted = new ManualResetEventSlim(false);

            var waitThread = new Thread(() =>
            {
                executor.WaitForIdle();
                waitCompleted.Set();
            });

            waitThread.Start();

            // Start a read
            bool first = executor.TryExecute(_ => { }, CancellationToken.None);
            Assert.True(first);

            // WaitForIdle should not complete yet
            Assert.False(waitCompleted.Wait(TimeSpan.FromMilliseconds(100)));

            // Stop the executor (blocks new reads but doesn't affect active read)
            executor.Stop();

            // WaitForIdle should still wait for active read
            Assert.False(waitCompleted.Wait(TimeSpan.FromMilliseconds(100)));

            // Complete the active read
            capturedActions[0]();

            // WaitForIdle should now complete
            Assert.True(waitCompleted.Wait(TimeSpan.FromSeconds(5)));
            waitThread.Join(TimeSpan.FromSeconds(5));
        }
    }
}