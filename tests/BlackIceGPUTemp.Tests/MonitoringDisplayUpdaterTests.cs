using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.Threading;
using BlackIceGPUTemp.Monitoring.Abstractions;
using BlackIceGPUTemp.Tray;
using Xunit;

namespace BlackIceGPUTemp.Tests
{
    public class MonitoringDisplayUpdaterTests
    {
        private readonly Action<Action> _dispatcher;
        private readonly Action<string> _updateTooltip;
        private readonly Action<string> _updateIcon;
        private readonly Action<Exception> _reportError;

        private Action? _capturedAction;
        private string? _tooltipResult;
        private string? _iconResult;
        private Exception? _reportedError;

        public MonitoringDisplayUpdaterTests()
        {
            _dispatcher = action => _capturedAction = action;
            _updateTooltip = text => _tooltipResult = text;
            _updateIcon = text => _iconResult = text;
            _reportError = ex => _reportedError = ex;
        }

        [Fact]
        public void Constructor_ThrowsWhenDispatcherIsNull()
        {
            Assert.Throws<ArgumentNullException>(() => new MonitoringDisplayUpdater(
                null!,
                _updateTooltip,
                _updateIcon,
                _reportError));
        }

        [Fact]
        public void Constructor_ThrowsWhenUpdateTooltipIsNull()
        {
            Assert.Throws<ArgumentNullException>(() => new MonitoringDisplayUpdater(
                _dispatcher,
                null!,
                _updateIcon,
                _reportError));
        }

        [Fact]
        public void Constructor_ThrowsWhenUpdateIconIsNull()
        {
            Assert.Throws<ArgumentNullException>(() => new MonitoringDisplayUpdater(
                _dispatcher,
                _updateTooltip,
                null!,
                _reportError));
        }

        [Fact]
        public void Constructor_ThrowsWhenReportErrorIsNull()
        {
            Assert.Throws<ArgumentNullException>(() => new MonitoringDisplayUpdater(
                _dispatcher,
                _updateTooltip,
                _updateIcon,
                null!));
        }

        [Fact]
        public void Update_DoesNotExecuteBeforeDispatcherActionInvoked()
        {
            var updater = new MonitoringDisplayUpdater(_dispatcher, _updateTooltip, _updateIcon, _reportError);
            var reading = new TemperatureReading(65.5, "GPU Core", DateTimeOffset.UtcNow);

            updater.Update("Test GPU", true, reading);

            Assert.Null(_tooltipResult);
            Assert.Null(_iconResult);
        }

        [Fact]
        public void Update_SuccessfulReading_PassesCorrectTooltipAndIconText()
        {
            var updater = new MonitoringDisplayUpdater(_dispatcher, _updateTooltip, _updateIcon, _reportError);
            var reading = new TemperatureReading(65.5, "GPU Core", DateTimeOffset.UtcNow);

            updater.Update("Test GPU", true, reading);
            _capturedAction!();

            Assert.Equal("Test GPU: 65.5°C", _tooltipResult);
            Assert.Equal("66", _iconResult);
        }

        [Fact]
        public void Update_FailedReading_PassesUnavailableTooltipAndDashIcon()
        {
            var updater = new MonitoringDisplayUpdater(_dispatcher, _updateTooltip, _updateIcon, _reportError);

            updater.Update("Test GPU", false, null);
            _capturedAction!();

            Assert.Equal("Test GPU: Temp unavailable", _tooltipResult);
            Assert.Equal("--", _iconResult);
        }

        [Fact]
        public void Update_FailedReading_WithNullError_PassesUnavailableTooltipAndDashIcon()
        {
            var updater = new MonitoringDisplayUpdater(_dispatcher, _updateTooltip, _updateIcon, _reportError);

            updater.Update("Test GPU", false, null, null);
            _capturedAction!();

            Assert.Equal("Test GPU: Temp unavailable", _tooltipResult);
            Assert.Equal("--", _iconResult);
        }

        [Fact]
        public void Update_FailedReading_WithEmptyError_PassesUnavailableTooltipAndDashIcon()
        {
            var updater = new MonitoringDisplayUpdater(_dispatcher, _updateTooltip, _updateIcon, _reportError);

            updater.Update("Test GPU", false, null, "");
            _capturedAction!();

            Assert.Equal("Test GPU: Temp unavailable", _tooltipResult);
            Assert.Equal("--", _iconResult);
        }

        [Fact]
        public void Update_FailedReading_WithWhitespaceError_PassesUnavailableTooltipAndDashIcon()
        {
            var updater = new MonitoringDisplayUpdater(_dispatcher, _updateTooltip, _updateIcon, _reportError);

            updater.Update("Test GPU", false, null, "   ");
            _capturedAction!();

            Assert.Equal("Test GPU: Temp unavailable", _tooltipResult);
            Assert.Equal("--", _iconResult);
        }

