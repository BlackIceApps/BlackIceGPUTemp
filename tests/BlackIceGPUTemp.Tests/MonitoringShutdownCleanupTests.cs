using System;
using System.Collections.Generic;
using System.Threading;
using BlackIceGPUTemp.Monitoring.Abstractions;
using BlackIceGPUTemp.Tray;
using Xunit;

namespace BlackIceGPUTemp.Tests
{
    public class MonitoringShutdownCleanupTests
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
        public void Execute_AllStepsCalledInCorrectOrder()
        {
            var executionOrder = new List<int>();
            var lifecycle = new FakeLifecycle();

            var cleanup = new MonitoringShutdownCleanup(
                stopTimer: () => executionOrder.Add(1),
                stopAndTakeLifecycle: () =>
                {
                    executionOrder.Add(2);
                    return lifecycle;
                },
                stopExecutor: () => executionOrder.Add(3),
                cancelTokenSource: () => executionOrder.Add(4),
                waitForIdle: () => executionOrder.Add(5),
                disposeTokenSource: () => executionOrder.Add(6),
                disposeTimer: () => executionOrder.Add(7),
                hideAndDisposeNotifyIcon: () => executionOrder.Add(8),
                disposeCurrentIcon: () => executionOrder.Add(9),
                disposeUiDispatcher: () => executionOrder.Add(10));

            cleanup.Execute();

            Assert.Equal(new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11 }, executionOrder);
            Assert.Equal(1, lifecycle.DisconnectCallCount);
        }

        [Fact]
        public void Execute_StepThrows_RemainingStepsStillExecuted()
        {
            var executionOrder = new List<int>();
            var lifecycle = new FakeLifecycle();

            var cleanup = new MonitoringShutdownCleanup(
                stopTimer: () => { executionOrder.Add(1); throw new InvalidOperationException("step 1 failed"); },
                stopAndTakeLifecycle: () => { executionOrder.Add(2); return lifecycle; },
                stopExecutor: () => { executionOrder.Add(3); throw new InvalidOperationException("step 3 failed"); },
                cancelTokenSource: () => executionOrder.Add(4),
                waitForIdle: () => { executionOrder.Add(5); throw new InvalidOperationException("step 5 failed"); },
                disposeTokenSource: () => executionOrder.Add(6),
                disposeTimer: () => executionOrder.Add(7),
                hideAndDisposeNotifyIcon: () => executionOrder.Add(8),
                disposeCurrentIcon: () => executionOrder.Add(9),
                disposeUiDispatcher: () => executionOrder.Add(10));

            cleanup.Execute();

            Assert.Equal(new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }, executionOrder);
        }

        [Fact]
        public void Execute_LoggerThrows_DoesNotInterruptCleanup()
        {
            var executionOrder = new List<int>();
            var lifecycle = new FakeLifecycle();
            int logCallCount = 0;

            var cleanup = new MonitoringShutdownCleanup(
                stopTimer: () => { executionOrder.Add(1); throw new InvalidOperationException("step 1 failed"); },
                stopAndTakeLifecycle: () => { executionOrder.Add(2); return lifecycle; },
                stopExecutor: () => executionOrder.Add(3),
                cancelTokenSource: () => executionOrder.Add(4),
                waitForIdle: () => executionOrder.Add(5),
                disposeTokenSource: () => executionOrder.Add(6),
                disposeTimer: () => executionOrder.Add(7),
                hideAndDisposeNotifyIcon: () => executionOrder.Add(8),
                disposeCurrentIcon: () => executionOrder.Add(9),
                disposeUiDispatcher: () => executionOrder.Add(10));

            cleanup.Execute();

            Assert.Equal(new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }, executionOrder);
        }

        [Fact]
        public void Execute_StopAndTakeThrows_DoesNotAttemptDisconnect()
        {
            var executionOrder = new List<int>();
            var lifecycle = new FakeLifecycle();

            var cleanup = new MonitoringShutdownCleanup(
                stopTimer: () => executionOrder.Add(1),
                stopAndTakeLifecycle: () => { executionOrder.Add(2); throw new InvalidOperationException("stop and take failed"); },
                stopExecutor: () => executionOrder.Add(3),
                cancelTokenSource: () => executionOrder.Add(4),
                waitForIdle: () => executionOrder.Add(5),
                disposeTokenSource: () => executionOrder.Add(6),
                disposeTimer: () => executionOrder.Add(7),
                hideAndDisposeNotifyIcon: () => executionOrder.Add(8),
                disposeCurrentIcon: () => executionOrder.Add(9),
                disposeUiDispatcher: () => executionOrder.Add(10));

            cleanup.Execute();

            Assert.Equal(new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }, executionOrder);
            Assert.Equal(0, lifecycle.DisconnectCallCount);
        }

        [Fact]
        public void Execute_WaitForIdleThrows_DoesNotDisconnectButContinuesSafeSteps()
        {
            var executionOrder = new List<int>();
            var lifecycle = new FakeLifecycle();

            var cleanup = new MonitoringShutdownCleanup(
                stopTimer: () => executionOrder.Add(1),
                stopAndTakeLifecycle: () => { executionOrder.Add(2); return lifecycle; },
                stopExecutor: () => executionOrder.Add(3),
                cancelTokenSource: () => executionOrder.Add(4),
                waitForIdle: () => { executionOrder.Add(5); throw new InvalidOperationException("wait for idle failed"); },
                disposeTokenSource: () => executionOrder.Add(6),
                disposeTimer: () => executionOrder.Add(7),
                hideAndDisposeNotifyIcon: () => executionOrder.Add(8),
                disposeCurrentIcon: () => executionOrder.Add(9),
                disposeUiDispatcher: () => executionOrder.Add(10));

            cleanup.Execute();

            Assert.Equal(new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }, executionOrder);
            Assert.Equal(0, lifecycle.DisconnectCallCount);
        }

        [Fact]
        public void Execute_WaitForIdleSucceeds_DisconnectsProviderOnceAtEnd()
        {
            var executionOrder = new List<int>();
            var lifecycle = new FakeLifecycle();

            var cleanup = new MonitoringShutdownCleanup(
                stopTimer: () => executionOrder.Add(1),
                stopAndTakeLifecycle: () => { executionOrder.Add(2); return lifecycle; },
                stopExecutor: () => executionOrder.Add(3),
                cancelTokenSource: () => executionOrder.Add(4),
                waitForIdle: () => executionOrder.Add(5),
                disposeTokenSource: () => executionOrder.Add(6),
                disposeTimer: () => executionOrder.Add(7),
                hideAndDisposeNotifyIcon: () => executionOrder.Add(8),
                disposeCurrentIcon: () => executionOrder.Add(9),
                disposeUiDispatcher: () => executionOrder.Add(10));

            cleanup.Execute();

            Assert.Equal(new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11 }, executionOrder);
            Assert.Equal(1, lifecycle.DisconnectCallCount);
            Assert.Equal(11, executionOrder[^1]);
        }

        [Fact]
        public void Execute_NoProvider_StepsExecuteNormally()
        {
            var executionOrder = new List<int>();

            var cleanup = new MonitoringShutdownCleanup(
                stopTimer: () => executionOrder.Add(1),
                stopAndTakeLifecycle: () => { executionOrder.Add(2); return (IGpuProviderLifecycle?)null; },
                stopExecutor: () => executionOrder.Add(3),
                cancelTokenSource: () => executionOrder.Add(4),
                waitForIdle: () => executionOrder.Add(5),
                disposeTokenSource: () => executionOrder.Add(6),
                disposeTimer: () => executionOrder.Add(7),
                hideAndDisposeNotifyIcon: () => executionOrder.Add(8),
                disposeCurrentIcon: () => executionOrder.Add(9),
                disposeUiDispatcher: () => executionOrder.Add(10));

            cleanup.Execute();

            Assert.Equal(new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }, executionOrder);
        }

        [Fact]
        public void Execute_DisconnectThrows_LoggedAndCleanupContinues()
        {
            var executionOrder = new List<int>();
            var lifecycle = new FakeLifecycle();

            var cleanup = new MonitoringShutdownCleanup(
                stopTimer: () => executionOrder.Add(1),
                stopAndTakeLifecycle: () => { executionOrder.Add(2); return lifecycle; },
                stopExecutor: () => executionOrder.Add(3),
                cancelTokenSource: () => executionOrder.Add(4),
                waitForIdle: () => executionOrder.Add(5),
                disposeTokenSource: () => executionOrder.Add(6),
                disposeTimer: () => executionOrder.Add(7),
                hideAndDisposeNotifyIcon: () => executionOrder.Add(8),
                disposeCurrentIcon: () => executionOrder.Add(9),
                disposeUiDispatcher: () => executionOrder.Add(10));

            lifecycle.DisconnectCallCount = 0;

            cleanup.Execute();

            Assert.Equal(new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11 }, executionOrder);
            Assert.Equal(1, lifecycle.DisconnectCallCount);
        }
    }
}