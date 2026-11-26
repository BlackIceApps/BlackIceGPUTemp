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
            private int _connectCallCount;
            private int _disconnectCallCount;
            private readonly Action? _disconnectCallback;
            private readonly Exception? _disconnectException;

            public int ConnectCallCount => _connectCallCount;
            public int DisconnectCallCount => _disconnectCallCount;
            public bool ConnectResult { get; set; } = true;
            public string? ConnectError { get; set; }

            public FakeLifecycle(Action? disconnectCallback = null, Exception? disconnectException = null)
            {
                _disconnectCallback = disconnectCallback;
                _disconnectException = disconnectException;
            }

            public bool Connect(out string? error)
            {
                _connectCallCount++;
                error = ConnectError;
                return ConnectResult;
            }

            public void Disconnect()
            {
                _disconnectCallCount++;
                _disconnectCallback?.Invoke();
                if (_disconnectException != null)
                {
                    throw _disconnectException;
                }
            }
        }

        [Fact]
        public void Execute_AllStepsCalledInCorrectOrder()
        {
            var executionOrder = new List<int>();
            var lifecycle = new FakeLifecycle(disconnectCallback: () => executionOrder.Add(11));

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
                disposeUiDispatcher: () => executionOrder.Add(10),
                logger: (_, _) => throw new InvalidOperationException("logger failed"));

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
            var lifecycle = new FakeLifecycle(disconnectCallback: () => executionOrder.Add(11));

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
            var lifecycle = new FakeLifecycle(
                disconnectCallback: () => executionOrder.Add(11),
                disconnectException: new InvalidOperationException("disconnect failed"));

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
        }

        public static TheoryData<int, string, int[]> StepExceptionTestCases => new()
        {
            { 1, "Stop timer", new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11 } },
            { 2, "Stop and take lifecycle", new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 } },
            { 3, "Stop executor", new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11 } },
            { 4, "Cancel token source", new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11 } },
            { 5, "WaitForIdle", new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 } },
            { 6, "Dispose token source", new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11 } },
            { 7, "Dispose timer", new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11 } },
            { 8, "Hide and dispose NotifyIcon", new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11 } },
            { 9, "Dispose current icon", new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11 } },
            { 10, "Dispose UI dispatcher", new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11 } },
            { 11, "Disconnect provider", new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11 } },
        };

        [Theory]
        [MemberData(nameof(StepExceptionTestCases))]
        public void Execute_StepThrows_AllSafeStepsExecute(int failingStep, string stepName, int[] expectedOrder)
        {
            var executionOrder = new List<int>();
            var lifecycle = failingStep == 11
                ? new FakeLifecycle(disconnectCallback: () => executionOrder.Add(11), disconnectException: new InvalidOperationException("Disconnect provider failed"))
                : new FakeLifecycle(disconnectCallback: () => executionOrder.Add(11));

            Func<int, string, Action> createThrowingAction = (int step, string name) => () =>
            {
                executionOrder.Add(step);
                throw new InvalidOperationException($"{name} failed");
            };

            var cleanup = new MonitoringShutdownCleanup(
                stopTimer: failingStep == 1 ? createThrowingAction(1, "Stop timer") : () => executionOrder.Add(1),
                stopAndTakeLifecycle: failingStep == 2
                    ? () => { executionOrder.Add(2); throw new InvalidOperationException("Stop and take lifecycle failed"); }
                    : () => { executionOrder.Add(2); return lifecycle; },
                stopExecutor: failingStep == 3 ? createThrowingAction(3, "Stop executor") : () => executionOrder.Add(3),
                cancelTokenSource: failingStep == 4 ? createThrowingAction(4, "Cancel token source") : () => executionOrder.Add(4),
                waitForIdle: failingStep == 5
                    ? () => { executionOrder.Add(5); throw new InvalidOperationException("WaitForIdle failed"); }
                    : () => executionOrder.Add(5),
                disposeTokenSource: failingStep == 6 ? createThrowingAction(6, "Dispose token source") : () => executionOrder.Add(6),
                disposeTimer: failingStep == 7 ? createThrowingAction(7, "Dispose timer") : () => executionOrder.Add(7),
                hideAndDisposeNotifyIcon: failingStep == 8 ? createThrowingAction(8, "Hide and dispose NotifyIcon") : () => executionOrder.Add(8),
                disposeCurrentIcon: failingStep == 9 ? createThrowingAction(9, "Dispose current icon") : () => executionOrder.Add(9),
                disposeUiDispatcher: failingStep == 10 ? createThrowingAction(10, "Dispose UI dispatcher") : () => executionOrder.Add(10));

            cleanup.Execute();

            Assert.Equal(expectedOrder, executionOrder);

            if (failingStep == 2 || failingStep == 5)
            {
                Assert.Equal(0, lifecycle.DisconnectCallCount);
            }
            else
            {
                Assert.Equal(1, lifecycle.DisconnectCallCount);
            }
        }
    }
}