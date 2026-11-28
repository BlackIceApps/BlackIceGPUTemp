using System;
using BlackIceGPUTemp.Logging;
using BlackIceGPUTemp.Monitoring.Providers.Amd.Native;

namespace BlackIceGPUTemp.Monitoring.Providers.Amd
{
    internal sealed class AdlxPendingCleanup : IDisposable
    {
        private const int ADLX_ORPHAN_OBJECTS = 0xB;

        private enum CleanupStage
        {
            None,
            TerminatePending,
            DisposePending,
            Completed
        }

        private CleanupStage _stage = CleanupStage.None;
        private IAdlxNative? _pendingNative;
        private IDisposable? _pendingDisposable;
        private IAdlxNative? _pendingDisposeNative;
        private IDisposable? _pendingDisposeDisposable;
        private bool _disposed;

        public bool HasPendingCleanup => _stage != CleanupStage.None && _stage != CleanupStage.Completed;
        public bool IsTerminatePending => _stage == CleanupStage.TerminatePending;
        public bool IsDisposePending => _stage == CleanupStage.DisposePending;

        public AdlxPendingCleanup()
        {
        }

        public void CaptureFromSession(IAdlxNative? pendingNative, IDisposable? pendingDisposable,
            IAdlxNative? pendingDisposeNative, IDisposable? pendingDisposeDisposable)
        {
            if (pendingDisposeNative != null && pendingDisposeDisposable != null)
            {
                _pendingDisposeNative = pendingDisposeNative;
                _pendingDisposeDisposable = pendingDisposeDisposable;
                _pendingNative = null;
                _pendingDisposable = null;
                _stage = CleanupStage.DisposePending;
            }
            else if (pendingNative != null)
            {
                _pendingNative = pendingNative;
                _pendingDisposable = pendingDisposable;
                _stage = CleanupStage.TerminatePending;
            }
            else
            {
                _pendingNative = null;
                _pendingDisposable = null;
                _stage = CleanupStage.None;
            }
        }

        public void CaptureFromFailedInitialization(IAdlxNative? native, IDisposable? disposableNative)
        {
            if (native != null && disposableNative != null)
            {
                _pendingDisposeNative = native;
                _pendingDisposeDisposable = disposableNative;
                _pendingNative = null;
                _pendingDisposable = null;
                _stage = CleanupStage.DisposePending;
            }
        }

        public bool TryHandlePendingCleanup(out string? error)
        {
            error = null;

            if (_stage == CleanupStage.DisposePending && _pendingDisposeNative != null && _pendingDisposeDisposable != null)
            {
                try
                {
                    _pendingDisposeDisposable.Dispose();
                }
                catch (Exception ex)
                {
                    Logger.LogError("Pending AdlxNative Dispose retry threw exception", ex);
                    error = "Pending AdlxNative Dispose retry threw exception";
                    return false;
                }
                _pendingDisposeNative = null;
                _pendingDisposeDisposable = null;
                _stage = CleanupStage.Completed;
            }

            if (_stage == CleanupStage.TerminatePending && _pendingNative != null)
            {
                int retryResult = 0;
                Exception? retryException = null;
                try
                {
                    retryResult = _pendingNative.Terminate();
                }
                catch (Exception ex)
                {
                    retryException = ex;
                }

                if (retryException == null && (retryResult == 0 || retryResult == ADLX_ORPHAN_OBJECTS))
                {
                    try
                    {
                        _pendingDisposable?.Dispose();
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError("Pending AdlxNative Dispose threw exception on retry", ex);
                        _pendingDisposeNative = _pendingNative;
                        _pendingDisposeDisposable = _pendingDisposable;
                        _pendingNative = null;
                        _pendingDisposable = null;
                        _stage = CleanupStage.DisposePending;
                        error = "Pending AdlxNative Dispose threw exception on retry";
                        return false;
                    }
                    _pendingNative = null;
                    _pendingDisposable = null;
                    _stage = CleanupStage.Completed;
                }
                else
                {
                    Logger.LogError($"Retry Terminate failed with HRESULT: 0x{retryResult:X}" + (retryException != null ? $", exception: {retryException.Message}" : ""));
                    error = $"Retry Terminate failed with HRESULT: 0x{retryResult:X}";
                    return false;
                }
            }

            return true;
        }

