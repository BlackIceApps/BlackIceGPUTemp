using System;
using System.Collections.Generic;
using System.Threading;
using BlackIceGPUTemp.Tray;
using Xunit;

namespace BlackIceGPUTemp.Tests
{
    public class MonitoringReadTriggerTests
    {
        [Fact]
        public void TryStart_NoSession_ReturnsFalse_ExecutorNotCalled()
        {
            var capturedActions = new List<Action>();
            var executor = new MonitoringReadExecutor(action => capturedActions.Add(action));
            var trigger = new MonitoringReadTrigger(executor);

            bool result = trigger.TryStart(
                hasSession: false,
                providerId: "provider",
                deviceId: "device",
                isDisposing: false,
                operation: _ => { },
                cancellationToken: CancellationToken.None);

            Assert.False(result);
            Assert.Empty(capturedActions);
        }

        [Fact]
        public void TryStart_ProviderIdNull_ReturnsFalse()
        {
            var capturedActions = new List<Action>();
            var executor = new MonitoringReadExecutor(action => capturedActions.Add(action));
            var trigger = new MonitoringReadTrigger(executor);

            bool result = trigger.TryStart(
                hasSession: true,
                providerId: null,
                deviceId: "device",
                isDisposing: false,
                operation: _ => { },
                cancellationToken: CancellationToken.None);

            Assert.False(result);
            Assert.Empty(capturedActions);
        }

        [Fact]
        public void TryStart_ProviderIdEmpty_ReturnsFalse()
        {
            var capturedActions = new List<Action>();
            var executor = new MonitoringReadExecutor(action => capturedActions.Add(action));
            var trigger = new MonitoringReadTrigger(executor);

            bool result = trigger.TryStart(
                hasSession: true,
                providerId: "",
                deviceId: "device",
                isDisposing: false,
                operation: _ => { },
                cancellationToken: CancellationToken.None);

            Assert.False(result);
            Assert.Empty(capturedActions);
        }

        [Fact]
        public void TryStart_ProviderIdWhitespace_ReturnsFalse()
        {
            var capturedActions = new List<Action>();
            var executor = new MonitoringReadExecutor(action => capturedActions.Add(action));
            var trigger = new MonitoringReadTrigger(executor);

            bool result = trigger.TryStart(
                hasSession: true,
                providerId: "   ",
                deviceId: "device",
                isDisposing: false,
                operation: _ => { },
                cancellationToken: CancellationToken.None);

            Assert.False(result);
            Assert.Empty(capturedActions);
        }

        [Fact]
        public void TryStart_DeviceIdNull_ReturnsFalse()
        {
            var capturedActions = new List<Action>();
            var executor = new MonitoringReadExecutor(action => capturedActions.Add(action));
            var trigger = new MonitoringReadTrigger(executor);

            bool result = trigger.TryStart(
                hasSession: true,
                providerId: "provider",
                deviceId: null,
                isDisposing: false,
                operation: _ => { },
                cancellationToken: CancellationToken.None);

            Assert.False(result);
            Assert.Empty(capturedActions);
        }

        [Fact]
        public void TryStart_DeviceIdEmpty_ReturnsFalse()
        {
            var capturedActions = new List<Action>();
            var executor = new MonitoringReadExecutor(action => capturedActions.Add(action));
            var trigger = new MonitoringReadTrigger(executor);

            bool result = trigger.TryStart(
                hasSession: true,
                providerId: "provider",
                deviceId: "",
                isDisposing: false,
                operation: _ => { },
                cancellationToken: CancellationToken.None);

            Assert.False(result);
            Assert.Empty(capturedActions);
        }

        [Fact]
        public void TryStart_DeviceIdWhitespace_ReturnsFalse()
        {
            var capturedActions = new List<Action>();
            var executor = new MonitoringReadExecutor(action => capturedActions.Add(action));
            var trigger = new MonitoringReadTrigger(executor);

            bool result = trigger.TryStart(
                hasSession: true,
                providerId: "provider",
                deviceId: "   ",
                isDisposing: false,
                operation: _ => { },
                cancellationToken: CancellationToken.None);

            Assert.False(result);
            Assert.Empty(capturedActions);
        }

        [Fact]
        public void TryStart_IsDisposingTrue_ReturnsFalse()
        {
            var capturedActions = new List<Action>();
            var executor = new MonitoringReadExecutor(action => capturedActions.Add(action));
            var trigger = new MonitoringReadTrigger(executor);

            bool result = trigger.TryStart(
                hasSession: true,
                providerId: "provider",
                deviceId: "device",
                isDisposing: true,
                operation: _ => { },
                cancellationToken: CancellationToken.None);

            Assert.False(result);
            Assert.Empty(capturedActions);
        }

        [Fact]
        public void TryStart_ValidData_ReturnsExecutorResult()
        {
            var capturedActions = new List<Action>();
            var executor = new MonitoringReadExecutor(action => capturedActions.Add(action));
            var trigger = new MonitoringReadTrigger(executor);

            bool result = trigger.TryStart(
                hasSession: true,
                providerId: "provider",
                deviceId: "device",
                isDisposing: false,
                operation: _ => { },
                cancellationToken: CancellationToken.None);

            Assert.True(result);
            Assert.Single(capturedActions);
        }

        [Fact]
        public void TryStart_ExecutorRejectsSecondRead_ReturnsFalse()
        {
            var capturedActions = new List<Action>();
            var executor = new MonitoringReadExecutor(action => capturedActions.Add(action));
            var trigger = new MonitoringReadTrigger(executor);

            bool first = trigger.TryStart(
                hasSession: true,
                providerId: "provider",
                deviceId: "device",
                isDisposing: false,
                operation: _ => { },
                cancellationToken: CancellationToken.None);

            Assert.True(first);

            bool second = trigger.TryStart(
                hasSession: true,
                providerId: "provider",
                deviceId: "device",
                isDisposing: false,
                operation: _ => { },
                cancellationToken: CancellationToken.None);

            Assert.False(second);
            Assert.Single(capturedActions);
        }

        [Fact]
        public void TryStart_TokenPassedToOperation_IsSameToken()
        {
            var capturedActions = new List<Action>();
            var capturedToken = new object?();
            var executor = new MonitoringReadExecutor(action => capturedActions.Add(action));
            var trigger = new MonitoringReadTrigger(executor);
            var expectedToken = new CancellationTokenSource().Token;

            trigger.TryStart(
                hasSession: true,
                providerId: "provider",
                deviceId: "device",
                isDisposing: false,
                operation: ct => { capturedToken = ct; },
                cancellationToken: expectedToken);

            capturedActions[0]();

            Assert.Same(expectedToken, capturedToken);
        }
    }
}