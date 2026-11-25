using System;
using System.Collections.Generic;
using System.Threading;
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
            var executor = new MonitoringReadExecutor(_ => throw new InvalidOperationException("scheduler failed"));

            Assert.Throws<InvalidOperationException>(() => executor.TryExecute(_ => { }, CancellationToken.None));

            bool accepted = executor.TryExecute(_ => { }, CancellationToken.None);

            Assert.True(accepted);
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
    }
}