        public CleanupResult PerformCleanup(IAdlxNative? native, IDisposable? disposableNative, bool initialized)
        {
            IAdlxNative? pendingNative = null;
            IDisposable? pendingDisposable = null;
            IAdlxNative? pendingDisposeNative = null;
            IDisposable? pendingDisposeDisposable = null;

            if (initialized && native != null)
            {
                int terminateResult = 0;
                Exception? terminateException = null;

                try
                {
                    terminateResult = native.Terminate();
                }
                catch (Exception ex)
                {
                    terminateException = ex;
                    Logger.LogError("ADLX Terminate threw exception", ex);
                }

                if (terminateException != null)
                {
                    pendingNative = native;
                    pendingDisposable = disposableNative;
                    _stage = CleanupStage.TerminatePending;
                }
                else if (terminateResult == 0 || terminateResult == ADLX_ORPHAN_OBJECTS)
                {
                    if (disposableNative != null)
                    {
                        try
                        {
                            disposableNative.Dispose();
                            _stage = CleanupStage.Completed;
                        }
                        catch (Exception ex)
                        {
                            Logger.LogError("AdlxNative Dispose threw exception", ex);
                            pendingDisposeNative = native;
                            pendingDisposeDisposable = disposableNative;
                            _stage = CleanupStage.DisposePending;
                        }
                    }
                    else
                    {
                        _stage = CleanupStage.Completed;
                    }
                }
                else
                {
                    Logger.LogError($"ADLX Terminate failed with HRESULT: 0x{terminateResult:X}");
                    pendingNative = native;
                    pendingDisposable = disposableNative;
                    _stage = CleanupStage.TerminatePending;
                }
            }
            else if (disposableNative != null)
            {
                bool disposeSucceeded = false;
                try
                {
                    disposableNative.Dispose();
                    disposeSucceeded = true;
                }
                catch (Exception ex)
                {
                    Logger.LogError("AdlxNative Dispose threw exception", ex);
                    pendingDisposeNative = native;
                    pendingDisposeDisposable = disposableNative;
                    _stage = CleanupStage.DisposePending;
                }

                if (disposeSucceeded)
                {
                    _stage = CleanupStage.Completed;
                }
            }
            else
            {
                _stage = CleanupStage.Completed;
            }

            _pendingNative = pendingNative;
            _pendingDisposable = pendingDisposable;
            _pendingDisposeNative = pendingDisposeNative;
            _pendingDisposeDisposable = pendingDisposeDisposable;

            return new CleanupResult(pendingNative, pendingDisposable, pendingDisposeNative, pendingDisposeDisposable);
        }

        public void Clear()
        {
            _pendingNative = null;
            _pendingDisposable = null;
            _pendingDisposeNative = null;
            _pendingDisposeDisposable = null;
            _stage = CleanupStage.None;
        }

        internal IAdlxNative? GetPendingNative() => _pendingNative;
        internal IDisposable? GetPendingDisposable() => _pendingDisposable;
        internal IAdlxNative? GetPendingDisposeNative() => _pendingDisposeNative;
        internal IDisposable? GetPendingDisposeDisposable() => _pendingDisposeDisposable;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            if (_stage == CleanupStage.DisposePending && _pendingDisposeNative != null && _pendingDisposeDisposable != null)
            {
                try
                {
                    _pendingDisposeDisposable.Dispose();
                }
                catch (Exception ex)
                {
                    Logger.LogError("AdlxPendingCleanup Dispose: pending Dispose threw exception", ex);
                }
            }
            else if (_stage == CleanupStage.TerminatePending && _pendingNative != null)
            {
                try
                {
                    int result = _pendingNative.Terminate();
                    if (result == 0 || result == ADLX_ORPHAN_OBJECTS)
                    {
                        _pendingDisposable?.Dispose();
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError("AdlxPendingCleanup Dispose: pending Terminate/Dispose threw exception", ex);
                }
            }

            _disposed = true;
        }

        public readonly struct CleanupResult
        {
            public readonly IAdlxNative? PendingNative;
            public readonly IDisposable? PendingDisposable;
            public readonly IAdlxNative? PendingDisposeNative;
            public readonly IDisposable? PendingDisposeDisposable;

            public CleanupResult(
                IAdlxNative? pendingNative,
                IDisposable? pendingDisposable,
                IAdlxNative? pendingDisposeNative,
                IDisposable? pendingDisposeDisposable)
            {
                PendingNative = pendingNative;
                PendingDisposable = pendingDisposable;
                PendingDisposeNative = pendingDisposeNative;
                PendingDisposeDisposable = pendingDisposeDisposable;
            }
        }
    }
}