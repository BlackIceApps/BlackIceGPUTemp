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
        private int _tooltipCallCount;
        private int _iconCallCount;

        public MonitoringDisplayUpdaterTests()
        {
            _dispatcher = action => _capturedAction = action;
            _updateTooltip = text =>
            {
                _tooltipResult = text;
                _tooltipCallCount++;
            };
            _updateIcon = text =>
            {
                _iconResult = text;
                _iconCallCount++;
            };
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

            Assert.Equal("Test GPU: 66°C", _tooltipResult);
            Assert.Equal("66", _iconResult);
            Assert.Equal(1, _tooltipCallCount);
            Assert.Equal(1, _iconCallCount);
        }

        [Fact]
        public void Update_FailedReading_PassesUnavailableTooltipAndDashIcon()
        {
            var updater = new MonitoringDisplayUpdater(_dispatcher, _updateTooltip, _updateIcon, _reportError);

            updater.Update("Test GPU", false, null);
            _capturedAction!();

            Assert.Equal("Test GPU: Temp unavailable", _tooltipResult);
            Assert.Equal("--", _iconResult);
            Assert.Equal(1, _tooltipCallCount);
            Assert.Equal(1, _iconCallCount);
        }

        [Fact]
        public void Update_FailedReading_WithNullError_PassesUnavailableTooltipAndDashIcon()
        {
            var updater = new MonitoringDisplayUpdater(_dispatcher, _updateTooltip, _updateIcon, _reportError);

            updater.Update("Test GPU", false, null, null);
            _capturedAction!();

            Assert.Equal("Test GPU: Temp unavailable", _tooltipResult);
            Assert.Equal("--", _iconResult);
            Assert.Equal(1, _tooltipCallCount);
            Assert.Equal(1, _iconCallCount);
        }

        [Fact]
        public void Update_FailedReading_WithEmptyError_PassesUnavailableTooltipAndDashIcon()
        {
            var updater = new MonitoringDisplayUpdater(_dispatcher, _updateTooltip, _updateIcon, _reportError);

            updater.Update("Test GPU", false, null, "");
            _capturedAction!();

            Assert.Equal("Test GPU: Temp unavailable", _tooltipResult);
            Assert.Equal("--", _iconResult);
            Assert.Equal(1, _tooltipCallCount);
            Assert.Equal(1, _iconCallCount);
        }

        [Fact]
        public void Update_FailedReading_WithWhitespaceError_PassesUnavailableTooltipAndDashIcon()
        {
            var updater = new MonitoringDisplayUpdater(_dispatcher, _updateTooltip, _updateIcon, _reportError);

            updater.Update("Test GPU", false, null, "   ");
            _capturedAction!();

            Assert.Equal("Test GPU: Temp unavailable", _tooltipResult);
            Assert.Equal("--", _iconResult);
            Assert.Equal(1, _tooltipCallCount);
            Assert.Equal(1, _iconCallCount);
        }

        [Fact]
        public void Update_FailedReading_WithSpecificError_PassesErrorTooltipAndDashIcon()
        {
            var updater = new MonitoringDisplayUpdater(_dispatcher, _updateTooltip, _updateIcon, _reportError);

            updater.Update("Test GPU", false, null, "Device not found");
            _capturedAction!();

            Assert.Equal("Test GPU: Device not found", _tooltipResult);
            Assert.Equal("--", _iconResult);
            Assert.Equal(1, _tooltipCallCount);
            Assert.Equal(1, _iconCallCount);
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
            Assert.Equal(1, _tooltipCallCount);
            Assert.Equal(1, _iconCallCount);
        }

        [Fact]
        public void Update_SuccessfulReading_WithErrorMessage_IgnoresErrorAndReturnsSuccessTooltip()
        {
            var updater = new MonitoringDisplayUpdater(_dispatcher, _updateTooltip, _updateIcon, _reportError);
            var reading = new TemperatureReading(65.5, "GPU Core", DateTimeOffset.UtcNow);

            updater.Update("Test GPU", true, reading, "Some error");
            _capturedAction!();

            Assert.Equal("Test GPU: 66°C", _tooltipResult);
            Assert.Equal("66", _iconResult);
            Assert.Equal(1, _tooltipCallCount);
            Assert.Equal(1, _iconCallCount);
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
            Assert.Equal(0, _tooltipCallCount);
            Assert.Equal(1, _iconCallCount);
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
            Assert.Equal("Test GPU: 66°C", _tooltipResult);
            Assert.Equal(1, _tooltipCallCount);
            Assert.Equal(0, _iconCallCount);
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
            Assert.Equal(0, _tooltipCallCount);
            Assert.Equal(0, _iconCallCount);
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
            Assert.Equal(0, _tooltipCallCount);
            Assert.Equal(1, _iconCallCount);
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

                Assert.Contains("66", _tooltipResult);
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

            if (backgroundException != null)
            {
                throw new System.Reflection.TargetInvocationException("Exception thrown on background thread", backgroundException);
            }
        }

        [Fact]
        public void Update_SameTemperatureTwice_DoesNotCallCallbacksAgain()
        {
            var updater = new MonitoringDisplayUpdater(_dispatcher, _updateTooltip, _updateIcon, _reportError);
            var reading1 = new TemperatureReading(65.4, "GPU Core", DateTimeOffset.UtcNow); // rounds to 65
            var reading2 = new TemperatureReading(65.5, "GPU Core", DateTimeOffset.UtcNow); // rounds to 66

            // First update: 65.4 -> rounds to 65
            updater.Update("Test GPU", true, reading1);
            _capturedAction!();

            Assert.Equal("Test GPU: 65°C", _tooltipResult);
            Assert.Equal("65", _iconResult);
            Assert.Equal(1, _tooltipCallCount);
            Assert.Equal(1, _iconCallCount);

            // Second update: 65.5 -> rounds to 66 (different, so callbacks called)
            _tooltipCallCount = 0;
            _iconCallCount = 0;
            updater.Update("Test GPU", true, reading2);
            _capturedAction!();

            Assert.Equal("Test GPU: 66°C", _tooltipResult);
            Assert.Equal("66", _iconResult);
            Assert.Equal(1, _tooltipCallCount);
            Assert.Equal(1, _iconCallCount);

            // Third update: 65.5 again -> rounds to 66 (same, no callbacks)
            _tooltipCallCount = 0;
            _iconCallCount = 0;
            updater.Update("Test GPU", true, reading2);
            _capturedAction!();

            Assert.Equal("Test GPU: 66°C", _tooltipResult);
            Assert.Equal("66", _iconResult);
            Assert.Equal(0, _tooltipCallCount);
            Assert.Equal(0, _iconCallCount);
        }

        [Fact]
        public void Update_TemperaturesWithinSameRoundedDegree_DoesNotCallCallbacksAgain()
        {
            var updater = new MonitoringDisplayUpdater(_dispatcher, _updateTooltip, _updateIcon, _reportError);
            var reading1 = new TemperatureReading(65.1, "GPU Core", DateTimeOffset.UtcNow); // rounds to 65
            var reading2 = new TemperatureReading(65.4, "GPU Core", DateTimeOffset.UtcNow); // rounds to 65

            // First update: 65.1 -> rounds to 65
            updater.Update("Test GPU", true, reading1);
            _capturedAction!();

            Assert.Equal("Test GPU: 65°C", _tooltipResult);
            Assert.Equal("65", _iconResult);
            Assert.Equal(1, _tooltipCallCount);
            Assert.Equal(1, _iconCallCount);

            // Second update: 65.4 -> also rounds to 65 (same rounded value, no callbacks)
            _tooltipCallCount = 0;
            _iconCallCount = 0;
            updater.Update("Test GPU", true, reading2);
            _capturedAction!();

            Assert.Equal("Test GPU: 65°C", _tooltipResult);
            Assert.Equal("65", _iconResult);
            Assert.Equal(0, _tooltipCallCount);
            Assert.Equal(0, _iconCallCount);
        }

        [Fact]
        public void Update_SameErrorTwice_DoesNotCallCallbacksAgain()
        {
            var updater = new MonitoringDisplayUpdater(_dispatcher, _updateTooltip, _updateIcon, _reportError);

            // First error update
            updater.Update("Test GPU", false, null, "Device not found");
            _capturedAction!();

            Assert.Equal("Test GPU: Device not found", _tooltipResult);
            Assert.Equal("--", _iconResult);
            Assert.Equal(1, _tooltipCallCount);
            Assert.Equal(1, _iconCallCount);

            // Second same error update - no callbacks
            _tooltipCallCount = 0;
            _iconCallCount = 0;
            updater.Update("Test GPU", false, null, "Device not found");
            _capturedAction!();

            Assert.Equal("Test GPU: Device not found", _tooltipResult);
            Assert.Equal("--", _iconResult);
            Assert.Equal(0, _tooltipCallCount);
            Assert.Equal(0, _iconCallCount);
        }

        [Fact]
        public void Update_ErrorMessageChange_UpdatesTooltipButNotIcon()
        {
            var updater = new MonitoringDisplayUpdater(_dispatcher, _updateTooltip, _updateIcon, _reportError);

            // First error
            updater.Update("Test GPU", false, null, "Error 1");
            _capturedAction!();

            Assert.Equal("Test GPU: Error 1", _tooltipResult);
            Assert.Equal("--", _iconResult);
            Assert.Equal(1, _tooltipCallCount);
            Assert.Equal(1, _iconCallCount);

            // Second error with different message - tooltip changes, icon stays same (--)
            _tooltipCallCount = 0;
            _iconCallCount = 0;
            updater.Update("Test GPU", false, null, "Error 2");
            _capturedAction!();

            Assert.Equal("Test GPU: Error 2", _tooltipResult);
            Assert.Equal("--", _iconResult);
            Assert.Equal(1, _tooltipCallCount);
            Assert.Equal(0, _iconCallCount);
        }

        [Fact]
        public void Update_TooltipThrows_DoesNotCacheText_NextUpdateRetries()
        {
            var callCount = 0;
            var tooltipException = new InvalidOperationException("Tooltip failed");
            var updateTooltip = new Action<string>(_ =>
            {
                callCount++;
                if (callCount == 1) throw tooltipException;
            });

            var updater = new MonitoringDisplayUpdater(_dispatcher, updateTooltip, _updateIcon, _reportError);
            var reading = new TemperatureReading(65.5, "GPU Core", DateTimeOffset.UtcNow);

            // First update - tooltip throws
            updater.Update("Test GPU", true, reading);
            _capturedAction!();

            Assert.Equal(tooltipException, _reportedError);
            Assert.Equal("66", _iconResult);
            Assert.Equal(1, callCount);
            Assert.Equal(1, _iconCallCount); // Fixture icon callback succeeded
            Assert.Equal(0, _tooltipCallCount); // Custom tooltip callback threw

            // Second update - retry tooltip (callCount continues from 1, so second call succeeds)
            // Icon is cached ("66"), so fixture icon callback NOT called
            // Tooltip uses custom callback, not fixture callback
            _reportedError = null;
            _tooltipCallCount = 0;
            _iconCallCount = 0;
            updater.Update("Test GPU", true, reading);
            _capturedAction!();

            Assert.Null(_reportedError);
            Assert.Equal("66", _iconResult);
            Assert.Equal(2, callCount); // Custom callback called twice total
            Assert.Equal(0, _tooltipCallCount); // Custom callback used, not fixture
            Assert.Equal(0, _iconCallCount); // Icon cached, fixture callback not called
        }

        [Fact]
        public void Update_IconThrows_DoesNotCacheText_NextUpdateRetries()
        {
            var callCount = 0;
            var iconException = new InvalidOperationException("Icon failed");
            string? capturedIconText = null;
            var updateIcon = new Action<string>(text =>
            {
                callCount++;
                if (callCount == 1) throw iconException;
                capturedIconText = text;
            });

            var updater = new MonitoringDisplayUpdater(_dispatcher, _updateTooltip, updateIcon, _reportError);
            var reading = new TemperatureReading(65.5, "GPU Core", DateTimeOffset.UtcNow);

            // First update - icon throws
            updater.Update("Test GPU", true, reading);
            _capturedAction!();

            Assert.Equal(iconException, _reportedError);
            Assert.Equal("Test GPU: 66°C", _tooltipResult);
            Assert.Equal(1, callCount);
            Assert.Equal(1, _tooltipCallCount); // Fixture tooltip callback succeeded
            Assert.Equal(0, _iconCallCount); // Custom icon callback threw
            Assert.Null(capturedIconText); // No text captured on first call (threw)

            // Second update - retry icon (callCount continues from 1, so second call succeeds)
            // Tooltip is cached ("Test GPU: 66°C"), so fixture tooltip callback NOT called
            // Icon uses custom callback, not fixture callback
            _reportedError = null;
            _tooltipCallCount = 0;
            _iconCallCount = 0;
            updater.Update("Test GPU", true, reading);
            _capturedAction!();

            Assert.Null(_reportedError);
            Assert.Equal("Test GPU: 66°C", _tooltipResult);
            Assert.Equal("66", capturedIconText); // Custom callback captured the text
            Assert.Equal(2, callCount); // Custom callback called twice total
            Assert.Equal(0, _tooltipCallCount); // Tooltip cached, fixture callback not called
            Assert.Equal(0, _iconCallCount); // Custom callback used, not fixture
        }

        [Fact]
        public void Update_TooltipThrows_IconStillUpdates()
        {
            var tooltipException = new InvalidOperationException("Tooltip failed");
            var updateTooltip = new Action<string>(_ => throw tooltipException);

            var updater = new MonitoringDisplayUpdater(_dispatcher, updateTooltip, _updateIcon, _reportError);
            var reading = new TemperatureReading(65.5, "GPU Core", DateTimeOffset.UtcNow);

            updater.Update("Test GPU", true, reading);
            _capturedAction!();

            Assert.Equal(tooltipException, _reportedError);
            Assert.Equal("66", _iconResult);
            Assert.Equal(0, _tooltipCallCount);
            Assert.Equal(1, _iconCallCount);
        }

        [Fact]
        public void Update_IconThrows_TooltipStillUpdates()
        {
            var iconException = new InvalidOperationException("Icon failed");
            var updateIcon = new Action<string>(_ => throw iconException);

            var updater = new MonitoringDisplayUpdater(_dispatcher, _updateTooltip, updateIcon, _reportError);
            var reading = new TemperatureReading(65.5, "GPU Core", DateTimeOffset.UtcNow);

            updater.Update("Test GPU", true, reading);
            _capturedAction!();

            Assert.Equal(iconException, _reportedError);
            Assert.Equal("Test GPU: 66°C", _tooltipResult);
            Assert.Equal(1, _tooltipCallCount);
            Assert.Equal(0, _iconCallCount);
        }

        [Fact]
        public void Update_TooltipErrorThenSuccess_IconSuccessThenError_BothIndependent()
        {
            var tooltipException = new InvalidOperationException("Tooltip failed");
            var iconException = new InvalidOperationException("Icon failed");
            var tooltipCallCount = 0;
            var iconCallCount = 0;
            var updateTooltip = new Action<string>(_ =>
            {
                tooltipCallCount++;
                if (tooltipCallCount == 1) throw tooltipException;
            });
            var updateIcon = new Action<string>(_ =>
            {
                iconCallCount++;
                if (iconCallCount == 1) throw iconException;
            });

            var reportedErrors = new System.Collections.Generic.List<Exception>();
            var reportError = new Action<Exception>(ex => reportedErrors.Add(ex));

            var updater = new MonitoringDisplayUpdater(_dispatcher, updateTooltip, updateIcon, reportError);
            var reading = new TemperatureReading(65.5, "GPU Core", DateTimeOffset.UtcNow);

            // First update - both throw
            updater.Update("Test GPU", true, reading);
            _capturedAction!();

            Assert.Equal(2, reportedErrors.Count);
            Assert.Contains(tooltipException, reportedErrors);
            Assert.Contains(iconException, reportedErrors);
            Assert.Equal(1, tooltipCallCount); // Custom tooltip callback called once (threw)
            Assert.Equal(1, iconCallCount); // Custom icon callback called once (threw)

            // Second update - both succeed
            reportedErrors.Clear();
            updater.Update("Test GPU", true, reading);
            _capturedAction!();

            Assert.Empty(reportedErrors);
            Assert.Equal(2, tooltipCallCount); // Custom tooltip callback called twice total (second succeeded)
            Assert.Equal(2, iconCallCount); // Custom icon callback called twice total (second succeeded)
        }
    }
}