        [Fact]
        public void Update_FailedReading_WithSpecificError_PassesErrorTooltipAndDashIcon()
        {
            var updater = new MonitoringDisplayUpdater(_dispatcher, _updateTooltip, _updateIcon, _reportError);

            updater.Update("Test GPU", false, null, "Device not found");
            _capturedAction!();

            Assert.Equal("Test GPU: Device not found", _tooltipResult);
            Assert.Equal("--", _iconResult);
        }

        [Fact]
        public void Update_FailedReading_WithNonNullReading_PassesErrorTooltipAndDashIcon()
        {
            var updater = new MonitoringDisplayUpdater(_dispatcher, _updateTooltip, _updateIcon, _reportError);
            var reading = new TemperatureReading(75.0, "GPU Core", DateTimeOffset.UtcNow);

            updater.Update("Test GPU", false, reading, "Failed to read temperature from device.");
            _capturedAction!();

            Assert.Equal("Test GPU: Failed to read temperature from device.", _tooltipResult);
            Assert.Equal("--", _iconResult);
        }

        [Fact]
        public void Update_SuccessfulReading_WithErrorMessage_IgnoresErrorAndReturnsSuccessTooltip()
        {
            var updater = new MonitoringDisplayUpdater(_dispatcher, _updateTooltip, _updateIcon, _reportError);
            var reading = new TemperatureReading(65.5, "GPU Core", DateTimeOffset.UtcNow);

            updater.Update("Test GPU", true, reading, "Some error");
            _capturedAction!();

            Assert.Equal("Test GPU: 65.5°C", _tooltipResult);
            Assert.Equal("66", _iconResult);
        }

        [Fact]
        public void Update_TooltipThrows_ReportsErrorAndIconStillUpdates()
        {
            var tooltipException = new InvalidOperationException("Tooltip failed");
            var updateTooltip = new Action<string>(_ => throw tooltipException);

            var updater = new MonitoringDisplayUpdater(_dispatcher, updateTooltip, _updateIcon, _reportError);
            var reading = new TemperatureReading(65.5, "GPU Core", DateTimeOffset.UtcNow);

            updater.Update("Test GPU", true, reading);
            _capturedAction!();

            Assert.Equal(tooltipException, _reportedError);
            Assert.Equal("66", _iconResult);
        }

        [Fact]
        public void Update_IconThrows_ReportsError()
        {
            var iconException = new InvalidOperationException("Icon failed");
            var updateIcon = new Action<string>(_ => throw iconException);

            var updater = new MonitoringDisplayUpdater(_dispatcher, _updateTooltip, updateIcon, _reportError);
            var reading = new TemperatureReading(65.5, "GPU Core", DateTimeOffset.UtcNow);

            updater.Update("Test GPU", true, reading);
            _capturedAction!();

            Assert.Equal(iconException, _reportedError);
            Assert.Equal("Test GPU: 65.5°C", _tooltipResult);
        }

        [Fact]
        public void Update_BothThrow_BothErrorsReportedIndependently()
        {
            var tooltipException = new InvalidOperationException("Tooltip failed");
            var iconException = new InvalidOperationException("Icon failed");
            var updateTooltip = new Action<string>(_ => throw tooltipException);
            var updateIcon = new Action<string>(_ => throw iconException);

            var reportedErrors = new System.Collections.Generic.List<Exception>();
            var reportError = new Action<Exception>(ex => reportedErrors.Add(ex));

            var updater = new MonitoringDisplayUpdater(_dispatcher, updateTooltip, updateIcon, reportError);
            var reading = new TemperatureReading(65.5, "GPU Core", DateTimeOffset.UtcNow);

            updater.Update("Test GPU", true, reading);
            _capturedAction!();

            Assert.Equal(2, reportedErrors.Count);
            Assert.Contains(tooltipException, reportedErrors);
            Assert.Contains(iconException, reportedErrors);
        }

        [Fact]
        public void Update_ReportErrorThrows_DoesNotEscapeCallback()
        {
            var tooltipException = new InvalidOperationException("Tooltip failed");
            var updateTooltip = new Action<string>(_ => throw tooltipException);
            var reportError = new Action<Exception>(_ => throw new InvalidOperationException("Report failed"));

            var updater = new MonitoringDisplayUpdater(_dispatcher, updateTooltip, _updateIcon, reportError);
            var reading = new TemperatureReading(65.5, "GPU Core", DateTimeOffset.UtcNow);

            var exception = Record.Exception(() =>
            {
                updater.Update("Test GPU", true, reading);
                _capturedAction!();
            });

            Assert.Null(exception);
            Assert.Equal("66", _iconResult);
        }

        [Fact]
        public void Update_UsesDispatcherThreadCultureForFormatting()
        {
            var updater = new MonitoringDisplayUpdater(_dispatcher, _updateTooltip, _updateIcon, _reportError);
            var reading = new TemperatureReading(65.5, "GPU Core", DateTimeOffset.UtcNow);

            var originalCulture = Thread.CurrentThread.CurrentCulture;
            Thread.CurrentThread.CurrentCulture = new CultureInfo("en-US");

            try
            {
                updater.Update("Test GPU", true, reading);

                Thread.CurrentThread.CurrentCulture = new CultureInfo("pl-PL");
                _capturedAction!();

                Assert.Contains("65,5", _tooltipResult);
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = originalCulture;
            }
        }

        [Fact]
        public void Update_ExecutesOnDispatcherThreadOnlyWhenInvokedFromAnotherThread()
        {
            int? callingThreadId = null;
            int? dispatcherCallbackThreadId = null;
            int? tooltipThreadId = null;
            int? iconThreadId = null;

            Exception? backgroundException = null;

            BlockingCollection<Action>? actionQueue = null;
            ManualResetEventSlim? dispatcherReadyEvent = null;
            ManualResetEventSlim? actionExecutedEvent = null;
            Thread? dispatcherThread = null;
            Thread? updateThread = null;

            try
            {
                actionQueue = new BlockingCollection<Action>();
                dispatcherReadyEvent = new ManualResetEventSlim(false);
                actionExecutedEvent = new ManualResetEventSlim(false);

                var localActionQueue = actionQueue;
                var localDispatcherReadyEvent = dispatcherReadyEvent;
                var localActionExecutedEvent = actionExecutedEvent;

                dispatcherThread = new Thread(() =>
                {
                    try
                    {
                        localDispatcherReadyEvent.Set();
                        foreach (var action in localActionQueue.GetConsumingEnumerable())
                        {
                            dispatcherCallbackThreadId = Environment.CurrentManagedThreadId;
                            try
                            {
                                action();
                            }
                            finally
                            {
                                localActionExecutedEvent.Set();
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Interlocked.CompareExchange(ref backgroundException, ex, null);
                    }
                })
                {
                    IsBackground = true
                };

                dispatcherThread.Start();
                Assert.True(dispatcherReadyEvent.Wait(TimeSpan.FromSeconds(5)), "Dispatcher thread failed to start within timeout.");

                Action<Action> dedicatedDispatcher = action =>
                {
                    if (!localActionQueue.IsAddingCompleted)
                    {
                        localActionQueue.Add(action);
                    }
                };

                var updater = new MonitoringDisplayUpdater(
                    dedicatedDispatcher,
                    text =>
                    {
                        tooltipThreadId = Environment.CurrentManagedThreadId;
                    },
                    text =>
                    {
                        iconThreadId = Environment.CurrentManagedThreadId;
                    },
                    _reportError);

                var reading = new TemperatureReading(65.5, "GPU Core", DateTimeOffset.UtcNow);

                updateThread = new Thread(() =>
                {
                    try
                    {
                        callingThreadId = Environment.CurrentManagedThreadId;
                        updater.Update("Test GPU", true, reading);
                    }
                    catch (Exception ex)
                    {
                        Interlocked.CompareExchange(ref backgroundException, ex, null);
                    }
                })
                {
                    IsBackground = true
                };

                updateThread.Start();
                Assert.True(updateThread.Join(TimeSpan.FromSeconds(5)), "Update thread failed to complete within timeout.");
                Assert.True(actionExecutedEvent.Wait(TimeSpan.FromSeconds(5)), "Dispatcher action execution timed out.");

                if (backgroundException != null)
                {
                    throw new System.Reflection.TargetInvocationException("Exception thrown on background thread", backgroundException);
                }

                Assert.NotNull(callingThreadId);
                Assert.NotNull(dispatcherCallbackThreadId);
                Assert.NotNull(tooltipThreadId);
                Assert.NotNull(iconThreadId);

                Assert.Equal(dispatcherCallbackThreadId, tooltipThreadId);
                Assert.Equal(dispatcherCallbackThreadId, iconThreadId);
                Assert.NotEqual(callingThreadId, tooltipThreadId);
                Assert.NotEqual(callingThreadId, iconThreadId);
            }
            finally
            {
                bool updateJoined = updateThread == null || !updateThread.IsAlive || updateThread.Join(TimeSpan.FromSeconds(5));

                if (actionQueue != null)
                {
                    try
                    {
                        actionQueue.CompleteAdding();
                    }
                    catch
                    {
                    }
                }

                bool dispatcherJoined = dispatcherThread == null || !dispatcherThread.IsAlive || dispatcherThread.Join(TimeSpan.FromSeconds(5));

                if (updateJoined && dispatcherJoined)
                {
                    try { actionQueue?.Dispose(); } catch { }
                    try { dispatcherReadyEvent?.Dispose(); } catch { }
                    try { actionExecutedEvent?.Dispose(); } catch { }
                }
            }
        }
    }
}