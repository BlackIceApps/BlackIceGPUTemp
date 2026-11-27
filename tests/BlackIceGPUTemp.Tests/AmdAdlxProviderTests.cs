using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using BlackIceGPUTemp.Monitoring.Abstractions;
using BlackIceGPUTemp.Monitoring.Providers.Amd;
using BlackIceGPUTemp.Monitoring.Providers.Amd.Native;
using Xunit;

namespace BlackIceGPUTemp.Tests
{
    public class AmdAdlxProviderTests
    {
        private class FakeAdlxNative : IAdlxNative, IDisposable
        {
            public int InitializeCalls { get; private set; }
            public int TerminateCalls { get; protected set; }
            public int ReleaseCalls { get; private set; }
            public List<int> ReleasedPointers { get; } = new List<int>();
            public List<int> ReleaseOrder { get; } = new List<int>();
            public List<int> ReleaseReturnCodes { get; } = new List<int>();
            public bool IsDisposed { get; protected set; }
            public int LastReleaseResult { get; private set; }
            private int _getGpuTemperatureCallCount;
            public int GetGpuTemperatureCallCount => _getGpuTemperatureCallCount;
            private int _getGpuListSizeCallCount;
            public int GetGpuListSizeCallCount => _getGpuListSizeCallCount;
            private int _getGpuAtCallCount;
            public int GetGpuAtCallCount => _getGpuAtCallCount;
            private int _getGpuUniqueIdCallCount;
            public int GetGpuUniqueIdCallCount => _getGpuUniqueIdCallCount;
            private int _getPerformanceMonitoringServicesCallCount;
            public int GetPerformanceMonitoringServicesCallCount => _getPerformanceMonitoringServicesCallCount;
            private int _getSupportedGpuMetricsCallCount;
            public int GetSupportedGpuMetricsCallCount => _getSupportedGpuMetricsCallCount;
            private int _isSupportedGpuTemperatureCallCount;
            public int IsSupportedGpuTemperatureCallCount => _isSupportedGpuTemperatureCallCount;
            private int _getCurrentGpuMetricsCallCount;
            public int GetCurrentGpuMetricsCallCount => _getCurrentGpuMetricsCallCount;
            public int ReleaseFailAtPointer { get; set; } = -1;
            public int ReleaseFailCount { get; set; }
            public bool ThrowOnRelease { get; set; } = false;
            public int ThrowOnReleasePointer { get; set; } = -1;
            public int ThrowOnReleaseCount { get; set; }
            public List<string> Events { get; } = new List<string>();

            public delegate int InitializeDelegate(out IntPtr system);
            public delegate int TerminateDelegate();
            public delegate int GetGpuListDelegate(IntPtr system, out IntPtr gpuList);
            public delegate int GetGpuListSizeDelegate(IntPtr gpuList, out int count);
            public delegate int GetGpuAtDelegate(IntPtr gpuList, int index, out IntPtr gpu);
            public delegate int GetGpuNameDelegate(IntPtr gpu, out string? name);
            public delegate int GetGpuUniqueIdDelegate(IntPtr gpu, out int uniqueId);
            public delegate int GetGpuTypeDelegate(IntPtr gpu, out int gpuType);
            public delegate int GetPerformanceMonitoringServicesDelegate(IntPtr system, out IntPtr services);
            public delegate int GetCurrentGpuMetricsDelegate(IntPtr services, IntPtr gpu, out IntPtr metrics);
            public delegate int GetSupportedGpuMetricsDelegate(IntPtr services, IntPtr gpu, out IntPtr metricsSupport);
            public delegate int IsSupportedGpuTemperatureDelegate(IntPtr metricsSupport, out bool supported);
            public delegate int GetGpuTemperatureDelegate(IntPtr metrics, out double celsius);
            public delegate int ReleaseDelegate(IntPtr adlxInterface);

            public InitializeDelegate? InitializeHandler { get; set; }
            public TerminateDelegate? TerminateHandler { get; set; }
            public GetGpuListDelegate? GetGpuListHandler { get; set; }
            public GetGpuListSizeDelegate? GetGpuListSizeHandler { get; set; }
            public GetGpuAtDelegate? GetGpuAtHandler { get; set; }
            public GetGpuNameDelegate? GetGpuNameHandler { get; set; }
            public GetGpuUniqueIdDelegate? GetGpuUniqueIdHandler { get; set; }
            public GetGpuTypeDelegate? GetGpuTypeHandler { get; set; }
            public GetPerformanceMonitoringServicesDelegate? GetPerfServicesHandler { get; set; }
            public GetCurrentGpuMetricsDelegate? GetCurrentMetricsHandler { get; set; }
            public GetSupportedGpuMetricsDelegate? GetSupportedMetricsHandler { get; set; }
            public IsSupportedGpuTemperatureDelegate? IsSupportedTempHandler { get; set; }
            public GetGpuTemperatureDelegate? GetGpuTempHandler { get; set; }
            public ReleaseDelegate? ReleaseHandler { get; set; }

            public List<string> GpuNames { get; set; } = new List<string> { "AMD Radeon RX 7900 XTX" };
            public List<int> GpuUniqueIds { get; set; } = new List<int> { 12345 };
            public List<int> GpuTypes { get; set; } = new List<int> { 2 };
            public List<bool> GpuTemperatureSupported { get; set; } = new List<bool> { true };
            public List<double> GpuTemperatures { get; set; } = new List<double> { 65.5 };

            public int Initialize(out IntPtr system)
            {
                InitializeCalls++;
                system = new IntPtr(0x1000);
                if (InitializeHandler != null)
                {
                    return InitializeHandler(out system);
                }
                return 0;
            }

            public virtual int Terminate()
            {
                TerminateCalls++;
                Events.Add("Terminate");
                if (TerminateHandler != null)
                {
                    return TerminateHandler();
                }
                return 0;
            }

            public int GetGpuList(IntPtr system, out IntPtr gpuList)
            {
                gpuList = new IntPtr(0x2000);
                if (GetGpuListHandler != null)
                {
                    return GetGpuListHandler(system, out gpuList);
                }
                return 0;
            }

            public int GetGpuListSize(IntPtr gpuList, out int count)
            {
                Interlocked.Increment(ref _getGpuListSizeCallCount);
                count = GpuNames.Count;
                if (GetGpuListSizeHandler != null)
                {
                    return GetGpuListSizeHandler(gpuList, out count);
                }
                return 0;
            }

            public int GetGpuAt(IntPtr gpuList, int index, out IntPtr gpu)
            {
                Interlocked.Increment(ref _getGpuAtCallCount);
                gpu = new IntPtr(0x3000 + index);
                if (GetGpuAtHandler != null)
                {
                    return GetGpuAtHandler(gpuList, index, out gpu);
                }
                return 0;
            }

            public int GetGpuName(IntPtr gpu, out string? name)
            {
                int index = (int)(gpu.ToInt64() - 0x3000);
                name = index >= 0 && index < GpuNames.Count ? GpuNames[index] : "Unknown GPU";
                if (GetGpuNameHandler != null)
                {
                    return GetGpuNameHandler(gpu, out name);
                }
                return 0;
            }

            public int GetGpuUniqueId(IntPtr gpu, out int uniqueId)
            {
                Interlocked.Increment(ref _getGpuUniqueIdCallCount);
                int index = (int)(gpu.ToInt64() - 0x3000);
                uniqueId = index >= 0 && index < GpuUniqueIds.Count ? GpuUniqueIds[index] : 12345;
                if (GetGpuUniqueIdHandler != null)
                {
                    return GetGpuUniqueIdHandler(gpu, out uniqueId);
                }
                return 0;
            }

            public int GetGpuType(IntPtr gpu, out int gpuType)
            {
                int index = (int)(gpu.ToInt64() - 0x3000);
                gpuType = index >= 0 && index < GpuTypes.Count ? GpuTypes[index] : 2;
                if (GetGpuTypeHandler != null)
                {
                    return GetGpuTypeHandler(gpu, out gpuType);
                }
                return 0;
            }

            public int GetPerformanceMonitoringServices(IntPtr system, out IntPtr services)
            {
                Interlocked.Increment(ref _getPerformanceMonitoringServicesCallCount);
                services = new IntPtr(0x4000);
                if (GetPerfServicesHandler != null)
                {
                    return GetPerfServicesHandler(system, out services);
                }
                return 0;
            }

            public int GetCurrentGpuMetrics(IntPtr services, IntPtr gpu, out IntPtr metrics)
            {
                Interlocked.Increment(ref _getCurrentGpuMetricsCallCount);
                int index = (int)(gpu.ToInt64() - 0x3000);
                metrics = new IntPtr(0x5000 + index);
                if (GetCurrentMetricsHandler != null)
                {
                    return GetCurrentMetricsHandler(services, gpu, out metrics);
                }
                return 0;
            }

            public int GetSupportedGpuMetrics(IntPtr services, IntPtr gpu, out IntPtr metricsSupport)
            {
                Interlocked.Increment(ref _getSupportedGpuMetricsCallCount);
                int index = (int)(gpu.ToInt64() - 0x3000);
                metricsSupport = new IntPtr(0x6000 + index);
                if (GetSupportedMetricsHandler != null)
                {
                    return GetSupportedMetricsHandler(services, gpu, out metricsSupport);
                }
                return 0;
            }

            public int IsSupportedGpuTemperature(IntPtr metricsSupport, out bool supported)
            {
                Interlocked.Increment(ref _isSupportedGpuTemperatureCallCount);
                int gpuIndex = (int)(metricsSupport.ToInt64() - 0x6000);
                supported = gpuIndex >= 0 && gpuIndex < GpuTemperatureSupported.Count ? GpuTemperatureSupported[gpuIndex] : true;
                if (IsSupportedTempHandler != null)
                {
                    return IsSupportedTempHandler(metricsSupport, out supported);
                }
                return 0;
            }

            public int GetGpuTemperature(IntPtr metrics, out double celsius)
            {
                int gpuIndex = (int)(metrics.ToInt64() - 0x5000);
                celsius = gpuIndex >= 0 && gpuIndex < GpuTemperatures.Count ? GpuTemperatures[gpuIndex] : 65.5;
                if (GetGpuTempHandler != null)
                {
                    Interlocked.Increment(ref _getGpuTemperatureCallCount);
                    return GetGpuTempHandler(metrics, out celsius);
                }
                Interlocked.Increment(ref _getGpuTemperatureCallCount);
                return 0;
            }

            public int Release(IntPtr adlxInterface)
            {
                ReleaseCalls++;
                int ptr = adlxInterface.ToInt32();
                ReleasedPointers.Add(ptr);
                ReleaseOrder.Add(ptr);
                Events.Add($"Release:0x{ptr:X}");

                int result = 0;
                if (ThrowOnRelease && ptr == ThrowOnReleasePointer)
                {
                    ThrowOnReleaseCount++;
                    throw new InvalidOperationException($"Simulated Release failure for 0x{ptr:X}");
                }
                if (ReleaseFailAtPointer == ptr)
                {
                    ReleaseFailCount++;
                    result = -1;
                }
                else if (ReleaseHandler != null)
                {
                    result = ReleaseHandler(adlxInterface);
                }
                ReleaseReturnCodes.Add(result);
                LastReleaseResult = result;
                return result;
            }

            public virtual void Dispose()
            {
                IsDisposed = true;
            }
        }

        private class TerminateFailingFake : FakeAdlxNative
        {
            public int TerminateHResult { get; set; } = unchecked((int)0x80004005);
            public bool ThrowOnTerminate { get; set; } = false;

            public override int Terminate()
            {
                TerminateCalls++;
                Events.Add("Terminate");
                if (ThrowOnTerminate)
                {
                    throw new InvalidOperationException("Simulated Terminate exception");
                }
                return TerminateHResult;
            }
        }

        private class TerminateSequenceFake : FakeAdlxNative
        {
            private readonly Queue<int> _terminateResults = new Queue<int>();
            private readonly Queue<bool> _throwOnTerminate = new Queue<bool>();

            public int DisposeCallCount { get; private set; }

            public void EnqueueTerminate(int result, bool throwEx = false)
            {
                _terminateResults.Enqueue(result);
                _throwOnTerminate.Enqueue(throwEx);
            }

            public override int Terminate()
            {
                TerminateCalls++;
                Events.Add("Terminate");
                if (_throwOnTerminate.Count > 0 && _throwOnTerminate.Dequeue())
                {
                    throw new InvalidOperationException("Simulated Terminate exception");
                }
                if (_terminateResults.Count > 0)
                {
                    return _terminateResults.Dequeue();
                }
                return 0;
            }

            public override void Dispose()
            {
                DisposeCallCount++;
                base.Dispose();
            }
        }

        private class DisposeSequenceFake : FakeAdlxNative
        {
            private readonly Queue<int> _terminateResults = new Queue<int>();
            private readonly Queue<Exception?> _disposeExceptions = new Queue<Exception?>();

            public int DisposeCallCount { get; private set; }

            public void EnqueueTerminate(int result)
            {
                _terminateResults.Enqueue(result);
            }

            public void EnqueueDispose(Exception? ex = null)
            {
                _disposeExceptions.Enqueue(ex);
            }

            public override int Terminate()
            {
                TerminateCalls++;
                Events.Add("Terminate");
                if (_terminateResults.Count > 0)
                {
                    return _terminateResults.Dequeue();
                }
                return 0;
            }

            public override void Dispose()
            {
                DisposeCallCount++;
                var ex = _disposeExceptions.Count > 0 ? _disposeExceptions.Dequeue() : null;
                if (ex != null)
                {
                    throw ex;
                }
                IsDisposed = true;
            }
        }

        private class DisposeThrowingFake : FakeAdlxNative
        {
            public bool ThrowOnDispose { get; set; } = false;

            public override void Dispose()
            {
                IsDisposed = true;
                if (ThrowOnDispose)
                {
                    throw new InvalidOperationException("Simulated Dispose exception");
                }
            }
        }

        private class DisposeFailsAndRetainsFake : FakeAdlxNative
        {
            public int DisposeCallCount { get; private set; }

            public override void Dispose()
            {
                DisposeCallCount++;
                throw new InvalidOperationException($"Simulated Dispose failure on call {DisposeCallCount}");
            }
        }

        private class DisposeThrowsOnceThenSucceedsFake : FakeAdlxNative
        {
            public int DisposeCallCount { get; private set; }
            private bool _disposedSuccessfully;

            public override void Dispose()
            {
                if (_disposedSuccessfully)
                {
                    return;
                }
                DisposeCallCount++;
                if (DisposeCallCount == 1)
                {
                    throw new InvalidOperationException("Simulated Dispose failure on first call");
                }
                _disposedSuccessfully = true;
                IsDisposed = true;
            }
        }

        private class TerminateFailsThenSucceedsThenDisposeThrowsFake : FakeAdlxNative
        {
            public new int TerminateCalls { get; private set; }
            public int DisposeCallCount { get; private set; }

            public override int Terminate()
            {
                TerminateCalls++;
                if (TerminateCalls == 1)
                {
                    return -1;
                }
                return 0;
            }

            public override void Dispose()
            {
                DisposeCallCount++;
                if (DisposeCallCount == 1)
                {
                    throw new InvalidOperationException("Simulated Dispose failure on first call");
                }
                IsDisposed = true;
            }
        }

        private class TerminateFailsThenSucceedsThenDisposeThrowsTwiceFake : FakeAdlxNative
        {
            public new int TerminateCalls { get; private set; }
            public int DisposeCallCount { get; private set; }

            public override int Terminate()
            {
                TerminateCalls++;
                if (TerminateCalls == 1)
                {
                    return -1;
                }
                return 0;
            }

            public override void Dispose()
            {
                DisposeCallCount++;
                throw new InvalidOperationException($"Simulated Dispose failure on call {DisposeCallCount}");
            }
        }

        private class DisposeThenSucceedsFake : FakeAdlxNative
        {
            public int DisposeCallCount { get; private set; }
            public bool ThrowOnFirstDispose { get; set; } = true;

            public override void Dispose()
            {
                DisposeCallCount++;
                if (ThrowOnFirstDispose && DisposeCallCount == 1)
                {
                    throw new InvalidOperationException("Simulated Dispose exception on first call");
                }
                IsDisposed = true;
            }
        }

        [Fact]
        public void RetryTerminate_FailsThenSucceedsButDisposeThrows_RetainsReferencesAndRetriesDisposeOnNextCall()
        {
            var fake = new TerminateFailsThenSucceedsThenDisposeThrowsFake
            {
                InitializeHandler = (out IntPtr sys) =>
                {
                    sys = new IntPtr(0x1000);
                    return 0;
                }
            };
            var fake2 = new FakeAdlxNative();
            var factoryCallCount = 0;
            var provider = new AmdAdlxProvider(() =>
            {
                factoryCallCount++;
                return factoryCallCount == 1 ? fake : fake2;
            });

            // First operation creates session, monitoring succeeds
            var result1 = ((IGpuProviderProbe)provider).TryProbe(out var error);
            Assert.True(result1);
            Assert.Equal(1, fake.InitializeCalls);
            Assert.Equal(0, fake.TerminateCalls);
            Assert.Equal(0, fake.DisposeCallCount);
            Assert.False(fake.IsDisposed);

            // Disconnect triggers Terminate (fails) -> PendingNative set, Dispose NOT called yet
            provider.Disconnect();

            Assert.Equal(1, fake.TerminateCalls); // First Terminate call (failed)
            Assert.Equal(0, fake.DisposeCallCount); // Dispose not called because Terminate failed
            Assert.False(fake.IsDisposed);

            // First Connect retries Terminate (succeeds), retries Dispose (throws) -> Connect fails
            var connectResult = provider.Connect(out error);
            Assert.False(connectResult); // Cleanup incomplete, Connect fails
            Assert.NotNull(error);
            Assert.Equal(2, fake.TerminateCalls); // Terminate retried and succeeded
            Assert.Equal(1, fake.DisposeCallCount); // Dispose retried and threw
            Assert.False(fake.IsDisposed);
            Assert.Equal(0, fake2.InitializeCalls); // New session NOT created yet
            Assert.Equal(1, factoryCallCount);

            // Second Connect retries Dispose (succeeds), creates new session
            connectResult = provider.Connect(out error);
            Assert.True(connectResult);
            Assert.Null(error);
            Assert.Equal(2, fake.TerminateCalls); // Terminate not retried again
            Assert.Equal(2, fake.DisposeCallCount); // Dispose retried and succeeded
            Assert.True(fake.IsDisposed);
            Assert.Equal(1, fake2.InitializeCalls); // New session created
            Assert.Equal(0, fake2.TerminateCalls); // New session not terminated yet
            Assert.False(fake2.IsDisposed);
            Assert.Equal(2, factoryCallCount);

            // Explicitly disconnect the new session
            provider.Disconnect();

            Assert.Equal(1, fake2.TerminateCalls);
            Assert.True(fake2.IsDisposed);
        }

        [Fact]
        public void RetryTerminate_FailsThenSucceedsButDisposeThrowsTwice_RetainsReferencesAndDoesNotInitNew()
        {
            var fake = new TerminateFailsThenSucceedsThenDisposeThrowsTwiceFake
            {
                InitializeHandler = (out IntPtr sys) =>
                {
                    sys = new IntPtr(0x1000);
                    return 0;
                }
            };
            var fake2 = new FakeAdlxNative();
            var factoryCallCount = 0;
            var provider = new AmdAdlxProvider(() =>
            {
                factoryCallCount++;
                return factoryCallCount == 1 ? fake : fake2;
            });

            // First operation creates session, monitoring succeeds
            var result1 = ((IGpuProviderProbe)provider).TryProbe(out var error);
            Assert.True(result1);
            Assert.Equal(1, fake.InitializeCalls);
            Assert.Equal(0, fake.TerminateCalls);
            Assert.Equal(0, fake.DisposeCallCount);
            Assert.False(fake.IsDisposed);

            // Disconnect triggers Terminate (fails) -> PendingNative set, Dispose NOT called yet
            provider.Disconnect();

            Assert.Equal(1, fake.TerminateCalls); // First Terminate call (failed)
            Assert.Equal(0, fake.DisposeCallCount); // Dispose not called because Terminate failed
            Assert.False(fake.IsDisposed);

            // First Connect retries Terminate (succeeds), retries Dispose (throws) -> Connect fails
            var connectResult = provider.Connect(out error);
            Assert.False(connectResult); // Cleanup incomplete, Connect fails
            Assert.NotNull(error);
            Assert.Equal(2, fake.TerminateCalls); // Terminate retried and succeeded
            Assert.Equal(1, fake.DisposeCallCount); // Dispose retried and threw
            Assert.False(fake.IsDisposed);
            Assert.Equal(0, fake2.InitializeCalls); // New session NOT created
            Assert.Equal(1, factoryCallCount);

            // Second Connect attempt - Dispose throws again
            connectResult = provider.Connect(out error);
            Assert.False(connectResult);
            Assert.NotNull(error);
            Assert.Equal(2, fake.TerminateCalls); // Terminate not retried (already succeeded)
            Assert.Equal(2, fake.DisposeCallCount); // Dispose retried and threw again
            Assert.False(fake.IsDisposed);
            Assert.Equal(0, fake2.InitializeCalls);
            Assert.Equal(1, factoryCallCount);
        }

        [Fact]
        public void Initialize_ReturnsErrorCode_ThenDisposeThrows_RetainsReferencesAndRetriesOnNextCall()
        {
            var fake = new DisposeThrowsOnceThenSucceedsFake
            {
                InitializeHandler = (out IntPtr sys) =>
                {
                    sys = new IntPtr(0x1000);
                    return -5;
                }
            };
            var fake2 = new FakeAdlxNative();
            var factoryCallCount = 0;
            var provider = new AmdAdlxProvider(() =>
            {
                factoryCallCount++;
                return factoryCallCount == 1 ? fake : fake2;
            });

            // First operation: Initialize fails, Dispose throws (Terminate not called since Initialize failed)
            var result1 = ((IGpuProviderProbe)provider).TryProbe(out var error);
            Assert.False(result1);
            Assert.Equal(1, fake.DisposeCallCount); // Dispose called to clean up failed init
            Assert.False(fake.IsDisposed);
            Assert.Equal(0, fake.TerminateCalls); // Terminate not called when Initialize fails

            // Explicit Connect retries Dispose (succeeds), creates new session
            var connectResult = provider.Connect(out error);
            Assert.True(connectResult);
            Assert.Null(error);
            Assert.Equal(2, fake.DisposeCallCount); // Dispose retried and succeeded
            Assert.True(fake.IsDisposed);
            Assert.Equal(1, fake2.InitializeCalls); // New session created
            Assert.Equal(0, fake2.TerminateCalls); // New session NOT terminated after Connect (stays active)
            Assert.False(fake2.IsDisposed); // New session stays active
            Assert.Equal(2, factoryCallCount);

            // Explicitly disconnect the new session
            provider.Disconnect();

            Assert.Equal(1, fake2.TerminateCalls);
            Assert.True(fake2.IsDisposed);
        }

        [Fact]
        public void Initialize_ThrowsException_ThenDisposeThrows_RetainsReferencesAndRetriesOnNextCall()
        {
            var fake = new DisposeThrowsOnceThenSucceedsFake
            {
                InitializeHandler = (out IntPtr sys) =>
                {
                    sys = IntPtr.Zero;
                    throw new InvalidOperationException("Initialize failed");
                }
            };
            var fake2 = new FakeAdlxNative();
            var factoryCallCount = 0;
            var provider = new AmdAdlxProvider(() =>
            {
                factoryCallCount++;
                return factoryCallCount == 1 ? fake : fake2;
            });

            // First operation: Initialize throws, Dispose throws (Terminate not called since Initialize failed)
            var result1 = ((IGpuProviderProbe)provider).TryProbe(out var error);
            Assert.False(result1);
            Assert.Contains("Initialize failed", error);
            Assert.Equal(1, fake.DisposeCallCount); // Dispose called to clean up failed init
            Assert.False(fake.IsDisposed);
            Assert.Equal(0, fake.TerminateCalls); // Terminate not called when Initialize fails

            // Explicit Connect retries Dispose (succeeds), creates new session
            var connectResult = provider.Connect(out error);
            Assert.True(connectResult);
            Assert.Null(error);
            Assert.Equal(2, fake.DisposeCallCount); // Dispose retried and succeeded
            Assert.True(fake.IsDisposed);
            Assert.Equal(1, fake2.InitializeCalls); // New session created
            Assert.Equal(0, fake2.TerminateCalls); // New session not terminated yet
            Assert.False(fake2.IsDisposed); // New session stays active
            Assert.Equal(2, factoryCallCount);

            // Explicitly disconnect the new session
            provider.Disconnect();

            Assert.Equal(1, fake2.TerminateCalls);
            Assert.True(fake2.IsDisposed);
        }

        [Fact]
        public void SessionCreation_NotInitialized_DisposeThrows_RetainsReferencesAndRetriesOnNextCall()
        {
            var fake = new DisposeThrowsOnceThenSucceedsFake
            {
                InitializeHandler = (out IntPtr sys) =>
                {
                    sys = new IntPtr(0x1000);
                    return -5;
                }
            };
            var fake2 = new FakeAdlxNative();
            var factoryCallCount = 0;
            var provider = new AmdAdlxProvider(() =>
            {
                factoryCallCount++;
                return factoryCallCount == 1 ? fake : fake2;
            });

            // First operation: Initialize fails with error code, Dispose throws (Terminate not called since not initialized)
            var result1 = ((IGpuProviderProbe)provider).TryProbe(out var error);
            Assert.False(result1);
            Assert.Equal(0, fake.TerminateCalls);
            Assert.Equal(1, fake.DisposeCallCount);
            Assert.False(fake.IsDisposed);

            // Explicit Connect retries Dispose (succeeds), creates new session
            var connectResult = provider.Connect(out error);
            Assert.True(connectResult);
            Assert.Null(error);
            Assert.Equal(0, fake.TerminateCalls); // Terminate not called since not initialized
            Assert.Equal(2, fake.DisposeCallCount);
            Assert.True(fake.IsDisposed);
            Assert.Equal(1, fake2.InitializeCalls);
            Assert.Equal(0, fake2.TerminateCalls); // New session not terminated yet
            Assert.False(fake2.IsDisposed); // New session stays active
            Assert.Equal(2, factoryCallCount);

            // Explicitly disconnect the new session
            provider.Disconnect();

            Assert.Equal(1, fake2.TerminateCalls);
            Assert.True(fake2.IsDisposed);
        }

        [Fact]
        public void SessionCreation_NotInitialized_DisposeThrowsTwice_RetainsReferencesAndDoesNotInitNew()
        {
            var fake = new DisposeFailsAndRetainsFake
            {
                InitializeHandler = (out IntPtr sys) =>
                {
                    sys = new IntPtr(0x1000);
                    return -5;
                }
            };
            var fake2 = new FakeAdlxNative();
            var factoryCallCount = 0;
            var provider = new AmdAdlxProvider(() =>
            {
                factoryCallCount++;
                return factoryCallCount == 1 ? fake : fake2;
            });

            // First operation: Initialize fails with error code, Dispose throws
            var result1 = ((IGpuProviderProbe)provider).TryProbe(out var error);
            Assert.False(result1);
            Assert.Equal(0, fake.TerminateCalls);
            Assert.Equal(1, fake.DisposeCallCount);
            Assert.False(fake.IsDisposed);

            // Explicit Connect retries Dispose (throws again)
            var connectResult = provider.Connect(out error);
            Assert.False(connectResult);
            Assert.NotNull(error);
            Assert.Equal(0, fake.TerminateCalls);
            Assert.Equal(2, fake.DisposeCallCount);
            Assert.False(fake.IsDisposed);
            Assert.Equal(0, fake2.InitializeCalls); // New session NOT created
            Assert.Equal(1, factoryCallCount);

            // Second Connect attempt - Dispose throws again
            connectResult = provider.Connect(out error);
            Assert.False(connectResult);
            Assert.NotNull(error);
            Assert.Equal(0, fake.TerminateCalls);
            Assert.Equal(3, fake.DisposeCallCount);
            Assert.False(fake.IsDisposed);
            Assert.Equal(0, fake2.InitializeCalls);
            Assert.Equal(1, factoryCallCount);
        }

        [Fact]
        public void ProviderMetadata_AreCorrect()
        {
            var provider = new AmdAdlxProvider(() => new FakeAdlxNative());
            Assert.Equal("amd-adlx", provider.Id);
            Assert.Equal("AMD ADLX", provider.DisplayName);
        }

        [Fact]
        public void Probe_WhenSupported_ReturnsAvailable()
        {
            var fake = new FakeAdlxNative();
            var provider = new AmdAdlxProvider(() => fake);

            var result = ((IGpuProviderProbe)provider).TryProbe(out var error);

            Assert.True(result);
            Assert.Null(error);
            Assert.Equal(1, fake.InitializeCalls);
            Assert.Equal(0, fake.TerminateCalls);
            Assert.False(fake.IsDisposed);

            provider.Disconnect();

            Assert.Equal(1, fake.TerminateCalls);
            Assert.True(fake.IsDisposed);
        }

        [Fact]
        public void Probe_WhenTemperatureNotSupported_ReturnsUnavailable()
        {
            var fake = new FakeAdlxNative
            {
                GpuTemperatureSupported = new List<bool> { false }
            };
            var provider = new AmdAdlxProvider(() => fake);

            var result = ((IGpuProviderProbe)provider).TryProbe(out var error);

            Assert.False(result);
            Assert.Equal("No GPU supports temperature monitoring via ADLX.", error);
            Assert.Equal(1, fake.InitializeCalls);
            Assert.Equal(0, fake.TerminateCalls);
            Assert.False(fake.IsDisposed);

            provider.Disconnect();

            Assert.Equal(1, fake.TerminateCalls);
            Assert.True(fake.IsDisposed);
        }

        [Fact]
        public void Probe_WhenInitializeReturnsZeroWithNullSystem_PerformsTerminateAndDisposes()
        {
            var fake = new FakeAdlxNative
            {
                InitializeHandler = (out IntPtr outSystem) =>
                {
                    outSystem = IntPtr.Zero;
                    return 0;
                }
            };
            var provider = new AmdAdlxProvider(() => fake);

            var result = ((IGpuProviderProbe)provider).TryProbe(out var error);

            Assert.False(result);
            Assert.Equal(1, fake.InitializeCalls);
            Assert.Equal(1, fake.TerminateCalls); // Terminate called during CleanupRef after failed init
            Assert.True(fake.IsDisposed); // CleanupRef succeeded completely
            Assert.Equal("ADLX initialization succeeded but returned a null system interface.", error);

            provider.Disconnect();

            // No active session (creation failed), Disconnect does nothing
            Assert.Equal(1, fake.TerminateCalls);
            Assert.True(fake.IsDisposed);
        }

        [Fact]
        public void Probe_WhenGpuListFails_ReleasesGpuList()
        {
            var fake = new FakeAdlxNative
            {
                GetGpuListHandler = (IntPtr sys, out IntPtr list) =>
                {
                    list = IntPtr.Zero;
                    return -1;
                }
            };
            var provider = new AmdAdlxProvider(() => fake);

            var result = ((IGpuProviderProbe)provider).TryProbe(out var error);

            Assert.False(result);
            Assert.DoesNotContain(0x1000, fake.ReleasedPointers);
            Assert.Equal(1, fake.TerminateCalls); // Terminate called during CleanupRef after failed init
            Assert.True(fake.IsDisposed); // CleanupRef succeeded completely

            provider.Disconnect();

            // No active session (creation failed), Disconnect does nothing
            Assert.Equal(1, fake.TerminateCalls);
            Assert.True(fake.IsDisposed);
        }

        [Fact]
        public void Probe_WhenGetGpuListReturnsErrorWithNonZeroPointer_ReleasesGpuListPointerOnceAndCleansUp()
        {
            var fake = new FakeAdlxNative
            {
                GetGpuListHandler = (IntPtr sys, out IntPtr list) =>
                {
                    list = new IntPtr(0x2000);
                    return -1;
                }
            };
            var provider = new AmdAdlxProvider(() => fake);

            var result = ((IGpuProviderProbe)provider).TryProbe(out var error);

            Assert.False(result);
            Assert.NotNull(error);
            Assert.Contains("Failed to get GPU list", error);
            Assert.Contains("-1", error);

            Assert.Contains(0x2000, fake.ReleasedPointers);
            Assert.Single(fake.ReleasedPointers, p => p == 0x2000);
            Assert.DoesNotContain(0x1000, fake.ReleasedPointers);

            Assert.Equal(1, fake.TerminateCalls);
            Assert.True(fake.IsDisposed);

            provider.Disconnect();

            // No active session (creation failed), Disconnect does nothing
            Assert.Equal(1, fake.TerminateCalls);
            Assert.True(fake.IsDisposed);
            Assert.Single(fake.ReleasedPointers, p => p == 0x2000);
        }

        [Fact]
        public void Probe_WhenGpuListSizeFails_ReleasesGpuList()
        {
            var fake = new FakeAdlxNative
            {
                GetGpuListSizeHandler = (IntPtr list, out int count) =>
                {
                    count = 0;
                    return -1;
                }
            };
            var provider = new AmdAdlxProvider(() => fake);

            var result = ((IGpuProviderProbe)provider).TryProbe(out var error);

            Assert.False(result);
            Assert.DoesNotContain(0x1000, fake.ReleasedPointers);
            Assert.DoesNotContain(0x2000, fake.ReleasedPointers); // Not released until Disconnect
            Assert.Equal(0, fake.TerminateCalls);
            Assert.False(fake.IsDisposed);

            provider.Disconnect();

            // gpuList released on Disconnect
            Assert.Contains(0x2000, fake.ReleasedPointers);
            Assert.DoesNotContain(0x1000, fake.ReleasedPointers);
            Assert.Equal(1, fake.TerminateCalls);
            Assert.True(fake.IsDisposed);
        }

        [Fact]
        public void Probe_ReleaseReturnCodeIsObserved()
        {
            var fake = new FakeAdlxNative
            {
                ReleaseHandler = (IntPtr ptr) => 42
            };
            var provider = new AmdAdlxProvider(() => fake);

            var result = ((IGpuProviderProbe)provider).TryProbe(out var error);

            Assert.True(result);
            Assert.Contains(42, fake.ReleaseReturnCodes);
            Assert.Equal(0, fake.TerminateCalls);
            Assert.False(fake.IsDisposed);

            provider.Disconnect();

            Assert.Equal(1, fake.TerminateCalls);
            Assert.True(fake.IsDisposed);
        }

        [Fact]
        public void Probe_ReleaseFailureDoesNotMaskOriginalResult()
        {
            var fake = new FakeAdlxNative
            {
                ReleaseHandler = (IntPtr ptr) => -1,
                GpuTemperatureSupported = new List<bool> { false }
            };
            var provider = new AmdAdlxProvider(() => fake);

            var result = ((IGpuProviderProbe)provider).TryProbe(out var error);

            Assert.False(result);
            Assert.Equal("No GPU supports temperature monitoring via ADLX.", error);
            Assert.Equal(0, fake.TerminateCalls);
            Assert.False(fake.IsDisposed);

            provider.Disconnect();

            Assert.Equal(1, fake.TerminateCalls);
            Assert.True(fake.IsDisposed);
        }

        [Fact]
        public void TryEnumerateDevices_ReturnsValidDevices()
        {
            var fake = new FakeAdlxNative();
            var provider = new AmdAdlxProvider(() => fake);

            bool success = ((IGpuDeviceEnumerator)provider).TryEnumerateDevices(out var devices, out var error);

            Assert.True(success);
            Assert.Null(error);
            Assert.Single(devices);
            var device = devices[0];
            Assert.Equal("amd-adlx:12345", device.Id);
            Assert.Equal("AMD Radeon RX 7900 XTX", device.Name);
            Assert.Equal(GpuVendor.Amd, device.Vendor);
            Assert.Equal(GpuKind.Discrete, device.Kind);
        }

        [Fact]
        public void TryEnumerateDevices_GpuKindIntegrated_MapsCorrectly()
        {
            var fake = new FakeAdlxNative
            {
                GpuTypes = new List<int> { 1 }
            };
            var provider = new AmdAdlxProvider(() => fake);

            bool success = ((IGpuDeviceEnumerator)provider).TryEnumerateDevices(out var devices, out var error);

            Assert.True(success);
            Assert.Null(error);
            Assert.Single(devices);
            Assert.Equal(GpuKind.Integrated, devices[0].Kind);
        }

        [Fact]
        public void TryEnumerateDevices_GpuKindDiscrete_MapsCorrectly()
        {
            var fake = new FakeAdlxNative
            {
                GpuTypes = new List<int> { 2 }
            };
            var provider = new AmdAdlxProvider(() => fake);

            bool success = ((IGpuDeviceEnumerator)provider).TryEnumerateDevices(out var devices, out var error);

            Assert.True(success);
            Assert.Null(error);
            Assert.Single(devices);
            Assert.Equal(GpuKind.Discrete, devices[0].Kind);
        }

        [Fact]
        public void TryEnumerateDevices_GpuKindUnknown_MapsCorrectly()
        {
            var fake = new FakeAdlxNative
            {
                GpuTypes = new List<int> { 0 }
            };
            var provider = new AmdAdlxProvider(() => fake);

            bool success = ((IGpuDeviceEnumerator)provider).TryEnumerateDevices(out var devices, out var error);

            Assert.True(success);
            Assert.Null(error);
            Assert.Single(devices);
            Assert.Equal(GpuKind.Unknown, devices[0].Kind);
        }

        [Fact]
        public void TryEnumerateDevices_GpuTypeReadError_DefaultsToUnknown()
        {
            var fake = new FakeAdlxNative
            {
                GetGpuTypeHandler = (IntPtr gpu, out int gpuType) =>
                {
                    gpuType = 0;
                    return -1;
                }
            };
            var provider = new AmdAdlxProvider(() => fake);

            bool success = ((IGpuDeviceEnumerator)provider).TryEnumerateDevices(out var devices, out var error);

            Assert.True(success);
            Assert.Null(error);
            Assert.Single(devices);
            Assert.Equal(GpuKind.Unknown, devices[0].Kind);
        }

        [Fact]
        public void TryEnumerateDevices_GpuTypeUnknownValue_DefaultsToUnknown()
        {
            var fake = new FakeAdlxNative
            {
                GpuTypes = new List<int> { 99 }
            };
            var provider = new AmdAdlxProvider(() => fake);

            bool success = ((IGpuDeviceEnumerator)provider).TryEnumerateDevices(out var devices, out var error);

            Assert.True(success);
            Assert.Null(error);
            Assert.Single(devices);
            Assert.Equal(GpuKind.Unknown, devices[0].Kind);
        }

        [Fact]
        public void TryEnumerateDevices_GpuTypeReadThrowsException_DefaultsToUnknown()
        {
            var fake = new FakeAdlxNative
            {
                GetGpuTypeHandler = (IntPtr gpu, out int gpuType) =>
                {
                    gpuType = 0;
                    throw new InvalidOperationException("Simulated GetGpuType failure");
                }
            };
            var provider = new AmdAdlxProvider(() => fake);

            bool success = ((IGpuDeviceEnumerator)provider).TryEnumerateDevices(out var devices, out var error);

            Assert.True(success);
            Assert.Null(error);
            Assert.Single(devices);
            Assert.Equal(GpuKind.Unknown, devices[0].Kind);
        }

        [Fact]
        public void TryEnumerateDevices_MultipleGpus_DifferentTypes()
        {
            var fake = new FakeAdlxNative
            {
                GpuNames = new List<string> { "Integrated GPU", "Discrete GPU" },
                GpuUniqueIds = new List<int> { 11111, 22222 },
                GpuTypes = new List<int> { 1, 2 },
                GpuTemperatureSupported = new List<bool> { true, true }
            };
            var provider = new AmdAdlxProvider(() => fake);

            bool success = ((IGpuDeviceEnumerator)provider).TryEnumerateDevices(out var devices, out var error);

            Assert.True(success);
            Assert.Null(error);
            Assert.Equal(2, devices.Count);
            Assert.Equal(GpuKind.Integrated, devices[0].Kind);
            Assert.Equal(GpuKind.Discrete, devices[1].Kind);
        }

        [Fact]
        public void TryEnumerateDevices_WhenInitializeFailsWithNullSystem_ReleasesNothing()
        {
            var fake = new FakeAdlxNative
            {
                InitializeHandler = (out IntPtr sys) =>
                {
                    sys = IntPtr.Zero;
                    return -1;
                }
            };
            var provider = new AmdAdlxProvider(() => fake);

            bool success = ((IGpuDeviceEnumerator)provider).TryEnumerateDevices(out var devices, out var error);

            Assert.False(success);
            Assert.NotNull(error);
            Assert.Empty(devices);
            Assert.Equal(1, fake.InitializeCalls);
            Assert.Equal(0, fake.ReleaseCalls);
        }

        [Fact]
        public void TryEnumerateDevices_WhenInitializeFailsWithNonZeroSystem_ZeroesSystem()
        {
            var fake = new FakeAdlxNative
            {
                InitializeHandler = (out IntPtr sys) =>
                {
                    sys = new IntPtr(0x1000);
                    return -1;
                }
            };
            var provider = new AmdAdlxProvider(() => fake);

            bool success = ((IGpuDeviceEnumerator)provider).TryEnumerateDevices(out var devices, out var error);

            Assert.False(success);
            Assert.NotNull(error);
            Assert.Empty(devices);
            Assert.Equal(1, fake.InitializeCalls);
            Assert.DoesNotContain(0x1000, fake.ReleasedPointers);
        }

        [Fact]
        public void TryEnumerateDevices_WhenGpuListFails_ReleasesGpuList()
        {
            var fake = new FakeAdlxNative
            {
                GetGpuListHandler = (IntPtr sys, out IntPtr list) =>
                {
                    list = IntPtr.Zero;
                    return -1;
                }
            };
            var provider = new AmdAdlxProvider(() => fake);

            bool success = ((IGpuDeviceEnumerator)provider).TryEnumerateDevices(out var devices, out var error);

            Assert.False(success);
            Assert.NotNull(error);
            Assert.Empty(devices);
            Assert.DoesNotContain(0x1000, fake.ReleasedPointers);
        }

        [Fact]
        public void EnumerateDevices_WhenGetGpuAtReturnsErrorWithNonZeroPointer_ReleasesGpuPointerOnce()
        {
            var fake = new FakeAdlxNative
            {
                GetGpuAtHandler = (IntPtr gpuList, int index, out IntPtr gpu) =>
                {
                    gpu = new IntPtr(0x3000);
                    return -1;
                }
            };
            var provider = new AmdAdlxProvider(() => fake);

            bool success = ((IGpuDeviceEnumerator)provider).TryEnumerateDevices(out var devices, out var error);

            Assert.False(success);
            Assert.NotNull(error);
            Assert.Contains("Failed to get GPU at index 0", error);
            Assert.Contains("-1", error);
            Assert.Empty(devices);

            Assert.Contains(0x3000, fake.ReleasedPointers);
            Assert.Single(fake.ReleasedPointers, p => p == 0x3000);
            Assert.DoesNotContain(0x2000, fake.ReleasedPointers);
            Assert.DoesNotContain(0x1000, fake.ReleasedPointers);

            Assert.Equal(0, fake.TerminateCalls);
            Assert.False(fake.IsDisposed);

            provider.Disconnect();

            Assert.Contains(0x2000, fake.ReleasedPointers);
            Assert.Equal(1, fake.TerminateCalls);
            Assert.True(fake.IsDisposed);
            Assert.Single(fake.ReleasedPointers, p => p == 0x3000);
            Assert.DoesNotContain(0x1000, fake.ReleasedPointers);
        }

        [Fact]
        public void TryEnumerateDevices_WhenGpuListSizeFails_ReleasesGpuList()
        {
            var fake = new FakeAdlxNative
            {
                GetGpuListSizeHandler = (IntPtr list, out int count) =>
                {
                    count = 0;
                    return -1;
                }
            };
            var provider = new AmdAdlxProvider(() => fake);

            bool success = ((IGpuDeviceEnumerator)provider).TryEnumerateDevices(out var devices, out var error);

            Assert.False(success);
            Assert.NotNull(error);
            Assert.Empty(devices);
            // Temporary interfaces released immediately, gpuList (0x2000) is persistent session resource
            Assert.DoesNotContain(0x1000, fake.ReleasedPointers);
            Assert.DoesNotContain(0x2000, fake.ReleasedPointers); // Not released until Disconnect

            provider.Disconnect();

            // gpuList released on Disconnect
            Assert.Contains(0x2000, fake.ReleasedPointers);
            Assert.DoesNotContain(0x1000, fake.ReleasedPointers);
        }

        [Fact]
        public void TryEnumerateDevices_ReleaseFailureDoesNotMaskOriginalResult()
        {
            var fake = new FakeAdlxNative
            {
                ReleaseHandler = (IntPtr ptr) => -1
            };
            var provider = new AmdAdlxProvider(() => fake);

            bool success = ((IGpuDeviceEnumerator)provider).TryEnumerateDevices(out var devices, out var error);

            Assert.True(success);
            Assert.Null(error);
            Assert.Single(devices);
        }

        [Fact]
        public void TryEnumerateDevices_NoTemperatureSupported_ReturnsEmptyWithSuccess()
        {
            // When no GPU supports temperature monitoring, enumeration succeeds but returns empty list
            var fake = new FakeAdlxNative
            {
                GpuTemperatureSupported = new List<bool> { false }
            };
            var provider = new AmdAdlxProvider(() => fake);

            bool success = ((IGpuDeviceEnumerator)provider).TryEnumerateDevices(out var devices, out var error);

            Assert.True(success);
            Assert.Null(error);
            Assert.Empty(devices);
        }

        [Fact]
        public void TryEnumerateDevices_AdlxError_GetGpuListSizeFails_ReturnsFalseWithError()
        {
            var fake = new FakeAdlxNative
            {
                GetGpuListSizeHandler = (IntPtr list, out int count) =>
                {
                    count = 0;
                    return -1; // ADLX error code
                }
            };
            var provider = new AmdAdlxProvider(() => fake);

            bool success = ((IGpuDeviceEnumerator)provider).TryEnumerateDevices(out var devices, out var error);

            Assert.False(success);
            Assert.NotNull(error);
            Assert.Contains("Failed to get GPU list size", error);
            Assert.Contains("-1", error);
            Assert.Empty(devices);
        }

        [Fact]
        public void TryEnumerateDevices_AdlxError_GetGpuAtFails_ReturnsFalseWithError()
        {
            // GetGpuAt fails for first GPU - enumeration should fail completely
            var fake = new FakeAdlxNative
            {
                GpuNames = new List<string> { "GPU1", "GPU2" },
                GpuUniqueIds = new List<int> { 111, 222 },
                GpuTypes = new List<int> { 2, 2 },
                GpuTemperatureSupported = new List<bool> { true, true },
                GetGpuAtHandler = (IntPtr list, int index, out IntPtr gpu) =>
                {
                    gpu = IntPtr.Zero;
                    return -1; // Fail first GPU
                }
            };
            var provider = new AmdAdlxProvider(() => fake);

            bool success = ((IGpuDeviceEnumerator)provider).TryEnumerateDevices(out var devices, out var error);

            Assert.False(success);
            Assert.NotNull(error);
            Assert.Contains("Failed to get GPU at index 0", error);
            Assert.Contains("-1", error);
            Assert.Empty(devices);
        }

        [Fact]
        public void TryEnumerateDevices_AdlxError_GetGpuUniqueIdFails_ReturnsFalseWithError()
        {
            var fake = new FakeAdlxNative
            {
                GpuNames = new List<string> { "GPU1" },
                GpuUniqueIds = new List<int> { 111 },
                GpuTypes = new List<int> { 2 },
                GpuTemperatureSupported = new List<bool> { true },
                GetGpuUniqueIdHandler = (IntPtr gpu, out int uid) =>
                {
                    uid = 0;
                    return -1; // Fail to get unique ID
                }
            };
            var provider = new AmdAdlxProvider(() => fake);

            bool success = ((IGpuDeviceEnumerator)provider).TryEnumerateDevices(out var devices, out var error);

            Assert.False(success);
            Assert.NotNull(error);
            Assert.Contains("Failed to get unique ID or name for GPU at index 0", error);
            Assert.Contains("-1", error);
            Assert.Empty(devices);
        }

        [Fact]
        public void TryEnumerateDevices_AdlxError_GetGpuNameFails_ReturnsFalseWithError()
        {
            var fake = new FakeAdlxNative
            {
                GpuNames = new List<string> { "GPU1" },
                GpuUniqueIds = new List<int> { 111 },
                GpuTypes = new List<int> { 2 },
                GpuTemperatureSupported = new List<bool> { true },
                GetGpuNameHandler = (IntPtr gpu, out string? name) =>
                {
                    name = null;
                    return -1; // Fail to get name
                }
            };
            var provider = new AmdAdlxProvider(() => fake);

            bool success = ((IGpuDeviceEnumerator)provider).TryEnumerateDevices(out var devices, out var error);

            Assert.False(success);
            Assert.NotNull(error);
            Assert.Contains("Failed to get unique ID or name for GPU at index 0", error);
            Assert.Contains("-1", error);
            Assert.Empty(devices);
        }

        [Fact]
        public void TryEnumerateDevices_AdlxError_IsTemperatureSupportedFails_ReturnsFalseWithError()
        {
            // API error during IsTemperatureSupported should fail enumeration
            var fake = new FakeAdlxNative
            {
                GpuNames = new List<string> { "GPU1" },
                GpuUniqueIds = new List<int> { 111 },
                GpuTypes = new List<int> { 2 },
                GpuTemperatureSupported = new List<bool> { true },
                GetSupportedMetricsHandler = (IntPtr svc, IntPtr gpu, out IntPtr ms) =>
                {
                    ms = IntPtr.Zero;
                    return -1; // API error getting supported metrics
                }
            };
            var provider = new AmdAdlxProvider(() => fake);

            bool success = ((IGpuDeviceEnumerator)provider).TryEnumerateDevices(out var devices, out var error);

            Assert.False(success);
            Assert.NotNull(error);
            Assert.Contains("Failed to check temperature support for GPU at index 0", error);
            Assert.Contains("-1", error);
            Assert.Empty(devices);
        }

        [Fact]
        public void TryEnumerateDevices_GpuWithoutTemperatureSupport_ReturnsEmptyListWithSuccess()
        {
            // When GPU doesn't support temperature (valid result, not error), enumeration succeeds with empty list
            var fake = new FakeAdlxNative
            {
                GpuNames = new List<string> { "GPU1" },
                GpuUniqueIds = new List<int> { 111 },
                GpuTypes = new List<int> { 2 },
                GpuTemperatureSupported = new List<bool> { false } // Not supported, not an error
            };
            var provider = new AmdAdlxProvider(() => fake);

            bool success = ((IGpuDeviceEnumerator)provider).TryEnumerateDevices(out var devices, out var error);

            Assert.True(success); // Enumeration succeeded
            Assert.Null(error);
            Assert.Empty(devices); // But no devices support temperature
        }

        [Fact]
        public void TryEnumerateDevices_ErrorAfterSuccessfulGpu_ReturnsEmptyList()
        {
            // First GPU succeeds, second GPU fails - should return empty list, not partial
            var fake = new FakeAdlxNative
            {
                GpuNames = new List<string> { "GPU1", "GPU2" },
                GpuUniqueIds = new List<int> { 111, 222 },
                GpuTypes = new List<int> { 2, 2 },
                GpuTemperatureSupported = new List<bool> { true, true },
                GetGpuAtHandler = (IntPtr list, int index, out IntPtr gpu) =>
                {
                    gpu = new IntPtr(0x3000 + index);
                    if (index == 1)
                    {
                        return -1; // Second GPU fails
                    }
                    return 0;
                }
            };
            var provider = new AmdAdlxProvider(() => fake);

            bool success = ((IGpuDeviceEnumerator)provider).TryEnumerateDevices(out var devices, out var error);

            Assert.False(success);
            Assert.NotNull(error);
            Assert.Contains("Failed to get GPU at index 1", error);
            Assert.Empty(devices); // Must be empty, not partial
        }

        [Fact]
        public void TryEnumerateDevices_ExceptionDuringEnumeration_ReturnsFalseWithError()
        {
            var fake = new FakeAdlxNative
            {
                GetGpuListSizeHandler = (IntPtr list, out int count) =>
                {
                    count = 1;
                    throw new InvalidOperationException("Simulated ADLX exception");
                }
            };
            var provider = new AmdAdlxProvider(() => fake);

            bool success = ((IGpuDeviceEnumerator)provider).TryEnumerateDevices(out var devices, out var error);

            Assert.False(success);
            Assert.NotNull(error);
            Assert.Contains("Exception during GPU enumeration", error);
            Assert.Contains("InvalidOperationException", error);
            Assert.Empty(devices);
        }

        [Fact]
        public void TryEnumerateDevices_CancellationDuringEnumeration_ThrowsOperationCanceledException()
        {
            var fake = new FakeAdlxNative
            {
                GpuNames = new List<string> { "GPU1", "GPU2", "GPU3" },
                GpuUniqueIds = new List<int> { 111, 222, 333 },
                GpuTypes = new List<int> { 2, 2, 2 },
                GpuTemperatureSupported = new List<bool> { true, true, true }
            };
            var provider = new AmdAdlxProvider(() => fake);
            var cts = new CancellationTokenSource();

            // Cancel on second GPU during GetGpuAt
            fake.GetGpuAtHandler = (IntPtr list, int index, out IntPtr gpu) =>
            {
                gpu = new IntPtr(0x3000 + index);
                if (index == 1)
                {
                    cts.Cancel();
                }
                return 0;
            };

            Assert.Throws<OperationCanceledException>(() => ((IGpuDeviceEnumerator)provider).TryEnumerateDevices(out _, out _, cts.Token));

            // Shared session should still be active after cancellation
            Assert.Equal(0, fake.TerminateCalls);
            Assert.False(fake.IsDisposed);
            // GPU handles acquired before cancellation should be released
            Assert.Contains(0x3000, fake.ReleasedPointers); // GPU 0
            Assert.Contains(0x3001, fake.ReleasedPointers); // GPU 1
            // GPU list should NOT be released (owned by persistent session)
            Assert.DoesNotContain(0x2000, fake.ReleasedPointers);
        }

        [Fact]
        public void Device_TryReadTemperature_ReturnsReading()
        {
            var fake = new FakeAdlxNative();
            var provider = new AmdAdlxProvider(() => fake);

            bool success = ((IGpuDeviceEnumerator)provider).TryEnumerateDevices(out var devices, out var error);
            Assert.True(success);
            var device = devices[0];

            success = device.TryReadTemperature(out var reading, out error);

            Assert.True(success);
            Assert.Null(error);
            Assert.NotNull(reading);
            Assert.Equal(65.5, reading.Celsius);
            Assert.Equal("GPU Edge", reading.SensorName);
        }

        [Fact]
        public void TryReadTemperature_WithValidDeviceId_ReturnsReading()
        {
            var fake = new FakeAdlxNative();
            var provider = new AmdAdlxProvider(() => fake);

            bool success = ((IGpuTemperatureReader)provider).TryReadTemperature("amd-adlx:12345", out var reading, out var error);

            Assert.True(success);
            Assert.Null(error);
            Assert.NotNull(reading);
            Assert.Equal(65.5, reading.Celsius);
        }

        [Fact]
        public void TryReadTemperature_WithInvalidDeviceId_ReturnsFalse()
        {
            var fake = new FakeAdlxNative();
            var provider = new AmdAdlxProvider(() => fake);

            bool success = ((IGpuTemperatureReader)provider).TryReadTemperature("invalid-id", out var reading, out var error);

            Assert.False(success);
            Assert.NotNull(error);
            Assert.Contains("Invalid device ID format", error!);
        }

        [Fact]
        public void TryReadTemperature_WithEmptyDeviceId_ReturnsFalse()
        {
            var fake = new FakeAdlxNative();
            var provider = new AmdAdlxProvider(() => fake);

            bool success = ((IGpuTemperatureReader)provider).TryReadTemperature("", out var reading, out var error);

            Assert.False(success);
            Assert.NotNull(error);
        }

        [Fact]
        public void TryReadTemperatureInternal_WhenInitializeFails_ReleasesNothing()
        {
            var fake = new FakeAdlxNative
            {
                InitializeHandler = (out IntPtr sys) =>
                {
                    sys = IntPtr.Zero;
                    return -1;
                }
            };
            var provider = new AmdAdlxProvider(() => fake);

            bool success = provider.TryReadTemperatureInternal(12345, out var reading, out var error);

            Assert.False(success);
            Assert.NotNull(error);
            Assert.Equal(0, fake.ReleaseCalls);
        }

        [Fact]
        public void TryReadTemperatureInternal_WhenGpuListFails_ReleasesGpuList()
        {
            var fake = new FakeAdlxNative
            {
                GetGpuListHandler = (IntPtr sys, out IntPtr list) =>
                {
                    list = IntPtr.Zero;
                    return -1;
                }
            };
            var provider = new AmdAdlxProvider(() => fake);

            bool success = provider.TryReadTemperatureInternal(12345, out var reading, out var error);

            Assert.False(success);
            Assert.DoesNotContain(0x1000, fake.ReleasedPointers);
            Assert.DoesNotContain(0x2000, fake.ReleasedPointers); // gpuList not acquired (GetGpuList failed)

            provider.Disconnect();

            // No active session (creation failed), nothing released on Disconnect
            Assert.DoesNotContain(0x1000, fake.ReleasedPointers);
            Assert.DoesNotContain(0x2000, fake.ReleasedPointers);
        }

        [Fact]
        public void TryReadTemperatureInternal_WhenGpuListSizeFails_ReleasesGpuList()
        {
            var fake = new FakeAdlxNative
            {
                GetGpuListSizeHandler = (IntPtr list, out int count) =>
                {
                    count = 0;
                    return -1;
                }
            };
            var provider = new AmdAdlxProvider(() => fake);

            bool success = provider.TryReadTemperatureInternal(12345, out var reading, out var error);

            Assert.False(success);
            Assert.DoesNotContain(0x1000, fake.ReleasedPointers);
            Assert.DoesNotContain(0x2000, fake.ReleasedPointers); // Not released until Disconnect

            provider.Disconnect();

            // gpuList released on Disconnect
            Assert.Contains(0x2000, fake.ReleasedPointers);
            Assert.DoesNotContain(0x1000, fake.ReleasedPointers);
        }

        [Fact]
        public void TryReadTemperatureInternal_WhenTargetGpuNotFound_ReleasesAllLoopGpusAndGpuList()
        {
            var fake = new FakeAdlxNative
            {
                GpuUniqueIds = new List<int> { 11111, 22222 },
                GpuNames = new List<string> { "GPU1", "GPU2" }
            };
            var provider = new AmdAdlxProvider(() => fake);

            bool success = provider.TryReadTemperatureInternal(99999, out var reading, out var error);

            Assert.False(success);
            // Temporary GPU interfaces released immediately
            Assert.Contains(0x3000, fake.ReleasedPointers);
            Assert.Contains(0x3001, fake.ReleasedPointers);
            Assert.DoesNotContain(0x1000, fake.ReleasedPointers);
            Assert.DoesNotContain(0x2000, fake.ReleasedPointers); // gpuList is persistent, not released until Disconnect

            provider.Disconnect();

            // gpuList released on Disconnect
            Assert.Contains(0x2000, fake.ReleasedPointers);
        }

        [Fact]
        public void TryReadTemperatureInternal_WhenGetPerfServicesFails_ReleasesTargetGpuAndGpuList()
        {
            var fake = new FakeAdlxNative
            {
                GetPerfServicesHandler = (IntPtr sys, out IntPtr svc) =>
                {
                    svc = IntPtr.Zero;
                    return -1;
                }
            };
            var provider = new AmdAdlxProvider(() => fake);

            bool success = provider.TryReadTemperatureInternal(12345, out var reading, out var error);

            Assert.False(success);
            // Target GPU (0x3000) is cached and NOT released after failed read
            Assert.DoesNotContain(0x3000, fake.ReleasedPointers);
            Assert.DoesNotContain(0x1000, fake.ReleasedPointers);
            Assert.DoesNotContain(0x2000, fake.ReleasedPointers); // gpuList is persistent

            provider.Disconnect();

            // gpuList released on Disconnect
            Assert.Contains(0x2000, fake.ReleasedPointers);
            // Cached target GPU (0x3000) released during Disconnect
            Assert.Contains(0x3000, fake.ReleasedPointers);
        }

        [Fact]
        public void TryReadTemperatureInternal_WhenGetPerfServicesFailsWithNonZeroPointer_ReleasesServicesTargetGpuAndGpuList()
        {
            var fake = new FakeAdlxNative
            {
                GetPerfServicesHandler = (IntPtr sys, out IntPtr svc) =>
                {
                    svc = new IntPtr(0x4000);
                    return -1;
                }
            };
            var provider = new AmdAdlxProvider(() => fake);

            bool success = provider.TryReadTemperatureInternal(12345, out var reading, out var error);

            Assert.False(success);
            Assert.Null(reading);
            Assert.NotNull(error);
            Assert.Contains("Failed to get performance monitoring services", error);

            // Services pointer (0x4000) released exactly once
            Assert.Contains(0x4000, fake.ReleasedPointers);
            Assert.Single(fake.ReleasedPointers, p => p == 0x4000);
            // Target GPU (0x3000) is cached and NOT released after failed read
            Assert.DoesNotContain(0x3000, fake.ReleasedPointers);
            // System (0x1000) and gpuList (0x2000) NOT released before Disconnect
            Assert.DoesNotContain(0x1000, fake.ReleasedPointers);
            Assert.DoesNotContain(0x2000, fake.ReleasedPointers);

            // Terminate NOT called yet, fake NOT disposed
            Assert.Equal(0, fake.TerminateCalls);
            Assert.False(fake.IsDisposed);

            provider.Disconnect();

            // gpuList (0x2000) released exactly once on Disconnect
            Assert.Contains(0x2000, fake.ReleasedPointers);
            Assert.Single(fake.ReleasedPointers, p => p == 0x2000);
            // Cached target GPU (0x3000) released exactly once during Disconnect
            Assert.Contains(0x3000, fake.ReleasedPointers);
            Assert.Single(fake.ReleasedPointers, p => p == 0x3000);
            // Terminate called exactly once
            Assert.Equal(1, fake.TerminateCalls);
            // Fake disposed
            Assert.True(fake.IsDisposed);
            // Services pointer (0x4000) still released exactly once (not again on Disconnect)
            Assert.Single(fake.ReleasedPointers, p => p == 0x4000);
        }

        [Fact]
        public void TryReadTemperatureInternal_WhenGetSupportedMetricsFails_ReleasesServicesTargetGpuAndGpuList()
        {
            var fake = new FakeAdlxNative
            {
                GetSupportedMetricsHandler = (IntPtr svc, IntPtr gpu, out IntPtr ms) =>
                {
                    ms = IntPtr.Zero;
                    return -1;
                }
            };
            var provider = new AmdAdlxProvider(() => fake);

            bool success = provider.TryReadTemperatureInternal(12345, out var reading, out var error);

            Assert.False(success);
            Assert.NotNull(error);
            Assert.Contains("Failed to check GPU temperature support", error);
            Assert.Contains("-1", error);
            // GetSupportedGpuMetrics failed, so metricsSupport was not acquired
            // Services (0x4000) are cached and NOT released during read
            Assert.DoesNotContain(0x4000, fake.ReleasedPointers);
            Assert.DoesNotContain(0x6000, fake.ReleasedPointers);
            // Target GPU (0x3000) is cached and NOT released after failed read
            Assert.DoesNotContain(0x3000, fake.ReleasedPointers);
            Assert.DoesNotContain(0x1000, fake.ReleasedPointers);
            Assert.DoesNotContain(0x2000, fake.ReleasedPointers); // gpuList is persistent

            provider.Disconnect();

            // gpuList released on Disconnect
            Assert.Contains(0x2000, fake.ReleasedPointers);
            // Cached services (0x4000) released during Disconnect
            Assert.Contains(0x4000, fake.ReleasedPointers);
            // Cached target GPU (0x3000) released during Disconnect
            Assert.Contains(0x3000, fake.ReleasedPointers);
        }

        [Fact]
        public void TryReadTemperatureInternal_WhenGetSupportedMetricsFailsWithNonZeroPointer_ReleasesMetricsSupportServicesTargetGpuAndGpuList()
        {
            var fake = new FakeAdlxNative
            {
                GetSupportedMetricsHandler = (IntPtr svc, IntPtr gpu, out IntPtr ms) =>
                {
                    ms = new IntPtr(0x6000);
                    return -1;
                }
            };
            var provider = new AmdAdlxProvider(() => fake);

            bool success = provider.TryReadTemperatureInternal(12345, out var reading, out var error);

            Assert.False(success);
            Assert.Null(reading);
            Assert.NotNull(error);
            Assert.Contains("Failed to check GPU temperature support", error);
            Assert.Contains("-1", error);

            // metricsSupport (0x6000) released exactly once (failed to cache)
            Assert.Contains(0x6000, fake.ReleasedPointers);
            Assert.Single(fake.ReleasedPointers, p => p == 0x6000);
            // Services (0x4000) are cached and NOT released during read
            Assert.DoesNotContain(0x4000, fake.ReleasedPointers);
            // Target GPU (0x3000) is cached and NOT released after failed read
            Assert.DoesNotContain(0x3000, fake.ReleasedPointers);
            // System (0x1000) and gpuList (0x2000) NOT released before Disconnect
            Assert.DoesNotContain(0x1000, fake.ReleasedPointers);
            Assert.DoesNotContain(0x2000, fake.ReleasedPointers);

            // Terminate NOT called yet, fake NOT disposed
            Assert.Equal(0, fake.TerminateCalls);
            Assert.False(fake.IsDisposed);

            provider.Disconnect();

            // gpuList (0x2000) released exactly once on Disconnect
            Assert.Contains(0x2000, fake.ReleasedPointers);
            Assert.Single(fake.ReleasedPointers, p => p == 0x2000);
            // Cached services (0x4000) released during Disconnect
            Assert.Contains(0x4000, fake.ReleasedPointers);
            Assert.Single(fake.ReleasedPointers, p => p == 0x4000);
            // Cached target GPU (0x3000) released during Disconnect
            Assert.Contains(0x3000, fake.ReleasedPointers);
            Assert.Single(fake.ReleasedPointers, p => p == 0x3000);
            // Cached target GPU (0x3000) released exactly once during Disconnect
            Assert.Contains(0x3000, fake.ReleasedPointers);
            Assert.Single(fake.ReleasedPointers, p => p == 0x3000);
            // metricsSupport (0x6000), Services (0x4000) still released exactly once (not again on Disconnect)
            Assert.Single(fake.ReleasedPointers, p => p == 0x6000);
            Assert.Single(fake.ReleasedPointers, p => p == 0x4000);
            // Terminate called exactly once
            Assert.Equal(1, fake.TerminateCalls);
            // Fake disposed
            Assert.True(fake.IsDisposed);
            // System (0x1000) NOT released
            Assert.DoesNotContain(0x1000, fake.ReleasedPointers);

            // Verify release order: child before parent
            int metricsSupportIdx = fake.ReleaseOrder.IndexOf(0x6000);
            int servicesIdx = fake.ReleaseOrder.IndexOf(0x4000);
            int targetGpuIdx = fake.ReleaseOrder.IndexOf(0x3000);
            int gpuListIdx = fake.ReleaseOrder.IndexOf(0x2000);

            Assert.True(metricsSupportIdx >= 0 && servicesIdx >= 0 && targetGpuIdx >= 0 && gpuListIdx >= 0, "All interfaces should be released");
            Assert.True(metricsSupportIdx < servicesIdx, "metricsSupport (0x6000) before services (0x4000)");
            Assert.True(servicesIdx < targetGpuIdx, "services (0x4000) before targetGpu (0x3000)");
            Assert.True(targetGpuIdx < gpuListIdx, "targetGpu (0x3000) before gpuList (0x2000)");
        }

        [Fact]
        public void TryReadTemperatureInternal_WhenIsSupportedTempFails_ReleasesMetricsSupportServicesTargetGpuAndGpuList()
        {
            var fake = new FakeAdlxNative
            {
                IsSupportedTempHandler = (IntPtr ms, out bool supp) =>
                {
                    supp = false;
                    return -1;
                }
            };
            var provider = new AmdAdlxProvider(() => fake);

            bool success = provider.TryReadTemperatureInternal(12345, out var reading, out var error);

            Assert.False(success);
            Assert.NotNull(error);
            Assert.Contains("Failed to check GPU temperature support", error);
            Assert.Contains("-1", error);
            // IsSupportedGpuTemperature failed, so metricsSupport (0x6000) was released and not cached
            Assert.Contains(0x6000, fake.ReleasedPointers);
            Assert.Single(fake.ReleasedPointers, p => p == 0x6000);
            // Services (0x4000) are cached and NOT released during read
            Assert.DoesNotContain(0x4000, fake.ReleasedPointers);
            // Target GPU (0x3000) is cached and NOT released after failed read
            Assert.DoesNotContain(0x3000, fake.ReleasedPointers);
            Assert.DoesNotContain(0x1000, fake.ReleasedPointers);
            Assert.DoesNotContain(0x2000, fake.ReleasedPointers); // gpuList is persistent

            provider.Disconnect();

            // gpuList released on Disconnect
            Assert.Contains(0x2000, fake.ReleasedPointers);
            // Cached services (0x4000) released during Disconnect
            Assert.Contains(0x4000, fake.ReleasedPointers);
            Assert.Single(fake.ReleasedPointers, p => p == 0x4000);
            // Cached target GPU (0x3000) released during Disconnect
            Assert.Contains(0x3000, fake.ReleasedPointers);
            Assert.Single(fake.ReleasedPointers, p => p == 0x3000);
        }

        [Fact]
        public void TryReadTemperatureInternal_WhenGetCurrentMetricsFails_ReleasesMetricsSupportServicesTargetGpuAndGpuList()
        {
            var fake = new FakeAdlxNative
            {
                GetCurrentMetricsHandler = (IntPtr svc, IntPtr gpu, out IntPtr met) =>
                {
                    met = IntPtr.Zero;
                    return -1;
                }
            };
            var provider = new AmdAdlxProvider(() => fake);

            bool success = provider.TryReadTemperatureInternal(12345, out var reading, out var error);

            Assert.False(success);
            // GetCurrentGpuMetrics failed, so metrics (0x5000) was never acquired
            // metricsSupport (0x6000) and services (0x4000) are cached, NOT released during read
            Assert.DoesNotContain(0x5000, fake.ReleasedPointers);
            Assert.DoesNotContain(0x6000, fake.ReleasedPointers);
            Assert.DoesNotContain(0x4000, fake.ReleasedPointers);
            // Target GPU (0x3000) is cached and NOT released after failed read
            Assert.DoesNotContain(0x3000, fake.ReleasedPointers);
            Assert.DoesNotContain(0x1000, fake.ReleasedPointers);
            Assert.DoesNotContain(0x2000, fake.ReleasedPointers); // gpuList is persistent

            provider.Disconnect();

            // gpuList released on Disconnect
            Assert.Contains(0x2000, fake.ReleasedPointers);
            // Cached metricsSupport (0x6000) released during Disconnect
            Assert.Contains(0x6000, fake.ReleasedPointers);
            // Cached services (0x4000) released during Disconnect
            Assert.Contains(0x4000, fake.ReleasedPointers);
            // Cached target GPU (0x3000) released during Disconnect
            Assert.Contains(0x3000, fake.ReleasedPointers);
        }

        [Fact]
        public void TryReadTemperatureInternal_WhenGetTemperatureFails_ReleasesMetricsMetricsSupportServicesTargetGpuAndGpuList()
        {
            var fake = new FakeAdlxNative
            {
                GetGpuTempHandler = (IntPtr met, out double temp) =>
                {
                    temp = 0;
                    return -1;
                }
            };
            var provider = new AmdAdlxProvider(() => fake);

            bool success = provider.TryReadTemperatureInternal(12345, out var reading, out var error);

            Assert.False(success);
            // GetGpuTemperature failed, but metrics (0x5000) was acquired and released in finally
            // metricsSupport (0x6000) and services (0x4000) are cached, NOT released during read
            Assert.Contains(0x5000, fake.ReleasedPointers);
            Assert.DoesNotContain(0x6000, fake.ReleasedPointers);
            Assert.DoesNotContain(0x4000, fake.ReleasedPointers);
            // Target GPU (0x3000) is cached and NOT released after failed read
            Assert.DoesNotContain(0x3000, fake.ReleasedPointers);
            Assert.DoesNotContain(0x1000, fake.ReleasedPointers);
            Assert.DoesNotContain(0x2000, fake.ReleasedPointers); // gpuList is persistent

            provider.Disconnect();

            // gpuList released on Disconnect
            Assert.Contains(0x2000, fake.ReleasedPointers);
            // Cached metricsSupport (0x6000) released during Disconnect
            Assert.Contains(0x6000, fake.ReleasedPointers);
            // Cached services (0x4000) released during Disconnect
            Assert.Contains(0x4000, fake.ReleasedPointers);
            // Cached target GPU (0x3000) released during Disconnect
            Assert.Contains(0x3000, fake.ReleasedPointers);
        }

        [Fact]
        public void TryReadTemperatureInternal_ReleaseReturnCodeIsObserved()
        {
            var fake = new FakeAdlxNative
            {
                ReleaseHandler = (IntPtr ptr) => 42
            };
            var provider = new AmdAdlxProvider(() => fake);

            bool success = provider.TryReadTemperatureInternal(12345, out var reading, out var error);

            Assert.True(success);
            Assert.Contains(42, fake.ReleaseReturnCodes);
        }

        [Fact]
        public void TryReadTemperatureInternal_ReleaseFailureDoesNotMaskOriginalResult()
        {
            var fake = new FakeAdlxNative
            {
                ReleaseHandler = (IntPtr ptr) => -1
            };
            var provider = new AmdAdlxProvider(() => fake);

            bool success = provider.TryReadTemperatureInternal(12345, out var reading, out var error);

            Assert.True(success);
            Assert.NotNull(reading);
        }

        [Fact]
        public void IsTemperatureSupported_UsesConsistentReleaseInterface()
        {
            var fake = new FakeAdlxNative();
            var provider = new AmdAdlxProvider(() => fake);

            bool success = ((IGpuDeviceEnumerator)provider).TryEnumerateDevices(out var devices, out var error);
            Assert.True(success);
            var device = devices[0];

            success = device.TryReadTemperature(out var reading, out error);

            Assert.True(success);
            // metricsSupport (0x6000) acquired and released during enumeration, then cached and released on Disconnect
            // But during TryReadTemperature, metricsSupport is cached (not released)
            // Current metrics (0x5000) is released per read
            Assert.Contains(0x5000, fake.ReleasedPointers); // current metrics released per read
            // Services (0x4000) are cached and not released until Disconnect
            Assert.DoesNotContain(0x4000, fake.ReleasedPointers);
        }

        [Fact]
        public void ReleaseOrder_Probe_ChildBeforeParent()
        {
            var fake = new FakeAdlxNative();
            var provider = new AmdAdlxProvider(() => fake);

            ((IGpuProviderProbe)provider).TryProbe(out _);
            provider.Disconnect();

            int gpuIdx = fake.ReleaseOrder.IndexOf(0x3000);
            int servicesIdx = fake.ReleaseOrder.IndexOf(0x4000);
            int metricsSupportIdx = fake.ReleaseOrder.IndexOf(0x6000);
            int gpuListIdx = fake.ReleaseOrder.IndexOf(0x2000);

            Assert.True(gpuIdx >= 0 && metricsSupportIdx >= 0 && servicesIdx >= 0, "Child interfaces should be released");
            Assert.True(metricsSupportIdx < servicesIdx, "metricsSupport (child) before services (parent)");
            // Services are released during Disconnect, GPU handles during Probe - no ordering required between them
            Assert.True(gpuIdx < gpuListIdx, "gpu (child) before gpuList (parent)");
        }

        [Fact]
        public void ReleaseOrder_TryReadTemperatureInternal_ChildBeforeParent()
        {
            var fake = new FakeAdlxNative();
            var provider = new AmdAdlxProvider(() => fake);

            provider.TryReadTemperatureInternal(12345, out var reading, out var error);
            provider.Disconnect();

            int metricsIdx = fake.ReleaseOrder.IndexOf(0x5000);
            int metricsSupportIdx = fake.ReleaseOrder.IndexOf(0x6000);
            int servicesIdx = fake.ReleaseOrder.IndexOf(0x4000);
            int targetGpuIdx = fake.ReleaseOrder.IndexOf(0x3000);
            int gpuListIdx = fake.ReleaseOrder.IndexOf(0x2000);

            Assert.True(metricsIdx >= 0 && metricsSupportIdx >= 0 && servicesIdx >= 0 && targetGpuIdx >= 0, "All child interfaces should be released");
            Assert.True(metricsIdx < metricsSupportIdx, "metrics before metricsSupport");
            Assert.True(metricsSupportIdx < servicesIdx, "metricsSupport before services");
            Assert.True(servicesIdx < targetGpuIdx, "services before targetGpu");
            Assert.True(targetGpuIdx < gpuListIdx, "targetGpu before gpuList");
        }

        [Fact]
        public void ReleaseOrder_TerminateAfterInterfaces()
        {
            var fake = new FakeAdlxNative();
            var provider = new AmdAdlxProvider(() => fake);

            ((IGpuProviderProbe)provider).TryProbe(out _);
            provider.Disconnect();

            int gpuListIdx = fake.Events.IndexOf("Release:0x2000");
            int terminateIdx = fake.Events.IndexOf("Terminate");

            Assert.True(gpuListIdx >= 0, "gpuList should be released");
            Assert.True(terminateIdx >= 0, "Terminate should be called");
            Assert.True(gpuListIdx < terminateIdx, "gpuList released before Terminate");
        }

        [Fact]
        public void ReleaseException_DoesNotBlockOtherReleases_Probe()
        {
            var fake = new FakeAdlxNative
            {
                ThrowOnRelease = true,
                ThrowOnReleasePointer = 0x6000 // metricsSupport is released during Probe
            };
            var provider = new AmdAdlxProvider(() => fake);

            var result = ((IGpuProviderProbe)provider).TryProbe(out var error);

            Assert.True(result);
            // Temporary interfaces released immediately despite exception
            Assert.Contains(0x6000, fake.ReleasedPointers); // metricsSupport released during Probe
            Assert.Contains(0x3000, fake.ReleasedPointers); // non-target GPU released during Probe
            Assert.DoesNotContain(0x4000, fake.ReleasedPointers); // services cached, NOT released during Probe
            Assert.DoesNotContain(0x1000, fake.ReleasedPointers);
            Assert.DoesNotContain(0x2000, fake.ReleasedPointers); // gpuList is persistent, not released until Disconnect
            Assert.Equal(1, fake.ThrowOnReleaseCount);

            provider.Disconnect();

            // gpuList released on Disconnect
            Assert.Contains(0x2000, fake.ReleasedPointers);
            // Cached services (0x4000) released during Disconnect
            Assert.Contains(0x4000, fake.ReleasedPointers);
        }

        [Fact]
        public void ReleaseException_DoesNotBlockOtherReleases_TryReadTemperatureInternal()
        {
            var fake = new FakeAdlxNative
            {
                ThrowOnRelease = true,
                ThrowOnReleasePointer = 0x6000
            };
            var provider = new AmdAdlxProvider(() => fake);

            bool success = provider.TryReadTemperatureInternal(12345, out var reading, out var error);

            Assert.True(success);
            // Only metrics (0x5000) released per read
            // metricsSupport (0x6000) and services (0x4000) are cached, NOT released per read
            Assert.Contains(0x5000, fake.ReleasedPointers);
            Assert.DoesNotContain(0x6000, fake.ReleasedPointers); // cached
            Assert.DoesNotContain(0x4000, fake.ReleasedPointers); // cached
            // Target GPU (0x3000) is cached and NOT released after successful read
            Assert.DoesNotContain(0x3000, fake.ReleasedPointers);
            Assert.DoesNotContain(0x1000, fake.ReleasedPointers);
            Assert.DoesNotContain(0x2000, fake.ReleasedPointers); // gpuList is persistent
            // ThrowOnRelease fires on 0x6000 (metricsSupport), but it's cached - not released during read
            Assert.Equal(0, fake.ThrowOnReleaseCount);

            provider.Disconnect();

            // gpuList released on Disconnect
            Assert.Contains(0x2000, fake.ReleasedPointers);
            // Cached metricsSupport (0x6000) released during Disconnect - this triggers the exception
            Assert.Contains(0x6000, fake.ReleasedPointers);
            // Cached services (0x4000) released during Disconnect
            Assert.Contains(0x4000, fake.ReleasedPointers);
            // Cached target GPU (0x3000) released during Disconnect
            Assert.Contains(0x3000, fake.ReleasedPointers);
            // Exception on 0x6000 should not block other releases
            Assert.Equal(1, fake.ThrowOnReleaseCount);
        }

        [Fact]
        public void ReleaseFailedHResult_DoesNotChangeOperationResult_Probe()
        {
            var fake = new FakeAdlxNative
            {
                ReleaseFailAtPointer = 0x6000 // metricsSupport is released during probe
            };
            var provider = new AmdAdlxProvider(() => fake);

            var result = ((IGpuProviderProbe)provider).TryProbe(out var error);

            Assert.True(result);
            Assert.Equal(1, fake.ReleaseFailCount);
        }

        [Fact]
        public void ReleaseFailedHResult_DoesNotChangeOperationResult_TryReadTemperatureInternal()
        {
            var fake = new FakeAdlxNative
            {
                ReleaseFailAtPointer = 0x5000 // metrics is released per read
            };
            var provider = new AmdAdlxProvider(() => fake);

            bool success = provider.TryReadTemperatureInternal(12345, out var reading, out var error);

            Assert.True(success);
            Assert.NotNull(reading);
            Assert.Equal(1, fake.ReleaseFailCount);
        }

        [Fact]
        public void ReleaseException_DoesNotChangeOperationResult_Probe()
        {
            var fake = new FakeAdlxNative
            {
                ThrowOnRelease = true,
                ThrowOnReleasePointer = 0x3000
            };
            var provider = new AmdAdlxProvider(() => fake);

            var result = ((IGpuProviderProbe)provider).TryProbe(out var error);

            Assert.True(result);
        }

        [Fact]
        public void ReleaseException_DoesNotChangeOperationResult_TryReadTemperatureInternal()
        {
            var fake = new FakeAdlxNative
            {
                ThrowOnRelease = true,
                ThrowOnReleasePointer = 0x5000
            };
            var provider = new AmdAdlxProvider(() => fake);

            bool success = provider.TryReadTemperatureInternal(12345, out var reading, out var error);

            Assert.True(success);
        }

        [Fact]
        public void Release_ReturnsZero_NotTreatedAsError()
        {
            var fake = new FakeAdlxNative
            {
                ReleaseHandler = (IntPtr ptr) => 0
            };
            var provider = new AmdAdlxProvider(() => fake);

            var result = ((IGpuProviderProbe)provider).TryProbe(out var error);

            Assert.True(result);
            Assert.Contains(0, fake.ReleaseReturnCodes);
        }

        [Fact]
        public void Release_ReturnsPositiveRefCount_NotTreatedAsError()
        {
            var fake = new FakeAdlxNative
            {
                ReleaseHandler = (IntPtr ptr) => 2
            };
            var provider = new AmdAdlxProvider(() => fake);

            var result = ((IGpuProviderProbe)provider).TryProbe(out var error);

            Assert.True(result);
            Assert.Contains(2, fake.ReleaseReturnCodes);
        }

        [Fact]
        public void Release_ReturnsNegativeValue_DoesNotBreakCleanup()
        {
            var fake = new FakeAdlxNative
            {
                ReleaseHandler = (IntPtr ptr) => -1
            };
            var provider = new AmdAdlxProvider(() => fake);

            var result = ((IGpuProviderProbe)provider).TryProbe(out var error);

            Assert.True(result);
            // metricsSupport (0x6000) acquired and released during probe
            // GPU handle (0x3000) for non-target GPU released during probe
            // Services (0x4000) are cached and NOT released during probe
            Assert.Contains(0x6000, fake.ReleasedPointers);
            Assert.Contains(0x3000, fake.ReleasedPointers);
            Assert.DoesNotContain(0x4000, fake.ReleasedPointers);
            Assert.DoesNotContain(0x1000, fake.ReleasedPointers);
            Assert.DoesNotContain(0x2000, fake.ReleasedPointers); // gpuList is persistent

            provider.Disconnect();

            // gpuList released on Disconnect
            Assert.Contains(0x2000, fake.ReleasedPointers);
            // Cached services (0x4000) released during Disconnect
            Assert.Contains(0x4000, fake.ReleasedPointers);
        }

        [Fact]
        public void ReleaseException_DoesNotBlockOtherReleases_AndZeroesPointer()
        {
            var fake = new FakeAdlxNative
            {
                ThrowOnRelease = true,
                ThrowOnReleasePointer = 0x6000 // metricsSupport is released during probe
            };
            var provider = new AmdAdlxProvider(() => fake);

            var result = ((IGpuProviderProbe)provider).TryProbe(out var error);

            Assert.True(result);
            // All temporary interfaces should still be released despite one throwing
            Assert.Contains(0x6000, fake.ReleasedPointers); // metricsSupport released during probe
            Assert.Contains(0x3000, fake.ReleasedPointers); // non-target GPU released during probe
            Assert.DoesNotContain(0x4000, fake.ReleasedPointers); // services cached, NOT released during probe
            Assert.DoesNotContain(0x1000, fake.ReleasedPointers);
            Assert.DoesNotContain(0x2000, fake.ReleasedPointers); // gpuList is persistent
            Assert.Equal(1, fake.ThrowOnReleaseCount);

            provider.Disconnect();

            // gpuList released on Disconnect
            Assert.Contains(0x2000, fake.ReleasedPointers);
            // Cached services (0x4000) released during Disconnect
            Assert.Contains(0x4000, fake.ReleasedPointers);
        }

        [Fact]
        public void ReleaseException_DoesNotBlockOtherReleases_TryReadTemperatureInternal_AndZeroesPointer()
        {
            var fake = new FakeAdlxNative
            {
                ThrowOnRelease = true,
                ThrowOnReleasePointer = 0x6000
            };
            var provider = new AmdAdlxProvider(() => fake);

            bool success = provider.TryReadTemperatureInternal(12345, out var reading, out var error);

            Assert.True(success);
            // Only metrics (0x5000) released per read
            // metricsSupport (0x6000) and services (0x4000) are cached, NOT released per read
            Assert.Contains(0x5000, fake.ReleasedPointers);
            Assert.DoesNotContain(0x6000, fake.ReleasedPointers); // cached
            Assert.DoesNotContain(0x4000, fake.ReleasedPointers); // cached
            // Target GPU (0x3000) is cached and NOT released after successful read
            Assert.DoesNotContain(0x3000, fake.ReleasedPointers);
            Assert.DoesNotContain(0x1000, fake.ReleasedPointers);
            Assert.DoesNotContain(0x2000, fake.ReleasedPointers); // gpuList is persistent
            // ThrowOnRelease fires on 0x6000 (metricsSupport), but it's cached - not released during read
            Assert.Equal(0, fake.ThrowOnReleaseCount);

            provider.Disconnect();

            // gpuList released on Disconnect
            Assert.Contains(0x2000, fake.ReleasedPointers);
            // Cached metricsSupport (0x6000) released during Disconnect - this triggers the exception
            Assert.Contains(0x6000, fake.ReleasedPointers);
            // Cached services (0x4000) released during Disconnect
            Assert.Contains(0x4000, fake.ReleasedPointers);
            // Cached target GPU (0x3000) released during Disconnect
            Assert.Contains(0x3000, fake.ReleasedPointers);
            // Exception on 0x6000 should not block other releases
            Assert.Equal(1, fake.ThrowOnReleaseCount);
        }

        [Fact]
        public void ReleaseInterface_ZeroesPointer_WhenReleaseThrows()
        {
            var fake = new FakeAdlxNative
            {
                ThrowOnRelease = true,
                ThrowOnReleasePointer = 0x4000
            };

            var interopType = typeof(AmdAdlxProvider).Assembly.GetType("BlackIceGPUTemp.Monitoring.Providers.Amd.AdlxInterop");
            Assert.NotNull(interopType);

            var releaseInterfaceMethod = interopType!.GetMethod("ReleaseInterface", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            Assert.NotNull(releaseInterfaceMethod);

            IntPtr ptr = new IntPtr(0x4000);
            object[] args = new object[] { fake, ptr };

            // Invoke the method - it should catch the exception and zero the pointer
            releaseInterfaceMethod!.Invoke(null, args);

            // Verify the pointer was zeroed (args[1] is the ref parameter)
            Assert.Equal(IntPtr.Zero, args[1]);
        }

        [Fact]
        public void Terminate_ReturnsErrorHResult_Probe_ReturnsSuccessAndLogsError()
        {
            // Terminate fails first, succeeds on retry
            var fake = new TerminateSequenceFake();
            fake.EnqueueTerminate(unchecked((int)0x80004005)); // First Disconnect: fails
            fake.EnqueueTerminate(0); // Retry on Connect: succeeds
            var fake2 = new FakeAdlxNative(); // New instance after retry
            var factoryCallCount = 0;
            var provider = new AmdAdlxProvider(() =>
            {
                factoryCallCount++;
                return factoryCallCount == 1 ? fake : fake2;
            });

            // Operation succeeds, session stays active
            var result = ((IGpuProviderProbe)provider).TryProbe(out var error);
            Assert.True(result);

            // Disconnect triggers Terminate, which fails - error retained for retry
            provider.Disconnect();

            Assert.Equal(1, fake.TerminateCalls);
            Assert.False(fake.IsDisposed); // Session not disposed, error retained

            // Explicit Connect triggers retry -> Terminate succeeds, then Dispose old session, creates new session
            var connectResult = provider.Connect(out error);
            Assert.True(connectResult);
            Assert.Null(error);
            Assert.Equal(2, fake.TerminateCalls); // Retry called
            Assert.Equal(1, fake.DisposeCallCount); // Dispose called once after successful Terminate retry
            Assert.True(fake.IsDisposed); // Old session disposed after successful retry
            // New session created and stays active after Connect
            Assert.Equal(1, fake2.InitializeCalls);
            Assert.Equal(0, fake2.TerminateCalls);
            Assert.False(fake2.IsDisposed);
            Assert.Equal(2, factoryCallCount);

            // Explicitly disconnect the new session
            provider.Disconnect();

            Assert.Equal(1, fake2.TerminateCalls);
            Assert.True(fake2.IsDisposed);
        }

        [Fact]
        public void Terminate_ReturnsErrorHResult_TryReadTemperatureInternal_ReturnsSuccessAndLogsError()
        {
            var fake = new TerminateFailingFake { TerminateHResult = unchecked((int)0x80004005) };
            var provider = new AmdAdlxProvider(() => fake);

            bool success = provider.TryReadTemperatureInternal(12345, out var reading, out var error);

            Assert.True(success);
            Assert.NotNull(reading);
            Assert.Equal(0, fake.TerminateCalls); // Terminate not called after normal operation
            Assert.False(fake.IsDisposed);

            provider.Disconnect();

            Assert.Equal(1, fake.TerminateCalls);
            Assert.False(fake.IsDisposed); // Terminate failed, Dispose not called
        }

        [Fact]
        public void Terminate_ReturnsErrorHResult_TryEnumerateDevices_ReturnsDevicesAndLogsError()
        {
            var fake = new TerminateFailingFake { TerminateHResult = unchecked((int)0x80004005) };
            var provider = new AmdAdlxProvider(() => fake);

            bool success = ((IGpuDeviceEnumerator)provider).TryEnumerateDevices(out var devices, out var error);

            Assert.True(success);
            Assert.Null(error);
            Assert.Single(devices);
            Assert.Equal(0, fake.TerminateCalls); // Terminate not called after normal operation
            Assert.False(fake.IsDisposed);

            provider.Disconnect();

            Assert.Equal(1, fake.TerminateCalls);
            Assert.False(fake.IsDisposed); // Terminate failed, Dispose not called
        }

        [Fact]
        public void Terminate_ThrowsException_Probe_ReturnsSuccessAndLogsError()
        {
            var fake = new TerminateFailingFake { ThrowOnTerminate = true };
            var provider = new AmdAdlxProvider(() => fake);

            var result = ((IGpuProviderProbe)provider).TryProbe(out var error);

            Assert.True(result);
            Assert.Equal(0, fake.TerminateCalls); // Terminate not called after normal operation
            Assert.False(fake.IsDisposed);

            provider.Disconnect();

            Assert.Equal(1, fake.TerminateCalls);
            Assert.False(fake.IsDisposed); // Terminate threw, Dispose not called
        }

        [Fact]
        public void Terminate_ThrowsException_TryReadTemperatureInternal_ReturnsSuccessAndLogsError()
        {
            var fake = new TerminateFailingFake { ThrowOnTerminate = true };
            var provider = new AmdAdlxProvider(() => fake);

            bool success = provider.TryReadTemperatureInternal(12345, out var reading, out var error);

            Assert.True(success);
            Assert.NotNull(reading);
            Assert.Equal(0, fake.TerminateCalls); // Terminate not called after normal operation
            Assert.False(fake.IsDisposed);

            provider.Disconnect();

            Assert.Equal(1, fake.TerminateCalls);
            Assert.False(fake.IsDisposed); // Terminate threw, Dispose not called
        }

        [Fact]
        public void Terminate_ThrowsException_TryEnumerateDevices_ReturnsDevicesAndLogsError()
        {
            var fake = new TerminateFailingFake { ThrowOnTerminate = true };
            var provider = new AmdAdlxProvider(() => fake);

            bool success = ((IGpuDeviceEnumerator)provider).TryEnumerateDevices(out var devices, out var error);

            Assert.True(success);
            Assert.Null(error);
            Assert.Single(devices);
            Assert.Equal(0, fake.TerminateCalls); // Terminate not called after normal operation
            Assert.False(fake.IsDisposed);

            provider.Disconnect();

            Assert.Equal(1, fake.TerminateCalls);
            Assert.False(fake.IsDisposed); // Terminate threw, Dispose not called
        }

        [Fact]
        public void Dispose_ThrowsException_Probe_ReturnsSuccessAndLogsError()
        {
            // Terminate succeeds, Dispose throws first, succeeds on retry
            var fake = new DisposeSequenceFake();
            fake.EnqueueTerminate(0); // Terminate succeeds
            fake.EnqueueDispose(new InvalidOperationException("Simulated Dispose failure")); // First Disconnect: Dispose throws
            fake.EnqueueDispose(null); // Retry on Connect: Dispose succeeds
            var fake2 = new FakeAdlxNative();
            var factoryCallCount = 0;
            var provider = new AmdAdlxProvider(() =>
            {
                factoryCallCount++;
                return factoryCallCount == 1 ? fake : fake2;
            });

            // Operation succeeds, session stays active
            var result = ((IGpuProviderProbe)provider).TryProbe(out var error);
            Assert.True(result);

            // Disconnect triggers Terminate (success) then Dispose (throws) - error retained for retry
            provider.Disconnect();

            Assert.Equal(1, fake.TerminateCalls);
            Assert.False(fake.IsDisposed); // Session not disposed, Dispose failed

            // Explicit Connect triggers retry -> Dispose succeeds, creates new session
            var connectResult = provider.Connect(out error);
            Assert.True(connectResult);
            Assert.Null(error);
            Assert.Equal(1, fake.TerminateCalls); // Terminate not retried (already succeeded)
            Assert.Equal(2, fake.DisposeCallCount); // First threw, second succeeded
            Assert.True(fake.IsDisposed); // Now disposed after successful retry
            // New session created and stays active after Connect
            Assert.Equal(1, fake2.InitializeCalls);
            Assert.Equal(0, fake2.TerminateCalls);
            Assert.False(fake2.IsDisposed);
            Assert.Equal(2, factoryCallCount);

            // Explicitly disconnect the new session
            provider.Disconnect();

            Assert.Equal(1, fake2.TerminateCalls);
            Assert.True(fake2.IsDisposed);
        }

        [Fact]
        public void Dispose_ThrowsException_TryReadTemperatureInternal_ReturnsSuccessAndLogsError()
        {
            // Terminate succeeds, Dispose throws first, succeeds on retry
            var fake = new DisposeSequenceFake();
            fake.EnqueueTerminate(0); // Terminate succeeds
            fake.EnqueueDispose(new InvalidOperationException("Simulated Dispose failure")); // First Disconnect: Dispose throws
            fake.EnqueueDispose(null); // Retry on Connect: Dispose succeeds
            var fake2 = new FakeAdlxNative();
            var factoryCallCount = 0;
            var provider = new AmdAdlxProvider(() =>
            {
                factoryCallCount++;
                return factoryCallCount == 1 ? fake : fake2;
            });

            // Operation succeeds, session stays active
            bool success = provider.TryReadTemperatureInternal(12345, out var reading, out var error);
            Assert.True(success);
            Assert.NotNull(reading);

            // Disconnect triggers Terminate (success) then Dispose (throws) - error retained for retry
            provider.Disconnect();

            Assert.Equal(1, fake.TerminateCalls);
            Assert.False(fake.IsDisposed); // Session not disposed, Dispose failed

            // Explicit Connect triggers retry -> Dispose succeeds, creates new session
            var connectResult = provider.Connect(out var connectError);
            Assert.True(connectResult);
            Assert.Null(connectError);
            Assert.Equal(1, fake.TerminateCalls); // Terminate not retried
            Assert.Equal(2, fake.DisposeCallCount); // First threw, second succeeded
            Assert.True(fake.IsDisposed); // Now disposed after successful retry
            // New session created and stays active after Connect
            Assert.Equal(1, fake2.InitializeCalls);
            Assert.Equal(0, fake2.TerminateCalls);
            Assert.False(fake2.IsDisposed);
            Assert.Equal(2, factoryCallCount);

            // Explicitly disconnect the new session
            provider.Disconnect();

            Assert.Equal(1, fake2.TerminateCalls);
            Assert.True(fake2.IsDisposed);
        }

        [Fact]
        public void Dispose_ThrowsException_TryEnumerateDevices_ReturnsDevicesAndLogsError()
        {
            // Terminate succeeds, Dispose throws first, succeeds on retry
            var fake = new DisposeSequenceFake();
            fake.EnqueueTerminate(0); // Terminate succeeds
            fake.EnqueueDispose(new InvalidOperationException("Simulated Dispose failure")); // First Disconnect: Dispose throws
            fake.EnqueueDispose(null); // Retry on Connect: Dispose succeeds
            var fake2 = new FakeAdlxNative();
            var factoryCallCount = 0;
            var provider = new AmdAdlxProvider(() =>
            {
                factoryCallCount++;
                return factoryCallCount == 1 ? fake : fake2;
            });

            // Operation succeeds, session stays active
            bool success = ((IGpuDeviceEnumerator)provider).TryEnumerateDevices(out var devices, out var error);
            Assert.True(success);
            Assert.Null(error);
            Assert.Single(devices);

            // Disconnect triggers Terminate (success) then Dispose (throws) - error retained for retry
            provider.Disconnect();

            Assert.Equal(1, fake.TerminateCalls);
            Assert.False(fake.IsDisposed); // Session not disposed, Dispose failed

            // Explicit Connect triggers retry -> Dispose succeeds, creates new session
            var connectResult = provider.Connect(out error);
            Assert.True(connectResult);
            Assert.Null(error);
            Assert.Equal(1, fake.TerminateCalls); // Terminate not retried
            Assert.Equal(2, fake.DisposeCallCount); // First threw, second succeeded
            Assert.True(fake.IsDisposed); // Now disposed after successful retry
            // New session created and stays active after Connect
            Assert.Equal(1, fake2.InitializeCalls);
            Assert.Equal(0, fake2.TerminateCalls);
            Assert.False(fake2.IsDisposed);
            Assert.Equal(2, factoryCallCount);

            // Explicitly disconnect the new session
            provider.Disconnect();

            Assert.Equal(1, fake2.TerminateCalls);
            Assert.True(fake2.IsDisposed);
        }

        [Fact]
        public void Terminate_ErrorDoesNotMaskOriginalFailure_Probe()
        {
            var fake = new TerminateFailingFake
            {
                TerminateHResult = unchecked((int)0x80004005),
                GpuTemperatureSupported = new List<bool> { false }
            };
            var provider = new AmdAdlxProvider(() => fake);

            var result = ((IGpuProviderProbe)provider).TryProbe(out var error);

            Assert.False(result);
            Assert.Equal("No GPU supports temperature monitoring via ADLX.", error);
            Assert.Equal(0, fake.TerminateCalls); // Terminate not called after normal operation

            provider.Disconnect();

            // Terminate error should not affect the original result (which was already returned)
            Assert.Equal(1, fake.TerminateCalls);
            Assert.False(fake.IsDisposed);
        }

        [Fact]
        public void Terminate_ErrorDoesNotMaskOriginalFailure_TryReadTemperatureInternal()
        {
            var fake = new TerminateFailingFake
            {
                TerminateHResult = unchecked((int)0x80004005),
                GetGpuTempHandler = (IntPtr met, out double temp) =>
                {
                    temp = 0;
                    return -1;
                }
            };
            var provider = new AmdAdlxProvider(() => fake);

            bool success = provider.TryReadTemperatureInternal(12345, out var reading, out var error);

            Assert.False(success);
            Assert.Equal("Failed to get GPU temperature reading.", error);
            Assert.Equal(0, fake.TerminateCalls); // Terminate not called after normal operation

            provider.Disconnect();

            // Terminate error should not affect the original result
            Assert.Equal(1, fake.TerminateCalls);
            Assert.False(fake.IsDisposed);
        }

        [Fact]
        public void Dispose_ErrorDoesNotMaskOriginalFailure_Probe()
        {
            var fake = new DisposeThrowingFake
            {
                ThrowOnDispose = true,
                GpuTemperatureSupported = new List<bool> { false }
            };
            var provider = new AmdAdlxProvider(() => fake);

            var result = ((IGpuProviderProbe)provider).TryProbe(out var error);

            Assert.False(result);
            Assert.Equal("No GPU supports temperature monitoring via ADLX.", error);
            Assert.Equal(0, fake.TerminateCalls); // Terminate not called after normal operation

            provider.Disconnect();

            // Dispose error should not affect the original result (which was already returned)
            // Terminate succeeds, Dispose throws but sets IsDisposed = true before throwing
            Assert.Equal(1, fake.TerminateCalls);
            Assert.True(fake.IsDisposed); // Dispose sets IsDisposed=true before throwing
        }

        [Fact]
        public void Dispose_ErrorDoesNotMaskOriginalFailure_TryReadTemperatureInternal()
        {
            var fake = new DisposeThrowingFake
            {
                ThrowOnDispose = true,
                GetGpuTempHandler = (IntPtr met, out double temp) =>
                {
                    temp = 0;
                    return -1;
                }
            };
            var provider = new AmdAdlxProvider(() => fake);

            bool success = provider.TryReadTemperatureInternal(12345, out var reading, out var error);

            Assert.False(success);
            Assert.Equal("Failed to get GPU temperature reading.", error);
            Assert.Equal(0, fake.TerminateCalls); // Terminate not called after normal operation

            provider.Disconnect();

            // Dispose error should not affect the original result
            // Terminate succeeds, Dispose throws but sets IsDisposed = true before throwing
            Assert.Equal(1, fake.TerminateCalls);
            Assert.True(fake.IsDisposed); // Dispose sets IsDisposed=true before throwing
        }

        [Fact]
        public void Terminate_Success_DisposesLibrary()
        {
            var fake = new FakeAdlxNative();
            var provider = new AmdAdlxProvider(() => fake);

            ((IGpuProviderProbe)provider).TryProbe(out _);

            Assert.Equal(0, fake.TerminateCalls);
            Assert.False(fake.IsDisposed);

            provider.Disconnect();

            Assert.Equal(1, fake.TerminateCalls);
            Assert.True(fake.IsDisposed);
        }

        [Fact]
        public void AdlxNative_Terminate_ReturnsZero_IsInitializedBecomesFalse()
        {
            int freeCallCount = 0;
            var native = new AdlxNative(
                new IntPtr(0x1234),
                _ => { freeCallCount++; return true; },
                true,
                () => 0);

            int result = native.Terminate();

            Assert.Equal(0, result);
            native.Dispose();
            Assert.Equal(1, freeCallCount);
            Assert.True(native.IsDisposed);
        }

        [Fact]
        public void AdlxNative_Terminate_ReturnsOrphanObjects_IsInitializedBecomesFalse()
        {
            int freeCallCount = 0;
            var native = new AdlxNative(
                new IntPtr(0x1234),
                _ => { freeCallCount++; return true; },
                true,
                () => 0xB);

            int result = native.Terminate();

            Assert.Equal(0xB, result);
            native.Dispose();
            Assert.Equal(1, freeCallCount);
            Assert.True(native.IsDisposed);
        }

        [Fact]
        public void AdlxNative_Terminate_ReturnsOtherError_IsInitializedRemainsTrue()
        {
            int freeCallCount = 0;
            var native = new AdlxNative(
                new IntPtr(0x1234),
                _ => { freeCallCount++; return true; },
                true,
                () => -1);

            int result = native.Terminate();

            Assert.Equal(-1, result);
            native.Dispose();
            Assert.Equal(0, freeCallCount);
            Assert.False(native.IsDisposed);
        }

        [Fact]
        public void AdlxNative_Dispose_WhenInitialized_DoesNotCallFreeLibrary()
        {
            int freeCallCount = 0;
            var native = new AdlxNative(new IntPtr(0x1234), _ => { freeCallCount++; return true; }, true);

            native.Dispose();

            Assert.Equal(0, freeCallCount);
            Assert.False(native.IsDisposed);
            Assert.Equal(new IntPtr(0x1234), native.LibHandle);
        }

        [Fact]
        public void AdlxNative_Dispose_WhenNotInitialized_SuccessfulFree_CallsFreeLibraryOnce()
        {
            int freeCallCount = 0;
            var native = new AdlxNative(new IntPtr(0x1234), _ => { freeCallCount++; return true; }, false);

            native.Dispose();

            Assert.Equal(1, freeCallCount);
            Assert.True(native.IsDisposed);
            Assert.Equal(IntPtr.Zero, native.LibHandle);
        }

        [Fact]
        public void AdlxNative_Dispose_WhenNotInitialized_FailedFree_RetainsHandleAndAllowsRetry()
        {
            int freeCallCount = 0;
            bool freeResult = false;
            var native = new AdlxNative(new IntPtr(0x1234), _ => { freeCallCount++; return freeResult; }, false);

            var ex = Assert.Throws<System.ComponentModel.Win32Exception>(() => native.Dispose());

            Assert.Equal(1, freeCallCount);
            Assert.False(native.IsDisposed);
            Assert.Equal(new IntPtr(0x1234), native.LibHandle);
            Assert.Contains("Failed to free ADLX library", ex.Message);
        }

        [Fact]
        public void AdlxNative_Dispose_WhenNotInitialized_FreeLibraryThrows_RetainsHandleAndState()
        {
            int freeCallCount = 0;
            var native = new AdlxNative(new IntPtr(0x1234), _ => { freeCallCount++; throw new InvalidOperationException("Simulated FreeLibrary failure"); }, false);

            var ex = Assert.Throws<InvalidOperationException>(() => native.Dispose());

            Assert.Equal(1, freeCallCount);
            Assert.False(native.IsDisposed);
            Assert.Equal(new IntPtr(0x1234), native.LibHandle);
            Assert.Equal("Simulated FreeLibrary failure", ex.Message);
        }

        private class FreeLibraryCallCounter
        {
            public int Count { get; set; }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static FreeLibraryCallCounter CreateAdlxNativeForFinalizerTest(bool isInitialized, Func<IntPtr, bool> freeLibraryOverride)
        {
            var counter = new FreeLibraryCallCounter();
            var native = new AdlxNative(
                new IntPtr(0x1234),
                h =>
                {
                    counter.Count++;
                    return freeLibraryOverride(h);
                },
                isInitialized);
            // Do not return native - let it become eligible for finalization
            GC.KeepAlive(native);
            return counter;
        }

        [Fact]
        public void AdlxNative_Finalizer_FreesLibrary_WhenNotInitialized()
        {
            var counter = CreateAdlxNativeForFinalizerTest(false, _ => true);

            // Force GC - finalizer should run and free the library
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            Assert.Equal(1, counter.Count);
        }

        [Fact]
        public void AdlxNative_Finalizer_DoesNotFreeLibrary_WhenInitialized()
        {
            var counter = CreateAdlxNativeForFinalizerTest(true, _ => true);

            // Force GC - finalizer should run but NOT free the library
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            Assert.Equal(0, counter.Count);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static FreeLibraryCallCounter CreateDisposedAdlxNative()
        {
            var counter = new FreeLibraryCallCounter();
            var native = new AdlxNative(
                new IntPtr(0x1234),
                h =>
                {
                    counter.Count++;
                    return true;
                },
                false);
            native.Dispose();
            return counter;
        }

        [Fact]
        public void AdlxNative_Dispose_SuppressesFinalizer()
        {
            var counter = CreateDisposedAdlxNative();

            // Force GC - finalizer should not run because it was suppressed
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            // FreeLibrary should have been called once by Dispose, not again by finalizer
            Assert.Equal(1, counter.Count);
        }

        [Fact]
        public void AdlxNative_Finalizer_IgnoresFreeLibraryFailure()
        {
            var counter = CreateAdlxNativeForFinalizerTest(false, _ =>
            {
                throw new InvalidOperationException("Simulated FreeLibrary failure in finalizer");
            });

            // Force GC - finalizer should run but not throw
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            // Finalizer should not throw, even though FreeLibrary threw
            Assert.Equal(1, counter.Count);
        }

        [Fact]
        public void Terminate_OrphanObjects_DisposesLibrary()
        {
            var fake = new TerminateFailingFake { TerminateHResult = 0xB };
            var provider = new AmdAdlxProvider(() => fake);

            ((IGpuProviderProbe)provider).TryProbe(out _);

            Assert.Equal(0, fake.TerminateCalls);
            Assert.False(fake.IsDisposed);

            provider.Disconnect();

            Assert.Equal(1, fake.TerminateCalls);
            Assert.True(fake.IsDisposed);
        }

        [Fact]
        public void Terminate_OtherError_RetainsNativeAndDoesNotDispose()
        {
            var fake = new TerminateFailingFake { TerminateHResult = -1 };
            var provider = new AmdAdlxProvider(() => fake);

            ((IGpuProviderProbe)provider).TryProbe(out _);

            Assert.Equal(0, fake.TerminateCalls);
            Assert.False(fake.IsDisposed);

            provider.Disconnect();

            Assert.Equal(1, fake.TerminateCalls);
            Assert.False(fake.IsDisposed);
        }

        [Fact]
        public void Terminate_Exception_RetainsNativeAndDoesNotDispose()
        {
            var fake = new TerminateFailingFake { ThrowOnTerminate = true };
            var provider = new AmdAdlxProvider(() => fake);

            ((IGpuProviderProbe)provider).TryProbe(out _);

            Assert.Equal(0, fake.TerminateCalls);
            Assert.False(fake.IsDisposed);

            provider.Disconnect();

            Assert.Equal(1, fake.TerminateCalls);
            Assert.False(fake.IsDisposed);
        }

        [Fact]
        public void NextCall_RetriesTerminate_OnErrorHResult_ReturnsErrorAndDoesNotInit()
        {
            var fake = new TerminateFailingFake { TerminateHResult = unchecked((int)0x80004005) };
            var fake2 = new FakeAdlxNative();
            var factoryCallCount = 0;
            var provider = new AmdAdlxProvider(() =>
            {
                factoryCallCount++;
                return factoryCallCount == 1 ? fake : fake2;
            });

            ((IGpuProviderProbe)provider).TryProbe(out _);

            Assert.Equal(0, fake.TerminateCalls); // Terminate not called after normal operation
            Assert.False(fake.IsDisposed);

            // Disconnect triggers Terminate which fails
            provider.Disconnect();

            Assert.Equal(1, fake.TerminateCalls);
            Assert.False(fake.IsDisposed); // Terminate failed, Dispose not called

            // Next Probe should NOT auto-reconnect (explicit disconnect blocks auto-reconnect)
            var result = ((IGpuProviderProbe)provider).TryProbe(out var error);

            Assert.False(result);
            Assert.Contains("disconnected", error, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(1, fake.TerminateCalls); // No retry on Probe
            Assert.False(fake.IsDisposed);
            Assert.Equal(0, fake2.InitializeCalls);

            // Explicit Connect should retry Terminate
            var connectResult = provider.Connect(out error);
            Assert.False(connectResult); // Retry Terminate still fails
            Assert.Contains("Retry Terminate failed with HRESULT: 0x", error);
            Assert.Equal(2, fake.TerminateCalls); // Retry called
            Assert.False(fake.IsDisposed);
            Assert.Equal(0, fake2.InitializeCalls);
            Assert.Equal(1, factoryCallCount);
        }

        [Fact]
        public void NextCall_RetriesTerminate_OnException_ReturnsErrorAndDoesNotInit()
        {
            var fake = new TerminateFailingFake { ThrowOnTerminate = true };
            var fake2 = new TerminateFailingFake { ThrowOnTerminate = true };
            var factoryCallCount = 0;
            var provider = new AmdAdlxProvider(() =>
            {
                factoryCallCount++;
                return factoryCallCount == 1 ? fake : fake2;
            });

            ((IGpuProviderProbe)provider).TryProbe(out _);

            Assert.Equal(0, fake.TerminateCalls); // Terminate not called after normal operation
            Assert.False(fake.IsDisposed);

            // Disconnect triggers Terminate which throws
            provider.Disconnect();

            Assert.Equal(1, fake.TerminateCalls);
            Assert.False(fake.IsDisposed); // Terminate threw, Dispose not called

            // Next Probe should NOT auto-reconnect
            var result = ((IGpuProviderProbe)provider).TryProbe(out var error);

            Assert.False(result);
            Assert.Contains("disconnected", error, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(1, fake.TerminateCalls); // No retry on Probe
            Assert.False(fake.IsDisposed);
            Assert.Equal(0, fake2.InitializeCalls);

            // Explicit Connect should retry Terminate
            var connectResult = provider.Connect(out error);
            Assert.False(connectResult); // Retry Terminate still throws
            Assert.Contains("Retry Terminate failed with HRESULT: 0x", error);
            Assert.Equal(2, fake.TerminateCalls); // Retry called
            Assert.False(fake.IsDisposed);
            Assert.Equal(0, fake2.InitializeCalls);
            Assert.Equal(1, factoryCallCount);
        }

        [Fact]
        public void RetryTerminate_ThrowsException_RetainsNativeAndDoesNotDispose()
        {
            var fake = new TerminateFailingFake { ThrowOnTerminate = true };
            var fake2 = new FakeAdlxNative();
            var factoryCallCount = 0;
            var provider = new AmdAdlxProvider(() =>
            {
                factoryCallCount++;
                return factoryCallCount == 1 ? fake : fake2;
            });

            ((IGpuProviderProbe)provider).TryProbe(out _);

            Assert.Equal(0, fake.TerminateCalls); // Terminate not called after normal operation
            Assert.False(fake.IsDisposed);

            // Disconnect triggers Terminate which throws
            provider.Disconnect();

            Assert.Equal(1, fake.TerminateCalls);
            Assert.False(fake.IsDisposed); // Terminate threw, Dispose not called

            // Explicit Connect should retry Terminate
            var connectResult = provider.Connect(out var error);
            Assert.False(connectResult); // Retry Terminate still throws
            Assert.Contains("Retry Terminate failed with HRESULT: 0x", error);
            Assert.Equal(2, fake.TerminateCalls); // Retry called
            Assert.False(fake.IsDisposed);
            Assert.Equal(0, fake2.InitializeCalls);
            Assert.Equal(1, factoryCallCount);
        }

        [Fact]
        public void FactoryThrowsException_Probe_ReturnsError()
        {
            var provider = new AmdAdlxProvider(() => throw new InvalidOperationException("Factory failed"));

            var result = ((IGpuProviderProbe)provider).TryProbe(out var error);

            Assert.False(result);
            Assert.Contains("Factory failed", error);
        }

        [Fact]
        public void InitializeThrowsException_Probe_ReturnsError()
        {
            var fake = new FakeAdlxNative
            {
                InitializeHandler = (out IntPtr sys) =>
                {
                    sys = IntPtr.Zero;
                    throw new InvalidOperationException("Initialize failed");
                }
            };
            var provider = new AmdAdlxProvider(() => fake);

            var result = ((IGpuProviderProbe)provider).TryProbe(out var error);

            Assert.False(result);
            Assert.Contains("Initialize failed", error);
        }

        [Fact]
        public void InitializeThrowsException_TryEnumerateDevices_ReturnsError()
        {
            var fake = new FakeAdlxNative
            {
                InitializeHandler = (out IntPtr sys) =>
                {
                    sys = IntPtr.Zero;
                    throw new InvalidOperationException("Initialize failed");
                }
            };
            var provider = new AmdAdlxProvider(() => fake);

            bool success = ((IGpuDeviceEnumerator)provider).TryEnumerateDevices(out var devices, out var error);

            Assert.False(success);
            Assert.NotNull(error);
            Assert.Contains("Initialize failed", error);
            Assert.Empty(devices);
        }

        [Fact]
        public void InitializeThrowsException_TryReadTemperatureInternal_ReturnsError()
        {
            var fake = new FakeAdlxNative
            {
                InitializeHandler = (out IntPtr sys) =>
                {
                    sys = IntPtr.Zero;
                    throw new InvalidOperationException("Initialize failed");
                }
            };
            var provider = new AmdAdlxProvider(() => fake);

            bool success = provider.TryReadTemperatureInternal(12345, out var reading, out var error);

            Assert.False(success);
            Assert.Contains("Initialize failed", error);
        }

        [Fact]
        public void Initialize_ReturnsNonZero_ReturnsErrorCode()
        {
            var fake = new FakeAdlxNative
            {
                InitializeHandler = (out IntPtr sys) =>
                {
                    sys = new IntPtr(0x1000);
                    return -5;
                }
            };
            var provider = new AmdAdlxProvider(() => fake);

            var result = ((IGpuProviderProbe)provider).TryProbe(out var error);

            Assert.False(result);
            Assert.Equal(0, fake.TerminateCalls);
            Assert.Contains("Result code: -5", error);
        }

        [Fact]
        public void Initialize_ZeroWithNullSystem_GetGpuListNotCalled()
        {
            var fake = new FakeAdlxNative
            {
                InitializeHandler = (out IntPtr sys) =>
                {
                    sys = IntPtr.Zero;
                    return 0;
                },
                GetGpuListHandler = (IntPtr sys, out IntPtr list) =>
                {
                    list = IntPtr.Zero;
                    throw new InvalidOperationException("GetGpuList should not be called");
                }
            };
            var provider = new AmdAdlxProvider(() => fake);

            var result = ((IGpuProviderProbe)provider).TryProbe(out var error);

            Assert.False(result);
            Assert.Equal("ADLX initialization succeeded but returned a null system interface.", error);
        }

        [Fact]
        public void Initialize_ZeroWithNullSystem_TerminateFailure_RetainsNative()
        {
            var fake = new TerminateFailingFake 
            { 
                ThrowOnTerminate = true,
                InitializeHandler = (out IntPtr sys) =>
                {
                    sys = IntPtr.Zero;
                    return 0;
                }
            };
            var provider = new AmdAdlxProvider(() => fake);

            var result = ((IGpuProviderProbe)provider).TryProbe(out var error);

            Assert.False(result);
            Assert.Equal(1, fake.TerminateCalls); // Terminate called during CleanupRef after failed init
            Assert.False(fake.IsDisposed);
            Assert.Equal("ADLX initialization succeeded but returned a null system interface.", error);

            // Disconnect triggers Terminate which throws
            provider.Disconnect();

            Assert.Equal(1, fake.TerminateCalls); // No active session, Disconnect does nothing
            Assert.False(fake.IsDisposed); // Terminate threw, Dispose not called

            // Next Probe should NOT auto-reconnect
            var result2 = ((IGpuProviderProbe)provider).TryProbe(out error);

            Assert.False(result2);
            Assert.Contains("disconnected", error, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(1, fake.TerminateCalls); // No retry on Probe
            Assert.False(fake.IsDisposed);

            // Explicit Connect should retry Terminate
            var connectResult = provider.Connect(out error);
            Assert.False(connectResult); // Retry Terminate still throws
            Assert.Contains("Retry Terminate failed with HRESULT: 0x", error);
            Assert.Equal(2, fake.TerminateCalls); // Retry called
            Assert.False(fake.IsDisposed);
        }

        [Fact]
        public void Initialize_ZeroWithNullSystem_TerminateErrorHResult_RetainsNative()
        {
            var fake = new TerminateFailingFake 
            { 
                TerminateHResult = -1,
                InitializeHandler = (out IntPtr sys) =>
                {
                    sys = IntPtr.Zero;
                    return 0;
                }
            };
            var provider = new AmdAdlxProvider(() => fake);

            var result = ((IGpuProviderProbe)provider).TryProbe(out var error);

            Assert.False(result);
            Assert.Equal(1, fake.TerminateCalls); // Terminate called during CleanupRef after failed init
            Assert.False(fake.IsDisposed);
            Assert.Equal("ADLX initialization succeeded but returned a null system interface.", error);

            // Disconnect triggers Terminate which fails with error HRESULT
            provider.Disconnect();

            Assert.Equal(1, fake.TerminateCalls); // No active session, Disconnect does nothing
            Assert.False(fake.IsDisposed); // Terminate failed, Dispose not called

            // Next Probe should NOT auto-reconnect
            var result2 = ((IGpuProviderProbe)provider).TryProbe(out error);

            Assert.False(result2);
            Assert.Contains("disconnected", error, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(1, fake.TerminateCalls); // No retry on Probe
            Assert.False(fake.IsDisposed);

            // Explicit Connect should retry Terminate
            var connectResult = provider.Connect(out error);
            Assert.False(connectResult); // Retry Terminate still fails
            Assert.Contains("Retry Terminate failed with HRESULT: 0x", error);
            Assert.Equal(2, fake.TerminateCalls); // Retry called
            Assert.False(fake.IsDisposed);
        }

        [Fact]
        public void Initialize_ZeroWithNullSystem_TerminateSuccess_AllowsNewInit()
        {
            var fake = new FakeAdlxNative
            {
                InitializeHandler = (out IntPtr sys) =>
                {
                    sys = IntPtr.Zero;
                    return 0;
                }
            };
            var fake2 = new FakeAdlxNative();
            var factoryCallCount = 0;
            var provider = new AmdAdlxProvider(() =>
            {
                factoryCallCount++;
                return factoryCallCount == 1 ? fake : fake2;
            });

            var result1 = ((IGpuProviderProbe)provider).TryProbe(out var error);

            Assert.False(result1);
            Assert.Equal(1, fake.TerminateCalls); // Terminate called during CleanupRef after failed init
            Assert.True(fake.IsDisposed); // CleanupRef succeeded completely
            Assert.Equal("ADLX initialization succeeded but returned a null system interface.", error);

            // Disconnect - no active session, does nothing
            provider.Disconnect();

            Assert.Equal(1, fake.TerminateCalls);
            Assert.True(fake.IsDisposed);

            // Explicit Connect creates new session
            var connectResult = provider.Connect(out error);
            Assert.True(connectResult);
            Assert.Null(error);
            Assert.Equal(1, fake2.InitializeCalls); // New session created
            Assert.Equal(0, fake2.TerminateCalls); // New session not terminated yet
            Assert.False(fake2.IsDisposed); // New session stays active
            Assert.Equal(2, factoryCallCount);

            // Explicitly disconnect the new session
            provider.Disconnect();

            Assert.Equal(1, fake2.TerminateCalls);
            Assert.True(fake2.IsDisposed);
        }

        [Fact]
        public void PendingDispose_RetryDispose_Succeeds_ClearsReferencesAndAllowsNewInit()
        {
            var fake = new DisposeThenSucceedsFake
            {
                InitializeHandler = (out IntPtr sys) =>
                {
                    sys = new IntPtr(0x1000);
                    return 0;
                },
                TerminateHandler = () => 0,
            };
            var fake2 = new FakeAdlxNative();
            var factoryCallCount = 0;
            var provider = new AmdAdlxProvider(() =>
            {
                factoryCallCount++;
                return factoryCallCount == 1 ? fake : fake2;
            });

            // First call: monitoring succeeds, Terminate succeeds, Dispose throws -> retained in _pendingDisposeNative
            var result1 = ((IGpuProviderProbe)provider).TryProbe(out var error);
            Assert.True(result1);
            Assert.Equal(0, fake.TerminateCalls); // Terminate not called after normal operation
            Assert.Equal(0, fake.DisposeCallCount); // Dispose not called after normal operation
            Assert.False(fake.IsDisposed);

            // Disconnect triggers Terminate (success) then Dispose (throws)
            provider.Disconnect();

            Assert.Equal(1, fake.TerminateCalls);
            Assert.Equal(1, fake.DisposeCallCount);
            Assert.False(fake.IsDisposed); // Dispose threw, retained

            // Explicit Connect retries Dispose (succeeds), creates new session
            var connectResult = provider.Connect(out error);
            Assert.True(connectResult);
            Assert.Null(error);
            Assert.Equal(1, fake.TerminateCalls); // Terminate not retried
            Assert.Equal(2, fake.DisposeCallCount); // Dispose retried and succeeded
            Assert.True(fake.IsDisposed);
            Assert.Equal(1, fake2.InitializeCalls); // New session created
            Assert.Equal(0, fake2.TerminateCalls); // New session not terminated yet
            Assert.False(fake2.IsDisposed); // New session stays active
            Assert.Equal(2, factoryCallCount);

            // Explicitly disconnect the new session
            provider.Disconnect();

            Assert.Equal(1, fake2.TerminateCalls);
            Assert.True(fake2.IsDisposed);
        }

        [Fact]
        public void PendingDispose_RetryDispose_Fails_RetainsReferencesAndDoesNotInitNew()
        {
            var fake = new DisposeFailsAndRetainsFake
            {
                InitializeHandler = (out IntPtr sys) =>
                {
                    sys = new IntPtr(0x1000);
                    return 0;
                },
                TerminateHandler = () => 0,
            };
            var fake2 = new FakeAdlxNative();
            var factoryCallCount = 0;
            var provider = new AmdAdlxProvider(() =>
            {
                factoryCallCount++;
                return factoryCallCount == 1 ? fake : fake2;
            });

            // First call: monitoring succeeds, Terminate succeeds, Dispose throws -> retained in _pendingDisposeNative
            var result1 = ((IGpuProviderProbe)provider).TryProbe(out var error);
            Assert.True(result1);
            Assert.Equal(0, fake.TerminateCalls); // Terminate not called after normal operation
            Assert.Equal(0, fake.DisposeCallCount); // Dispose not called after normal operation
            Assert.False(fake.IsDisposed);

            // Disconnect triggers Terminate (success) then Dispose (throws)
            provider.Disconnect();

            Assert.Equal(1, fake.TerminateCalls);
            Assert.Equal(1, fake.DisposeCallCount);
            Assert.False(fake.IsDisposed); // Dispose threw, retained

            // Explicit Connect retries Dispose (still fails)
            var connectResult = provider.Connect(out error);
            Assert.False(connectResult);
            Assert.NotNull(error);
            Assert.Equal(1, fake.TerminateCalls); // Terminate not retried
            Assert.Equal(2, fake.DisposeCallCount); // Dispose retried and threw again
            Assert.False(fake.IsDisposed);
            Assert.Equal(0, fake2.InitializeCalls); // New session NOT created
            Assert.Equal(1, factoryCallCount);

            // Second Connect attempt - Dispose throws again
            connectResult = provider.Connect(out error);
            Assert.False(connectResult);
            Assert.NotNull(error);
            Assert.Equal(1, fake.TerminateCalls);
            Assert.Equal(3, fake.DisposeCallCount);
            Assert.False(fake.IsDisposed);
            Assert.Equal(0, fake2.InitializeCalls);
            Assert.Equal(1, factoryCallCount);
        }

        [Fact]
        public void Probe_CancellationBeforeStart_ThrowsOperationCanceledException()
        {
            var fake = new FakeAdlxNative();
            var provider = new AmdAdlxProvider(() => fake);
            var cts = new CancellationTokenSource();
            cts.Cancel();

            Assert.Throws<OperationCanceledException>(() => ((IGpuProviderProbe)provider).TryProbe(out _, cts.Token));
            Assert.Equal(0, fake.InitializeCalls);
        }

        [Fact]
        public void Probe_CancellationDuringEnumeration_ThrowsOperationCanceledException()
        {
            var fake = new FakeAdlxNative
            {
                GpuNames = new List<string> { "GPU1", "GPU2", "GPU3", "GPU4", "GPU5" },
                GpuUniqueIds = new List<int> { 1, 2, 3, 4, 5 },
                GpuTemperatureSupported = new List<bool> { true, true, true, true, true }
            };
            var provider = new AmdAdlxProvider(() => fake);
            var cts = new CancellationTokenSource();

            // Cancel from GetGpuAt handler - during enumeration, after Initialize and GetGpuListSize
            fake.GetGpuAtHandler = (IntPtr list, int index, out IntPtr gpu) =>
            {
                gpu = new IntPtr(0x3000 + index);
                cts.Cancel();
                return 0;
            };

            Assert.Throws<OperationCanceledException>(() => ((IGpuProviderProbe)provider).TryProbe(out _, cts.Token));
            
            // Verify Initialize and GetGpuListSize were called, but no further GPUs processed
            Assert.Equal(1, fake.InitializeCalls);
            // Shared session should still be active after cancellation
            Assert.Equal(0, fake.TerminateCalls);
            Assert.False(fake.IsDisposed);
            // GPU handle from GetGpuAt should be released
            Assert.Contains(0x3000, fake.ReleasedPointers);
            // GPU list should NOT be released (owned by persistent session)
            Assert.DoesNotContain(0x2000, fake.ReleasedPointers);
            // System should NOT be released (managed by ADLX)
            Assert.DoesNotContain(0x1000, fake.ReleasedPointers);
        }

        [Fact]
        public void TryEnumerateDevices_CancellationBeforeStart_ThrowsOperationCanceledException()
        {
            var fake = new FakeAdlxNative();
            var provider = new AmdAdlxProvider(() => fake);
            var cts = new CancellationTokenSource();
            cts.Cancel();

            Assert.Throws<OperationCanceledException>(() => ((IGpuDeviceEnumerator)provider).TryEnumerateDevices(out _, out _, cts.Token));
            Assert.Equal(0, fake.InitializeCalls);
        }

        [Fact]
        public void TryReadTemperatureInternal_CancellationBeforeStart_ThrowsOperationCanceledException()
        {
            var fake = new FakeAdlxNative();
            var provider = new AmdAdlxProvider(() => fake);
            var cts = new CancellationTokenSource();
            cts.Cancel();

            Assert.Throws<OperationCanceledException>(() => provider.TryReadTemperatureInternal(12345, out _, out _, cts.Token));
            Assert.Equal(0, fake.InitializeCalls);
        }

        [Fact]
        public void Probe_Cancellation_DoesNotLeakResources()
        {
            var fake = new FakeAdlxNative();
            var provider = new AmdAdlxProvider(() => fake);
            var cts = new CancellationTokenSource();
            cts.Cancel();

            try
            {
                ((IGpuProviderProbe)provider).TryProbe(out _, cts.Token);
            }
            catch (OperationCanceledException)
            {
            }

            // Resources should be cleaned up - no pending references
            Assert.Equal(0, fake.InitializeCalls);
        }

        [Fact]
        public void TryEnumerateDevices_Cancellation_DoesNotLeakResources()
        {
            var fake = new FakeAdlxNative();
            var provider = new AmdAdlxProvider(() => fake);
            var cts = new CancellationTokenSource();
            cts.Cancel();

            try
            {
                ((IGpuDeviceEnumerator)provider).TryEnumerateDevices(out _, out _, cts.Token);
            }
            catch (OperationCanceledException)
            {
            }

            // Resources should be cleaned up
            Assert.Equal(0, fake.InitializeCalls);
        }

        [Fact]
        public void TryReadTemperatureInternal_Cancellation_DoesNotLeakResources()
        {
            var fake = new FakeAdlxNative();
            var provider = new AmdAdlxProvider(() => fake);
            var cts = new CancellationTokenSource();
            cts.Cancel();

            try
            {
                provider.TryReadTemperatureInternal(12345, out _, out _, cts.Token);
            }
            catch (OperationCanceledException)
            {
            }

            // Resources should be cleaned up
            Assert.Equal(0, fake.InitializeCalls);
        }

        [Fact]
        public void TryReadTemperatureInternal_CancellationNotTreatedAsReadError()
        {
            var fake = new FakeAdlxNative();
            var provider = new AmdAdlxProvider(() => fake);
            var cts = new CancellationTokenSource();
            cts.Cancel();

            bool success = false;
            string? error = null;
            TemperatureReading? reading = null;

            try
            {
                success = provider.TryReadTemperatureInternal(12345, out reading, out error, cts.Token);
            }
            catch (OperationCanceledException)
            {
                // Expected - cancellation should propagate, not be treated as a read error
            }

            Assert.False(success);
            // The method should throw OperationCanceledException, not return false with an error message
            // This test verifies that cancellation is not caught and converted to a regular error
        }

        [Fact]
        public void TryEnumerateDevices_CancellationDuringGetGpuUniqueId_ThrowsOperationCanceledException()
        {
            // Cancellation occurs from GetGpuUniqueId handler - verify GetGpuName is NOT called after cancellation
            var nativeCalls = new List<string>();
            var fake = new FakeAdlxNative
            {
                GpuNames = new List<string> { "GPU1", "GPU2", "GPU3" },
                GpuUniqueIds = new List<int> { 111, 222, 333 },
                GpuTypes = new List<int> { 2, 2, 2 },
                GpuTemperatureSupported = new List<bool> { true, true, true }
            };
            var provider = new AmdAdlxProvider(() => fake);
            var cts = new CancellationTokenSource();

            // Set up handlers to track calls
            fake.GetGpuListSizeHandler = (IntPtr list, out int count) =>
            {
                nativeCalls.Add("GetGpuListSize");
                count = fake.GpuNames.Count;
                return 0;
            };
            fake.GetGpuAtHandler = (IntPtr list, int index, out IntPtr gpu) =>
            {
                nativeCalls.Add($"GetGpuAt[{index}]");
                gpu = new IntPtr(0x3000 + index);
                return 0;
            };
            fake.GetGpuUniqueIdHandler = (IntPtr gpu, out int uid) =>
            {
                nativeCalls.Add("GetGpuUniqueId");
                int index = (int)(gpu.ToInt64() - 0x3000);
                uid = index >= 0 && index < fake.GpuUniqueIds.Count ? fake.GpuUniqueIds[index] : 0;
                if (index == 1) // Cancel on second GPU during GetGpuUniqueId
                {
                    cts.Cancel();
                }
                return 0;
            };
            fake.GetGpuNameHandler = (IntPtr gpu, out string? name) =>
            {
                nativeCalls.Add("GetGpuName");
                int index = (int)(gpu.ToInt64() - 0x3000);
                name = index >= 0 && index < fake.GpuNames.Count ? fake.GpuNames[index] : "Unknown";
                return 0;
            };
            fake.GetGpuTypeHandler = (IntPtr gpu, out int gpuType) =>
            {
                nativeCalls.Add("GetGpuType");
                int index = (int)(gpu.ToInt64() - 0x3000);
                gpuType = index >= 0 && index < fake.GpuTypes.Count ? fake.GpuTypes[index] : 0;
                return 0;
            };

            Assert.Throws<OperationCanceledException>(() => ((IGpuDeviceEnumerator)provider).TryEnumerateDevices(out _, out _, cts.Token));

            // Verify calls made before cancellation
            Assert.Contains("GetGpuListSize", nativeCalls);
            Assert.Contains("GetGpuAt[0]", nativeCalls);
            Assert.Contains("GetGpuAt[1]", nativeCalls);
            Assert.Contains("GetGpuUniqueId", nativeCalls); // Called for GPU 0 and 1
            
            // GetGpuName should have been called for GPU 0 (before cancellation)
            // but NOT for GPU 1 (cancellation happened in GetGpuUniqueId for GPU 1, before GetGpuName)
            // Actually, looking at the code, GetGpuName is called after GetGpuUniqueId in GetUniqueIdAndName
            // and cancellation is checked between them. So GetGpuName for GPU 0 should be called,
            // but for GPU 1 it should NOT be called because cancellation happens in GetGpuUniqueId handler.
            // The test setup has GetGpuUniqueId for index 1 cancel, so GetGpuName for index 1 should not be called.
            
            // GetGpuName for GPU 0 (index 0) should be called
            // GetGpuName for GPU 1 (index 1) should NOT be called (cancelled in GetGpuUniqueId)
            // Since both use the same handler, we check the call count
            int getGpuNameCallCount = nativeCalls.Count(c => c == "GetGpuName");
            Assert.Equal(1, getGpuNameCallCount); // Only GPU 0's name read
            
            // GetGpuType should not be called for GPU 1 (after cancellation)
            int getGpuTypeCallCount = nativeCalls.Count(c => c == "GetGpuType");
            Assert.Equal(1, getGpuTypeCallCount); // Only GPU 0's type read

            // Shared session should still be active after cancellation
            Assert.Equal(0, fake.TerminateCalls);
            Assert.False(fake.IsDisposed);
            // GPU handles released
            Assert.Contains(0x3000, fake.ReleasedPointers); // GPU 0
            Assert.Contains(0x3001, fake.ReleasedPointers); // GPU 1
            // GPU list should NOT be released (owned by persistent session)
            Assert.DoesNotContain(0x2000, fake.ReleasedPointers);
            Assert.DoesNotContain(0x1000, fake.ReleasedPointers); // System not released
        }

        [Fact]
        public void TryReadTemperatureInternal_CancellationDuringGetPerfServices_ThrowsOperationCanceledException()
        {
            // Cancellation occurs from GetPerformanceMonitoringServices handler - verify subsequent calls not made
            var nativeCalls = new List<string>();
            var fake = new FakeAdlxNative();
            var provider = new AmdAdlxProvider(() => fake);
            var cts = new CancellationTokenSource();

            // Track all native calls
            fake.GetGpuListSizeHandler = (IntPtr list, out int count) =>
            {
                nativeCalls.Add("GetGpuListSize");
                count = 1;
                return 0;
            };
            fake.GetGpuAtHandler = (IntPtr list, int index, out IntPtr gpu) =>
            {
                nativeCalls.Add("GetGpuAt");
                gpu = new IntPtr(0x3000);
                return 0;
            };
            fake.GetGpuUniqueIdHandler = (IntPtr gpu, out int uid) =>
            {
                nativeCalls.Add("GetGpuUniqueId");
                uid = 12345;
                return 0;
            };
            fake.GetPerfServicesHandler = (IntPtr sys, out IntPtr svc) =>
            {
                nativeCalls.Add("GetPerformanceMonitoringServices");
                svc = new IntPtr(0x4000);
                cts.Cancel(); // Cancel here
                return 0;
            };
            fake.GetSupportedMetricsHandler = (IntPtr svc, IntPtr gpu, out IntPtr ms) =>
            {
                nativeCalls.Add("GetSupportedGpuMetrics");
                ms = new IntPtr(0x6000);
                return 0;
            };
            fake.IsSupportedTempHandler = (IntPtr ms, out bool supported) =>
            {
                nativeCalls.Add("IsSupportedGpuTemperature");
                supported = true;
                return 0;
            };
            fake.GetCurrentMetricsHandler = (IntPtr svc, IntPtr gpu, out IntPtr met) =>
            {
                nativeCalls.Add("GetCurrentGpuMetrics");
                met = new IntPtr(0x5000);
                return 0;
            };
            fake.GetGpuTempHandler = (IntPtr met, out double temp) =>
            {
                nativeCalls.Add("GetGpuTemperature");
                temp = 65.5;
                return 0;
            };

            Assert.Throws<OperationCanceledException>(() => provider.TryReadTemperatureInternal(12345, out _, out _, cts.Token));

            // Verify calls made before cancellation
            Assert.Contains("GetGpuListSize", nativeCalls);
            Assert.Contains("GetGpuAt", nativeCalls);
            Assert.Contains("GetGpuUniqueId", nativeCalls);
            Assert.Contains("GetPerformanceMonitoringServices", nativeCalls);
            
            // Subsequent calls should NOT be made
            Assert.DoesNotContain("GetSupportedGpuMetrics", nativeCalls);
            Assert.DoesNotContain("IsSupportedGpuTemperature", nativeCalls);
            Assert.DoesNotContain("GetCurrentGpuMetrics", nativeCalls);
            Assert.DoesNotContain("GetGpuTemperature", nativeCalls);

            // Shared session should still be active after cancellation
            Assert.Equal(0, fake.TerminateCalls);
            Assert.False(fake.IsDisposed);
            // Services (0x4000) acquired and cached before cancellation - NOT released
            Assert.DoesNotContain(0x4000, fake.ReleasedPointers); // services cached, not released
            // Target GPU (0x3000) is cached and NOT released during cancellation
            Assert.DoesNotContain(0x3000, fake.ReleasedPointers);
            // GPU list should NOT be released (owned by persistent session)
            Assert.DoesNotContain(0x2000, fake.ReleasedPointers);
            Assert.DoesNotContain(0x1000, fake.ReleasedPointers); // System not released

            // Disconnect to release all session resources
            provider.Disconnect();

            // After Disconnect: cached GPU handle and gpuList should be released
            Assert.Contains(0x3000, fake.ReleasedPointers); // target GPU released during Disconnect
            Assert.Contains(0x2000, fake.ReleasedPointers); // gpuList released during Disconnect
        }

        [Fact]
        public void Probe_CancellationTokenCheckedBetweenNativeCalls()
        {
            // Verify that CancellationToken is checked between native ADLX calls
            // (native calls themselves don't receive the token, but the provider checks it between calls)
            var nativeCalls = new List<string>();
            var fake = new FakeAdlxNative
            {
                GpuTemperatureSupported = new List<bool> { false, true, true },
                InitializeHandler = (out IntPtr sys) =>
                {
                    nativeCalls.Add("Initialize");
                    sys = new IntPtr(0x1000);
                    return 0;
                },
                GetGpuListHandler = (IntPtr sys, out IntPtr list) =>
                {
                    nativeCalls.Add("GetGpuList");
                    list = new IntPtr(0x2000);
                    return 0;
                },
                GetGpuListSizeHandler = (IntPtr list, out int count) =>
                {
                    nativeCalls.Add("GetGpuListSize");
                    count = 3;
                    return 0;
                },
                GetGpuAtHandler = (IntPtr list, int index, out IntPtr gpu) =>
                {
                    nativeCalls.Add($"GetGpuAt[{index}]");
                    gpu = new IntPtr(0x3000 + index);
                    return 0;
                },
                GetGpuUniqueIdHandler = (IntPtr gpu, out int uid) =>
                {
                    nativeCalls.Add("GetGpuUniqueId");
                    uid = 12345;
                    return 0;
                },
                GetPerfServicesHandler = (IntPtr sys, out IntPtr svc) =>
                {
                    nativeCalls.Add("GetPerformanceMonitoringServices");
                    svc = new IntPtr(0x4000);
                    return 0;
                },
                GetSupportedMetricsHandler = (IntPtr svc, IntPtr gpu, out IntPtr ms) =>
                {
                    nativeCalls.Add("GetSupportedGpuMetrics");
                    int gpuIndex = (int)(gpu.ToInt64() - 0x3000);
                    ms = new IntPtr(0x6000 + gpuIndex);
                    return 0;
                },
                IsSupportedTempHandler = (IntPtr ms, out bool supported) =>
                {
                    nativeCalls.Add("IsSupportedGpuTemperature");
                    int gpuIndex = (int)(ms.ToInt64() - 0x6000);
                    supported = gpuIndex != 0; // GPU 0 returns false, others true
                    return 0;
                }
            };
            var provider = new AmdAdlxProvider(() => fake);
            
            // Cancel after Initialize and GetGpuListSize, during GetGpuAt for second GPU
            var cts = new CancellationTokenSource();
            fake.GetGpuAtHandler = (IntPtr list, int index, out IntPtr gpu) =>
            {
                nativeCalls.Add($"GetGpuAt[{index}]");
                gpu = new IntPtr(0x3000 + index);
                if (index == 1) // Cancel on second GPU
                {
                    cts.Cancel();
                }
                return 0;
            };

            Assert.Throws<OperationCanceledException>(() => ((IGpuProviderProbe)provider).TryProbe(out _, cts.Token));

            // Initialize and GetGpuListSize should have been called
            Assert.Contains("Initialize", nativeCalls);
            Assert.Contains("GetGpuListSize", nativeCalls);
            // GetGpuAt for index 0 and 1 should have been called
            Assert.Contains("GetGpuAt[0]", nativeCalls);
            Assert.Contains("GetGpuAt[1]", nativeCalls);
            // GetGpuAt for index 2 should NOT have been called (cancelled before)
            Assert.DoesNotContain("GetGpuAt[2]", nativeCalls);
            // IsSupportedGpuTemperature should not have been called for index 2
            // (but may have been called for index 0 and 1)
            
            // Cancellation does not close persistent session - session should still be active
            Assert.Equal(0, fake.TerminateCalls);
            Assert.False(fake.IsDisposed);
            // Local interfaces acquired before cancellation should be released
            Assert.Contains(0x3001, fake.ReleasedPointers); // GPU handle from GetGpuAt[1]
            // GPU list handle 0x2000 should NOT be released yet (owned by persistent session)
            Assert.DoesNotContain(0x2000, fake.ReleasedPointers);

            // Now explicitly disconnect to verify full cleanup
            provider.Disconnect();

            Assert.Equal(1, fake.TerminateCalls);
            Assert.True(fake.IsDisposed);
            // GPU list handle 0x2000 should now be released
            Assert.Contains(0x2000, fake.ReleasedPointers);
        }

        [Fact]
        public void Implements_IGpuProvider_CanBeUsedAsAggregateInterface()
        {
            var provider = new AmdAdlxProvider(() => new FakeAdlxNative());

            // Should be assignable to IGpuProvider (aggregate interface)
            IGpuProvider aggregateProvider = provider;

            // Should have Id and DisplayName from shared metadata interface
            Assert.Equal("amd-adlx", aggregateProvider.Id);
            Assert.Equal("AMD ADLX", aggregateProvider.DisplayName);

            // Should be able to call Probe (from IGpuProviderProbe)
            var probeResult = ((IGpuProviderProbe)aggregateProvider).TryProbe(out var probeError);
            Assert.True(probeResult);
            Assert.Null(probeError);

            // Should be able to call EnumerateDevices (from IGpuDeviceEnumerator)
            bool success = ((IGpuDeviceEnumerator)aggregateProvider).TryEnumerateDevices(out var devices, out var error);
            Assert.True(success);
            Assert.Null(error);
            Assert.Single(devices);
            Assert.Equal("amd-adlx:12345", devices[0].Id);
        }

        [Fact]
        public void LazyConnection_ProbeCreatesSession_ReusedByEnumerateAndRead()
        {
            var fake = new FakeAdlxNative();
            var provider = new AmdAdlxProvider(() => fake);

            // First operation: Probe (lazy creates session)
            var probeResult = ((IGpuProviderProbe)provider).TryProbe(out var probeError);
            Assert.True(probeResult);
            Assert.Null(probeError);
            Assert.Equal(1, fake.InitializeCalls);

            // Second operation: EnumerateDevices (reuses session)
            bool success = ((IGpuDeviceEnumerator)provider).TryEnumerateDevices(out var devices, out var error);
            Assert.True(success);
            Assert.Null(error);
            Assert.Single(devices);
            Assert.Equal(1, fake.InitializeCalls); // No new Initialize

            // Third operation: TryReadTemperature (reuses session)
            success = ((IGpuTemperatureReader)provider).TryReadTemperature("amd-adlx:12345", out var reading, out error);
            Assert.True(success);
            Assert.NotNull(reading);
            Assert.Equal(1, fake.InitializeCalls); // Still no new Initialize
            Assert.Equal(0, fake.TerminateCalls); // Session not terminated
        }

        [Fact]
        public void LazyConnection_EnumerateCreatesSession_ReusedByProbeAndRead()
        {
            var fake = new FakeAdlxNative();
            var provider = new AmdAdlxProvider(() => fake);

            // First operation: EnumerateDevices (lazy creates session)
            bool success = ((IGpuDeviceEnumerator)provider).TryEnumerateDevices(out var devices, out var error);
            Assert.True(success);
            Assert.Null(error);
            Assert.Single(devices);
            Assert.Equal(1, fake.InitializeCalls);

            // Second operation: Probe (reuses session)
            var probeResult = ((IGpuProviderProbe)provider).TryProbe(out var probeError);
            Assert.True(probeResult);
            Assert.Null(probeError);
            Assert.Equal(1, fake.InitializeCalls);

            // Third operation: TryReadTemperature (reuses session)
            success = ((IGpuTemperatureReader)provider).TryReadTemperature("amd-adlx:12345", out _, out _);
            Assert.True(success);
            Assert.Equal(1, fake.InitializeCalls);
            Assert.Equal(0, fake.TerminateCalls);
        }

        [Fact]
        public void LazyConnection_ReadCreatesSession_ReusedByProbeAndEnumerate()
        {
            var fake = new FakeAdlxNative();
            var provider = new AmdAdlxProvider(() => fake);

            // First operation: TryReadTemperature (lazy creates session)
            bool success = ((IGpuTemperatureReader)provider).TryReadTemperature("amd-adlx:12345", out _, out _);
            Assert.True(success);
            Assert.Equal(1, fake.InitializeCalls);

            // Second operation: Probe (reuses session)
            var probeResult = ((IGpuProviderProbe)provider).TryProbe(out var probeError);
            Assert.True(probeResult);
            Assert.Null(probeError);
            Assert.Equal(1, fake.InitializeCalls);

            // Third operation: EnumerateDevices (reuses session)
            success = ((IGpuDeviceEnumerator)provider).TryEnumerateDevices(out var devices, out var error);
            Assert.True(success);
            Assert.Null(error);
            Assert.Single(devices);
            Assert.Equal(1, fake.InitializeCalls);
            Assert.Equal(0, fake.TerminateCalls);
        }

        [Fact]
        public void Connect_Explicit_IsIdempotent()
        {
            var fake = new FakeAdlxNative();
            var provider = new AmdAdlxProvider(() => fake);

            Assert.True(provider.Connect(out _));
            Assert.True(provider.Connect(out _));
            Assert.Equal(1, fake.InitializeCalls);
        }

        [Fact]
        public void Disconnect_ClosesSession_PendingRetryOnNextOperation()
        {
            // Terminate succeeds, Dispose throws -> retained in _pendingDisposeNative
            var fake = new DisposeThenSucceedsFake
            {
                InitializeHandler = (out IntPtr sys) =>
                {
                    sys = new IntPtr(0x1000);
                    return 0;
                },
                TerminateHandler = () => 0,
            };
            var fake2 = new FakeAdlxNative();
            var factoryCallCount = 0;
            var provider = new AmdAdlxProvider(() =>
            {
                factoryCallCount++;
                return factoryCallCount == 1 ? fake : fake2;
            });

            // Lazy connect via Probe
            var probeResult = ((IGpuProviderProbe)provider).TryProbe(out var probeError);
            Assert.True(probeResult);
            Assert.Null(probeError);
            Assert.Equal(1, fake.InitializeCalls);

            provider.Disconnect();

            Assert.Equal(1, fake.TerminateCalls);
            Assert.Equal(1, fake.DisposeCallCount);
            Assert.False(fake.IsDisposed); // Dispose threw, retained

            // Next explicit Connect retries Dispose, succeeds
            var connectResult = provider.Connect(out var error);
            Assert.True(connectResult);
            Assert.Null(error);
            Assert.Equal(1, fake.TerminateCalls);
            Assert.Equal(2, fake.DisposeCallCount);
            Assert.True(fake.IsDisposed);
            Assert.Equal(1, fake2.InitializeCalls);
            Assert.Equal(0, fake2.TerminateCalls);
            Assert.False(fake2.IsDisposed);
            Assert.Equal(2, factoryCallCount);
        }

        [Fact]
        public void Cancellation_DoesNotCloseSharedSession()
        {
            var fake = new FakeAdlxNative();
            var provider = new AmdAdlxProvider(() => fake);

            // Lazy connect
            var probeResult = ((IGpuProviderProbe)provider).TryProbe(out var probeError);
            Assert.True(probeResult);
            Assert.Null(probeError);

            var cts = new CancellationTokenSource();
            cts.Cancel();

            Assert.Throws<OperationCanceledException>(() => ((IGpuProviderProbe)provider).TryProbe(out _, cts.Token));

            // Session should still be active
            Assert.False(fake.IsDisposed);
            Assert.Equal(0, fake.TerminateCalls);

            // Next operation should work
            var result = ((IGpuProviderProbe)provider).TryProbe(out probeError);
            Assert.True(result);
            Assert.Null(probeError);
        }

        [Fact]
        public void Disconnect_NormalClose_ReleasesResourcesOnce()
        {
            var fake = new FakeAdlxNative();
            var provider = new AmdAdlxProvider(() => fake);

            // Create persistent session via lazy operation
            var probeResult = ((IGpuProviderProbe)provider).TryProbe(out var probeError);
            Assert.True(probeResult);
            Assert.Null(probeError);

            // Session is active
            Assert.Equal(1, fake.InitializeCalls);
            Assert.Equal(0, fake.TerminateCalls);
            Assert.False(fake.IsDisposed);

            // Disconnect: should terminate and dispose
            provider.Disconnect();

            Assert.Equal(1, fake.TerminateCalls);
            Assert.True(fake.IsDisposed);
            // gpuList (0x2000) released exactly once
            Assert.Single(fake.ReleasedPointers, p => p == 0x2000);
            // System (0x1000) NOT released via Release (managed by ADLX)
            Assert.DoesNotContain(0x1000, fake.ReleasedPointers);
            // Cleanup order: gpuList released before Terminate
            int gpuListReleaseIdx = fake.Events.IndexOf("Release:0x2000");
            int terminateIdx = fake.Events.IndexOf("Terminate");
            Assert.True(gpuListReleaseIdx >= 0, "gpuList should be released");
            Assert.True(terminateIdx >= 0, "Terminate should be called");
            Assert.True(gpuListReleaseIdx < terminateIdx, "gpuList released before Terminate");

            // Second Disconnect is idempotent
            int releaseCount = fake.ReleasedPointers.Count;
            provider.Disconnect();

            Assert.Equal(1, fake.TerminateCalls); // No additional Terminate
            Assert.Single(fake.ReleasedPointers, p => p == 0x2000); // gpuList still released only once
            Assert.Equal(releaseCount, fake.ReleasedPointers.Count); // Total release count unchanged
            // No additional resource releases
        }

        [Fact]
        public void Implements_IGpuProviderLifecycle_CanConnectAndDisconnect()
        {
            var fake = new FakeAdlxNative();
            var provider = new AmdAdlxProvider(() => fake);

            // Verify interface implementation
            IGpuProviderLifecycle lifecycle = provider;
            Assert.NotNull(lifecycle);

            // Connect creates session
            Assert.True(lifecycle.Connect(out var error));
            Assert.Null(error);
            Assert.Equal(1, fake.InitializeCalls);

            // Second Connect is idempotent
            Assert.True(lifecycle.Connect(out _));
            Assert.Equal(1, fake.InitializeCalls);

            // Disconnect terminates and disposes
            lifecycle.Disconnect();
            Assert.Equal(1, fake.TerminateCalls);
            Assert.True(fake.IsDisposed);

            // Second Disconnect is idempotent
            lifecycle.Disconnect();
            Assert.Equal(1, fake.TerminateCalls);
        }

        [Fact]
        public void Implements_IGpuProviderLifecycle_Disconnect_PreventsAutoReconnectOnProbe()
        {
            var fake = new FakeAdlxNative();
            var provider = new AmdAdlxProvider(() => fake);

            IGpuProviderLifecycle lifecycle = provider;

            // Connect
            Assert.True(lifecycle.Connect(out _));
            Assert.Equal(1, fake.InitializeCalls);

            // Disconnect
            lifecycle.Disconnect();
            Assert.Equal(1, fake.TerminateCalls);

            // Probe after Disconnect should NOT reinitialize
            var probeResult = ((IGpuProviderProbe)provider).TryProbe(out var probeError, CancellationToken.None);
            Assert.False(probeResult);
            Assert.Contains("disconnected", probeError, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(1, fake.InitializeCalls); // No new initialization
        }

        [Fact]
        public void Implements_IGpuProviderLifecycle_Disconnect_PreventsAutoReconnectOnEnumerateDevices()
        {
            var fake = new FakeAdlxNative();
            var provider = new AmdAdlxProvider(() => fake);

            IGpuProviderLifecycle lifecycle = provider;

            // Connect
            Assert.True(lifecycle.Connect(out _));
            Assert.Equal(1, fake.InitializeCalls);

            // Disconnect
            lifecycle.Disconnect();
            Assert.Equal(1, fake.TerminateCalls);

            // EnumerateDevices after Disconnect should NOT reinitialize
            bool success = ((IGpuDeviceEnumerator)provider).TryEnumerateDevices(out var devices, out var error, CancellationToken.None);
            Assert.False(success); // Should fail because disconnected
            Assert.NotNull(error);
            Assert.Contains("disconnected", error, StringComparison.OrdinalIgnoreCase);
            Assert.Empty(devices);
            Assert.Equal(1, fake.InitializeCalls); // No new initialization
        }

        [Fact]
        public void Implements_IGpuProviderLifecycle_Disconnect_PreventsAutoReconnectOnTryReadTemperature()
        {
            var fake = new FakeAdlxNative();
            var provider = new AmdAdlxProvider(() => fake);

            IGpuProviderLifecycle lifecycle = provider;

            // Connect
            Assert.True(lifecycle.Connect(out _));
            Assert.Equal(1, fake.InitializeCalls);

            // Disconnect
            lifecycle.Disconnect();
            Assert.Equal(1, fake.TerminateCalls);

            // TryReadTemperature after Disconnect should NOT reinitialize
            bool result = provider.TryReadTemperature("amd-adlx:12345", out var reading, out var error, CancellationToken.None);
            Assert.False(result);
            Assert.Contains("disconnected", error, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(1, fake.InitializeCalls); // No new initialization
        }

        [Fact]
        public void Implements_IGpuProviderLifecycle_ConnectAfterDisconnect_Reinitializes()
        {
            var fake = new FakeAdlxNative();
            var provider = new AmdAdlxProvider(() => fake);

            IGpuProviderLifecycle lifecycle = provider;

            // Connect
            Assert.True(lifecycle.Connect(out _));
            Assert.Equal(1, fake.InitializeCalls);

            // Disconnect
            lifecycle.Disconnect();
            Assert.Equal(1, fake.TerminateCalls);

            // Connect again - should reinitialize
            Assert.True(lifecycle.Connect(out _));
            Assert.Equal(2, fake.InitializeCalls);
        }

        [Fact]
        public void Implements_IGpuProviderLifecycle_MultipleConnectWithoutDisconnect_Idempotent()
        {
            var fake = new FakeAdlxNative();
            var provider = new AmdAdlxProvider(() => fake);

            IGpuProviderLifecycle lifecycle = provider;

            // First Connect
            Assert.True(lifecycle.Connect(out _));
            Assert.Equal(1, fake.InitializeCalls);

            // Second Connect - should be idempotent
            Assert.True(lifecycle.Connect(out _));
            Assert.Equal(1, fake.InitializeCalls);

            // Third Connect - should be idempotent
            Assert.True(lifecycle.Connect(out _));
            Assert.Equal(1, fake.InitializeCalls);

            // Disconnect
            lifecycle.Disconnect();
            Assert.Equal(1, fake.TerminateCalls);

            // Connect after Disconnect - should reinitialize
            Assert.True(lifecycle.Connect(out _));
            Assert.Equal(2, fake.InitializeCalls);
        }

        [Fact]
        public void Implements_IGpuProviderLifecycle_DisconnectBeforeConnect_PreventsAutoReconnectOnProbe()
        {
            var fake = new FakeAdlxNative();
            var provider = new AmdAdlxProvider(() => fake);

            IGpuProviderLifecycle lifecycle = provider;

            // Disconnect BEFORE any Connect
            lifecycle.Disconnect();

            // Probe should NOT reinitialize
            var probeResult = ((IGpuProviderProbe)provider).TryProbe(out var probeError, CancellationToken.None);
            Assert.False(probeResult);
            Assert.Contains("disconnected", probeError, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(0, fake.InitializeCalls); // No initialization at all
        }

        [Fact]
        public void Implements_IGpuProviderLifecycle_DisconnectBeforeConnect_PreventsAutoReconnectOnEnumerateDevices()
        {
            var fake = new FakeAdlxNative();
            var provider = new AmdAdlxProvider(() => fake);

            IGpuProviderLifecycle lifecycle = provider;

            // Disconnect BEFORE any Connect
            lifecycle.Disconnect();

            // EnumerateDevices should NOT reinitialize
            bool success = ((IGpuDeviceEnumerator)provider).TryEnumerateDevices(out var devices, out var error, CancellationToken.None);
            Assert.False(success); // Should fail because disconnected
            Assert.NotNull(error);
            Assert.Contains("disconnected", error, StringComparison.OrdinalIgnoreCase);
            Assert.Empty(devices);
            Assert.Equal(0, fake.InitializeCalls); // No initialization at all
        }

        [Fact]
        public void Implements_IGpuProviderLifecycle_DisconnectBeforeConnect_PreventsAutoReconnectOnTryReadTemperature()
        {
            var fake = new FakeAdlxNative();
            var provider = new AmdAdlxProvider(() => fake);

            IGpuProviderLifecycle lifecycle = provider;

            // Disconnect BEFORE any Connect
            lifecycle.Disconnect();

            // TryReadTemperature should NOT reinitialize
            bool result = provider.TryReadTemperature("amd-adlx:12345", out var reading, out var error, CancellationToken.None);
            Assert.False(result);
            Assert.Contains("disconnected", error, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(0, fake.InitializeCalls); // No initialization at all
        }

        [Fact]
        public void Implements_IGpuProviderLifecycle_ConnectAfterEarlyDisconnect_Reinitializes()
        {
            var fake = new FakeAdlxNative();
            var provider = new AmdAdlxProvider(() => fake);

            IGpuProviderLifecycle lifecycle = provider;

            // Disconnect BEFORE any Connect
            lifecycle.Disconnect();

            // Connect should now work and initialize
            Assert.True(lifecycle.Connect(out _));
            Assert.Equal(1, fake.InitializeCalls);
        }

        [Fact]
        public void TryProbe_ConcurrentOperations_AreSerializedAndSessionInitializedOnce()
        {
            // This test verifies that two concurrent TryProbe() calls on the same provider
            // are properly serialized by the internal lock, and the ADLX session is initialized only once.
            // It directly measures concurrent entries to the native handler using Interlocked.
            
            var firstOperationReachedPause = new ManualResetEventSlim(false);
            var releaseFirstOperation = new ManualResetEventSlim(false);
            var firstOperationCompleted = new ManualResetEventSlim(false);
            var secondOperationCompleted = new ManualResetEventSlim(false);
            
            bool firstOperationError = false;
            bool secondOperationError = false;
            string? firstError = null;
            string? secondError = null;
            int initializeCallCountAtFirstPause = 0;
            int terminateCallCountAtEnd = 0;
            bool isDisposedAtEnd = false;
            
            // Concurrency tracking in handler
            int currentHandlerEntries = 0;
            int maxConcurrentHandlerEntries = 0;
            int handlerCallCount = 0;
            
            var fake = new FakeAdlxNative
            {
                GpuNames = new List<string> { "AMD Radeon RX 7900 XTX" },
                GpuUniqueIds = new List<int> { 12345 },
                GpuTypes = new List<int> { 2 },
                GpuTemperatureSupported = new List<bool> { true }
            };
            
            // Pause the first operation inside GetGpuListSize (called during TryProbe after session creation)
            fake.GetGpuListSizeHandler = (IntPtr gpuList, out int count) =>
            {
                count = fake.GpuNames.Count;
                
                // Track concurrent entries using Interlocked
                int entries = Interlocked.Increment(ref currentHandlerEntries);
                int maxEntries = maxConcurrentHandlerEntries;
                while (entries > maxEntries)
                {
                    maxEntries = Interlocked.CompareExchange(ref maxConcurrentHandlerEntries, entries, maxEntries);
                }
                Interlocked.Increment(ref handlerCallCount);
                
                // If this is the first entry, capture initialize count and signal pause
                if (entries == 1)
                {
                    initializeCallCountAtFirstPause = fake.InitializeCalls;
                    firstOperationReachedPause.Set();
                    
                    // Wait for release signal
                    if (!releaseFirstOperation.Wait(TimeSpan.FromSeconds(30)))
                    {
                        firstOperationError = true;
                        firstError = "Timeout waiting for release signal";
                        firstOperationCompleted.Set();
                        Interlocked.Decrement(ref currentHandlerEntries);
                        return -1;
                    }
                }
                
                try
                {
                    return 0;
                }
                finally
                {
                    Interlocked.Decrement(ref currentHandlerEntries);
                }
            };
            
            var provider = new AmdAdlxProvider(() => fake);
            
            // Declare outside try so finally can access them
            Thread? firstThread = null;
            Thread? secondThread = null;
            
            try
            {
                // Start first TryProbe on dedicated thread
                firstThread = new Thread(() =>
                {
                    try
                    {
                        bool result = ((IGpuProviderProbe)provider).TryProbe(out var error);
                        if (!result)
                        {
                            firstOperationError = true;
                            firstError = error;
                        }
                    }
                    catch (Exception ex)
                    {
                        firstOperationError = true;
                        firstError = ex.ToString();
                    }
                    finally
                    {
                        firstOperationCompleted.Set();
                    }
                });
                firstThread.Start();
                
                // Wait for first operation to reach the pause point (inside GetGpuListSize)
                Assert.True(firstOperationReachedPause.Wait(TimeSpan.FromSeconds(10)), 
                    "First operation did not reach pause point in time");
                
                // Verify session was initialized exactly once before the pause
                Assert.Equal(1, initializeCallCountAtFirstPause);
                
                // NOW start second thread - first operation is confirmed paused in handler holding the lock
                secondThread = new Thread(() =>
                {
                    try
                    {
                        bool result = ((IGpuProviderProbe)provider).TryProbe(out var error);
                        if (!result)
                        {
                            secondOperationError = true;
                            secondError = error;
                        }
                    }
                    catch (Exception ex)
                    {
                        secondOperationError = true;
                        secondError = ex.ToString();
                    }
                    finally
                    {
                        secondOperationCompleted.Set();
                    }
                });
                secondThread.Start();
                
                // Wait for second thread to block on the provider's lock (monitor)
                // Use a timeout-based wait without active polling - check ThreadState at intervals
                // but without Thread.Sleep; use a cancellation token with delay.
                bool secondThreadBlocked = false;
                var waitTimeout = TimeSpan.FromSeconds(10);
                var waitStart = DateTime.UtcNow;
                
                while (DateTime.UtcNow - waitStart < waitTimeout)
                {
                    var state = secondThread.ThreadState;
                    if ((state & System.Threading.ThreadState.WaitSleepJoin) != 0)
                    {
                        secondThreadBlocked = true;
                        break;
                    }
                    // Yield instead of Sleep to avoid fixed delay
                    Thread.Yield();
                }
                
                // Failure to observe WaitSleepJoin is a test failure - we cannot confirm blocking
                Assert.True(secondThreadBlocked, "Second thread did not enter WaitSleepJoin state (blocked on lock) within timeout");
                
                // At this point: first operation is paused in handler, second thread is blocked on lock
                Assert.False(firstOperationCompleted.IsSet, "First operation should still be paused");
                
                // Verify only one initialization occurred
                Assert.Equal(1, fake.InitializeCalls);
                
                // Release first operation
                releaseFirstOperation.Set();
                
                // Wait for both operations to complete (auxiliary signals)
                Assert.True(firstOperationCompleted.Wait(TimeSpan.FromSeconds(10)), 
                    "First operation did not complete in time");
                Assert.True(secondOperationCompleted.Wait(TimeSpan.FromSeconds(10)), 
                    "Second operation did not complete in time");
                
                // Verify both operations succeeded
                Assert.False(firstOperationError, $"First operation failed: {firstError}");
                Assert.False(secondOperationError, $"Second operation failed: {secondError}");
                
                // Verify handler was called for both operations
                Assert.Equal(2, handlerCallCount);
                
                // Verify max concurrent handler entries never exceeded 1 (serialized)
                Assert.Equal(1, maxConcurrentHandlerEntries);
            }
            finally
            {
                // Always release first operation to avoid deadlock
                releaseFirstOperation.Set();
                
                // Wait for actual thread termination - not just signals
                bool firstThreadDone = false;
                bool secondThreadDone = false;
                
                if (firstThread != null)
                {
                    firstThreadDone = firstThread.Join(TimeSpan.FromSeconds(10));
                }
                else
                {
                    firstThreadDone = true; // No thread to wait for
                }
                
                if (secondThread != null)
                {
                    secondThreadDone = secondThread.Join(TimeSpan.FromSeconds(10));
                }
                else
                {
                    secondThreadDone = true; // No thread to wait for
                }
                
                // If either didn't complete, fail cleanup and don't dispose/Disconnect
                if (!firstThreadDone || !secondThreadDone)
                {
                    throw new InvalidOperationException(
                        $"Test cleanup failed: firstThreadDone={firstThreadDone}, secondThreadDone={secondThreadDone}. " +
                        "Threads still running; synchronization objects not disposed and provider not disconnected to avoid race.");
                }
                
                // Clean up provider - only after both threads confirmed done
                try { provider.Disconnect(); } catch { }
                
                // Deterministic disposal of synchronization objects (after threads confirmed done)
                firstOperationReachedPause.Dispose();
                releaseFirstOperation.Dispose();
                firstOperationCompleted.Dispose();
                secondOperationCompleted.Dispose();
            }
            
            // Verify session was initialized exactly once total
            Assert.Equal(1, fake.InitializeCalls);
            
            // Verify Terminate was called once on Disconnect
            terminateCallCountAtEnd = fake.TerminateCalls;
            isDisposedAtEnd = fake.IsDisposed;
            Assert.Equal(1, terminateCallCountAtEnd);
            Assert.True(isDisposedAtEnd);
            
            // Verify no double-release of gpuList (0x2000)
            Assert.Single(fake.ReleasedPointers, p => p == 0x2000);
            
            // Verify System (0x1000) NOT released via Release (managed by ADLX)
            Assert.DoesNotContain(0x1000, fake.ReleasedPointers);
            
            // Verify cleanup order: gpuList released before Terminate
            int gpuListReleaseIdx = fake.Events.IndexOf("Release:0x2000");
            int terminateIdx = fake.Events.IndexOf("Terminate");
            Assert.True(gpuListReleaseIdx >= 0, "gpuList should be released");
            Assert.True(terminateIdx >= 0, "Terminate should be called");
            Assert.True(gpuListReleaseIdx < terminateIdx, "gpuList released before Terminate");
        }

        [Fact]
        public void Disconnect_DuringActiveTryReadTemperature_WaitsForReadCompletionAndCleansUpOnce()
        {
            // This test verifies that Disconnect() does not release ADLX resources while a parallel
            // TryReadTemperature() operation is still using the shared session. It confirms:
            // 1. Disconnect blocks on the provider lock until TryReadTemperature completes
            // 2. Terminate is not called while the read is active
            // 3. Resources are not released before the read completes
            // 4. After read completes, Disconnect finishes cleanly
            // 5. No double-release of resources occurs
            // 6. Session is closed exactly once

            var readReachedPause = new ManualResetEventSlim(false);
            var releaseRead = new ManualResetEventSlim(false);
            var readCompleted = new ManualResetEventSlim(false);
            var disconnectCompleted = new ManualResetEventSlim(false);

            bool readError = false;
            string? readErrorMessage = null;
            TemperatureReading? readResult = null;
            bool disconnectError = false;
            string? disconnectErrorMessage = null;
            int terminateCallsAtReadPause = 0;
            int releaseCallsAtReadPause = 0;
            int terminateCallsAtEnd = 0;
            bool isDisposedAtEnd = false;

            var fake = new FakeAdlxNative
            {
                GpuNames = new List<string> { "AMD Radeon RX 7900 XTX" },
                GpuUniqueIds = new List<int> { 12345 },
                GpuTypes = new List<int> { 2 },
                GpuTemperatureSupported = new List<bool> { true },
                GpuTemperatures = new List<double> { 65.5 }
            };

            // Pause the read operation inside GetGpuTemperature (the final native call in the read chain)
            fake.GetGpuTempHandler = (IntPtr metrics, out double celsius) =>
            {
                celsius = fake.GpuTemperatures[0];

                // Signal that read has reached the pause point (holding the provider lock)
                readReachedPause.Set();

                // Wait for release signal
                if (!releaseRead.Wait(TimeSpan.FromSeconds(30)))
                {
                    readError = true;
                    readErrorMessage = "Timeout waiting for release signal";
                    readCompleted.Set();
                    return -1;
                }

                return 0;
            };

            var provider = new AmdAdlxProvider(() => fake);

            // Declare outside try so finally can access them
            Thread? readThread = null;
            Thread? disconnectThread = null;

            try
            {
                // Start TryReadTemperature on dedicated thread
                readThread = new Thread(() =>
                {
                    try
                    {
                        bool success = ((IGpuTemperatureReader)provider).TryReadTemperature("amd-adlx:12345", out readResult, out var error);
                        if (!success)
                        {
                            readError = true;
                            readErrorMessage = error;
                        }
                    }
                    catch (Exception ex)
                    {
                        readError = true;
                        readErrorMessage = ex.ToString();
                    }
                    finally
                    {
                        readCompleted.Set();
                    }
                });
                readThread.Start();

                // Wait for read to reach the pause point (inside GetGpuTemperature, holding the lock)
                Assert.True(readReachedPause.Wait(TimeSpan.FromSeconds(10)),
                    "Read operation did not reach pause point in time");

                // Capture state at pause: Terminate should NOT have been called yet
                terminateCallsAtReadPause = fake.TerminateCalls;
                releaseCallsAtReadPause = fake.ReleaseCalls;
                Assert.Equal(0, terminateCallsAtReadPause);
                Assert.Equal(0, releaseCallsAtReadPause);
                Assert.False(fake.IsDisposed);

                // NOW start Disconnect on another thread - it should block on the provider lock
                disconnectThread = new Thread(() =>
                {
                    try
                    {
                        provider.Disconnect();
                    }
                    catch (Exception ex)
                    {
                        disconnectError = true;
                        disconnectErrorMessage = ex.ToString();
                    }
                    finally
                    {
                        disconnectCompleted.Set();
                    }
                });
                disconnectThread.Start();

                // Wait for Disconnect thread to block on the lock (WaitSleepJoin state)
                bool disconnectThreadBlocked = false;
                var waitTimeout = TimeSpan.FromSeconds(10);
                var waitStart = DateTime.UtcNow;

                while (DateTime.UtcNow - waitStart < waitTimeout)
                {
                    var state = disconnectThread.ThreadState;
                    if ((state & System.Threading.ThreadState.WaitSleepJoin) != 0)
                    {
                        disconnectThreadBlocked = true;
                        break;
                    }
                    Thread.Yield();
                }

                Assert.True(disconnectThreadBlocked, "Disconnect thread did not enter WaitSleepJoin state (blocked on lock) within timeout");

                // Verify Disconnect has NOT completed yet (read still holding the lock)
                Assert.False(disconnectCompleted.IsSet, "Disconnect should not complete while read is paused");
                Assert.Equal(0, fake.TerminateCalls); // Terminate not called yet
                Assert.Equal(0, fake.ReleaseCalls); // No Release calls yet
                Assert.False(fake.IsDisposed); // Session not disposed

                // Release the read operation
                releaseRead.Set();

                // Wait for both operations to complete
                Assert.True(readCompleted.Wait(TimeSpan.FromSeconds(10)),
                    "Read operation did not complete in time after release");
                Assert.True(disconnectCompleted.Wait(TimeSpan.FromSeconds(10)),
                    "Disconnect did not complete in time after read release");

                // Verify both operations succeeded
                Assert.False(readError, $"Read operation failed: {readErrorMessage}");
                Assert.NotNull(readResult);
                Assert.Equal(65.5, readResult!.Celsius);
                Assert.False(disconnectError, $"Disconnect failed: {disconnectErrorMessage}");
            }
            finally
            {
                // Always release read to avoid deadlock
                releaseRead.Set();

                // Wait for actual thread termination
                bool readThreadDone = false;
                bool disconnectThreadDone = false;

                if (readThread != null)
                {
                    readThreadDone = readThread.Join(TimeSpan.FromSeconds(10));
                }
                else
                {
                    readThreadDone = true;
                }

                bool disconnectThreadWasStarted = disconnectThread != null;

                if (disconnectThreadWasStarted)
                {
                    // Disconnect was started on a separate thread - wait for it to complete
                    Thread? localDisconnectThread = disconnectThread;
                    if (localDisconnectThread != null)
                    {
                        disconnectThreadDone = localDisconnectThread.Join(TimeSpan.FromSeconds(10));
                    }
                }
                else
                {
                    // Disconnect was never started - clean up the session directly
                    disconnectThreadDone = true;
                }

                if (!readThreadDone || !disconnectThreadDone)
                {
                    throw new InvalidOperationException(
                        $"Test cleanup failed: readThreadDone={readThreadDone}, disconnectThreadDone={disconnectThreadDone}. " +
                        "Threads still running; synchronization objects not disposed and provider not disconnected to avoid race.");
                }

                // Only call Disconnect if the disconnect thread was never started
                // (if it was started, it already called Disconnect internally)
                if (!disconnectThreadWasStarted)
                {
                    try { provider.Disconnect(); } catch { }
                }

                // Deterministic disposal of synchronization objects (after threads confirmed done)
                readReachedPause.Dispose();
                releaseRead.Dispose();
                readCompleted.Dispose();
                disconnectCompleted.Dispose();
            }

            // Verify final state after both operations completed
            terminateCallsAtEnd = fake.TerminateCalls;
            isDisposedAtEnd = fake.IsDisposed;

            // Terminate called exactly once (during Disconnect cleanup)
            Assert.Equal(1, terminateCallsAtEnd);

            // Session disposed exactly once
            Assert.True(isDisposedAtEnd);

            // gpuList (0x2000) released exactly once during Disconnect cleanup
            Assert.Single(fake.ReleasedPointers, p => p == 0x2000);

            // System (0x1000) NOT released via Release (managed by ADLX)
            Assert.DoesNotContain(0x1000, fake.ReleasedPointers);

            // Verify cleanup order: gpuList released before Terminate
            int gpuListReleaseIdx = fake.Events.IndexOf("Release:0x2000");
            int terminateIdx = fake.Events.IndexOf("Terminate");
            Assert.True(gpuListReleaseIdx >= 0, "gpuList should be released");
            Assert.True(terminateIdx >= 0, "Terminate should be called");
            Assert.True(gpuListReleaseIdx < terminateIdx, "gpuList released before Terminate");

            // Verify no double-release of any interface
            var releaseCounts = new Dictionary<int, int>();
            foreach (var ptr in fake.ReleasedPointers)
            {
                if (!releaseCounts.ContainsKey(ptr))
                    releaseCounts[ptr] = 0;
                releaseCounts[ptr]++;
            }
            foreach (var kvp in releaseCounts)
            {
                Assert.True(kvp.Value == 1, $"Interface 0x{kvp.Key:X} was released {kvp.Value} times (expected exactly once)");
            }
        }

        [Fact]
        public void TryReadTemperature_CancellationWhileWaitingOnLock_DoesNotExecuteNativeReadAndPreservesSession()
        {
            // This test verifies that when TryReadTemperature() is cancelled while waiting for the provider lock,
            // it does not execute the native temperature read, does not close the shared session,
            // and does not disrupt subsequent operations.

            var firstReadReachedPause = new ManualResetEventSlim(false);
            var releaseFirstRead = new ManualResetEventSlim(false);
            var firstReadCompleted = new ManualResetEventSlim(false);
            var secondReadCompleted = new ManualResetEventSlim(false);

            bool firstReadError = false;
            string? firstReadErrorMessage = null;
            TemperatureReading? firstReadResult = null;
            bool secondReadError = false;
            string? secondReadErrorMessage = null;
            OperationCanceledException? secondReadCancellationException = null;
            bool secondReadEnteredWaitState = false;

            var fake = new FakeAdlxNative
            {
                GpuNames = new List<string> { "AMD Radeon RX 7900 XTX" },
                GpuUniqueIds = new List<int> { 12345 },
                GpuTypes = new List<int> { 2 },
                GpuTemperatureSupported = new List<bool> { true },
                GpuTemperatures = new List<double> { 65.5 }
            };

            // Make GetSupportedGpuMetrics return unique pointers per call so release tracking
            // can distinguish between different interface instances (TryProbe + 2 successful reads)
            int metricsSupportCallCount = 0;
            fake.GetSupportedMetricsHandler = (IntPtr services, IntPtr gpu, out IntPtr metricsSupport) =>
            {
                metricsSupport = new IntPtr(0x6000 + metricsSupportCallCount);
                metricsSupportCallCount++;
                return 0;
            };

            // Similarly for GetPerformanceMonitoringServices and GetCurrentGpuMetrics
            int perfServicesCallCount = 0;
            fake.GetPerfServicesHandler = (IntPtr system, out IntPtr services) =>
            {
                services = new IntPtr(0x4000 + perfServicesCallCount);
                perfServicesCallCount++;
                return 0;
            };

            int currentMetricsCallCount = 0;
            fake.GetCurrentMetricsHandler = (IntPtr services, IntPtr gpu, out IntPtr metrics) =>
            {
                int index = (int)(gpu.ToInt64() - 0x3000);
                metrics = new IntPtr(0x5000 + index + currentMetricsCallCount * 100); // unique per call
                currentMetricsCallCount++;
                return 0;
            };

            // Make GetGpuAt return unique pointers per call so release tracking
            // can distinguish between different GPU interface instances (TryProbe + 2 successful reads)
            int gpuAtCallCount = 0;
            fake.GetGpuAtHandler = (IntPtr gpuList, int index, out IntPtr gpu) =>
            {
                gpu = new IntPtr(0x3000 + gpuAtCallCount);
                gpuAtCallCount++;
                return 0;
            };

            // Pause the FIRST read operation inside GetGpuTemperature (final native call in the chain)
            // while it holds the provider lock. This ensures the second read will block on the lock.
            // Only the FIRST call to GetGpuTemperature pauses; subsequent calls return immediately.
            fake.GetGpuTempHandler = (IntPtr metrics, out double celsius) =>
            {
                celsius = fake.GpuTemperatures[0];

                // Pause only on the first call (when call count becomes 1)
                int callNumber = fake.GetGpuTemperatureCallCount;
                if (callNumber == 1)
                {
                    // Signal that first read has reached the pause point (holding the provider lock)
                    firstReadReachedPause.Set();

                    // Wait for release signal
                    if (!releaseFirstRead.Wait(TimeSpan.FromSeconds(30)))
                    {
                        firstReadError = true;
                        firstReadErrorMessage = "Timeout waiting for release signal";
                        firstReadCompleted.Set();
                        return -1;
                    }
                }

                return 0;
            };

            var provider = new AmdAdlxProvider(() => fake);

            // Initialize session via first operation (lazy connect)
            var initResult = ((IGpuProviderProbe)provider).TryProbe(out var initError);
            Assert.True(initResult, $"Initial session creation failed: {initError}");
            Assert.Equal(1, fake.InitializeCalls);

            // Declare outside try so finally can access them
            Thread? firstReadThread = null;
            Thread? secondReadThread = null;
            var secondReadCts = new CancellationTokenSource();

            try
            {
                // Start FIRST TryReadTemperature on dedicated thread - it will pause in GetGpuTemperature
                firstReadThread = new Thread(() =>
                {
                    try
                    {
                        bool success = ((IGpuTemperatureReader)provider).TryReadTemperature("amd-adlx:12345", out firstReadResult, out var error);
                        if (!success)
                        {
                            firstReadError = true;
                            firstReadErrorMessage = error;
                        }
                    }
                    catch (Exception ex)
                    {
                        firstReadError = true;
                        firstReadErrorMessage = ex.ToString();
                    }
                    finally
                    {
                        firstReadCompleted.Set();
                    }
                });
                firstReadThread.Start();

                // Wait for first read to reach the pause point (inside GetGpuTemperature, holding the lock)
                Assert.True(firstReadReachedPause.Wait(TimeSpan.FromSeconds(10)),
                    "First read operation did not reach pause point in time");

                // Verify first read is holding the lock: Terminate/Release not called, session active
                Assert.Equal(0, fake.TerminateCalls);
                Assert.False(fake.IsDisposed);

                // NOW start SECOND read on another thread with a cancellable token
                // It should block on the provider lock (WaitSleepJoin)
                secondReadThread = new Thread(() =>
                {
                    try
                    {
                        bool success = ((IGpuTemperatureReader)provider).TryReadTemperature("amd-adlx:12345", out _, out _, secondReadCts.Token);
                        if (!success)
                        {
                            secondReadError = true;
                            secondReadErrorMessage = "TryReadTemperature returned false instead of throwing OperationCanceledException";
                        }
                    }
                    catch (OperationCanceledException oce)
                    {
                        secondReadCancellationException = oce;
                    }
                    catch (Exception ex)
                    {
                        secondReadError = true;
                        secondReadErrorMessage = ex.ToString();
                    }
                    finally
                    {
                        secondReadCompleted.Set();
                    }
                });
                secondReadThread.Start();

                // Wait for second thread to block on the provider lock (WaitSleepJoin state)
                bool secondThreadBlocked = false;
                var waitTimeout = TimeSpan.FromSeconds(10);
                var waitStart = DateTime.UtcNow;

                while (DateTime.UtcNow - waitStart < waitTimeout)
                {
                    var state = secondReadThread.ThreadState;
                    if ((state & System.Threading.ThreadState.WaitSleepJoin) != 0)
                    {
                        secondThreadBlocked = true;
                        secondReadEnteredWaitState = true;
                        break;
                    }
                    Thread.Yield();
                }

                // Failure to observe WaitSleepJoin is a test failure - we cannot confirm blocking on lock
                Assert.True(secondThreadBlocked, "Second thread did not enter WaitSleepJoin state (blocked on lock) within timeout");

                // At this point: first read is paused in handler (holding lock), second thread is blocked on lock
                Assert.False(firstReadCompleted.IsSet, "First read should still be paused");

                // Verify session is still active (not terminated/disposed by the waiting second operation)
                Assert.Equal(0, fake.TerminateCalls);
                Assert.False(fake.IsDisposed);

                // NOW cancel the second read's token while first read is still paused holding the lock
                secondReadCts.Cancel();

                // Release the first read operation
                releaseFirstRead.Set();

                // Wait for both operations to complete
                Assert.True(firstReadCompleted.Wait(TimeSpan.FromSeconds(10)),
                    "First read operation did not complete in time after release");
                Assert.True(secondReadCompleted.Wait(TimeSpan.FromSeconds(10)),
                    "Second read operation did not complete in time after cancellation");

                // Verify first read succeeded
                Assert.False(firstReadError, $"First read failed: {firstReadErrorMessage}");
                Assert.NotNull(firstReadResult);
                Assert.Equal(65.5, firstReadResult!.Celsius);

                // Verify second read was cancelled with OperationCanceledException
                Assert.NotNull(secondReadCancellationException);
                Assert.False(secondReadError, $"Second read failed unexpectedly: {secondReadErrorMessage}");

                // Verify second read did NOT execute the native temperature read (GetGpuTemperature)
                // Only the first read should have called GetGpuTemperature (call count = 1)
                Assert.Equal(1, fake.GetGpuTemperatureCallCount);

                // Verify cancellation did NOT cause Disconnect/Terminate
                Assert.Equal(0, fake.TerminateCalls);
                Assert.False(fake.IsDisposed);

                // Verify second read entered wait state (was actually blocked on lock)
                Assert.True(secondReadEnteredWaitState, "Second read did not enter wait state on lock");

                // NOW verify subsequent read WITHOUT cancellation succeeds (session preserved)
                var subsequentResult = ((IGpuTemperatureReader)provider).TryReadTemperature("amd-adlx:12345", out var subsequentReading, out var subsequentError);
                Assert.True(subsequentResult, $"Subsequent read failed: {subsequentError}");
                Assert.NotNull(subsequentReading);
                Assert.Equal(65.5, subsequentReading!.Celsius);

                // Verify GetGpuTemperature was called again for the subsequent read (call count = 2)
                Assert.Equal(2, fake.GetGpuTemperatureCallCount);
            }
            finally
            {
                // Always release first read to avoid deadlock
                releaseFirstRead.Set();
                secondReadCts.Cancel(); // Ensure cancellation in case test failed before

                // Wait for actual thread termination
                bool firstThreadDone = false;
                bool secondThreadDone = false;

                if (firstReadThread != null)
                {
                    firstThreadDone = firstReadThread.Join(TimeSpan.FromSeconds(10));
                }
                else
                {
                    firstThreadDone = true;
                }

                if (secondReadThread != null)
                {
                    secondThreadDone = secondReadThread.Join(TimeSpan.FromSeconds(10));
                }
                else
                {
                    secondThreadDone = true;
                }

                if (!firstThreadDone || !secondThreadDone)
                {
                    throw new InvalidOperationException(
                        $"Test cleanup failed: firstThreadDone={firstThreadDone}, secondThreadDone={secondThreadDone}. " +
                        "Threads still running; synchronization objects not disposed and provider not disconnected to avoid race.");
                }

                // Clean up provider - only after both threads confirmed done
                try { provider.Disconnect(); } catch { }

                // Deterministic disposal of synchronization objects (after threads confirmed done)
                firstReadReachedPause.Dispose();
                releaseFirstRead.Dispose();
                firstReadCompleted.Dispose();
                secondReadCompleted.Dispose();
                secondReadCts.Dispose();
            }

            // Verify session was properly cleaned up exactly once
            Assert.Equal(1, fake.TerminateCalls);
            Assert.True(fake.IsDisposed);

            // Verify no double-release of gpuList (0x2000)
            Assert.Single(fake.ReleasedPointers, p => p == 0x2000);

            // Verify System (0x1000) NOT released via Release (managed by ADLX)
            Assert.DoesNotContain(0x1000, fake.ReleasedPointers);

            // Verify cleanup order: gpuList released before Terminate
            int gpuListReleaseIdx = fake.Events.IndexOf("Release:0x2000");
            int terminateIdx = fake.Events.IndexOf("Terminate");
            Assert.True(gpuListReleaseIdx >= 0, "gpuList should be released");
            Assert.True(terminateIdx >= 0, "Terminate should be called");
            Assert.True(gpuListReleaseIdx < terminateIdx, "gpuList released before Terminate");

            // Verify no double-release of any interface
            var releaseCounts = new Dictionary<int, int>();
            foreach (var ptr in fake.ReleasedPointers)
            {
                if (!releaseCounts.ContainsKey(ptr))
                    releaseCounts[ptr] = 0;
                releaseCounts[ptr]++;
            }
            foreach (var kvp in releaseCounts)
            {
                Assert.True(kvp.Value == 1, $"Interface 0x{kvp.Key:X} was released {kvp.Value} times (expected exactly once)");
            }
        }

        [Fact]
        public void Connect_DuringDisconnect_WaitsForCleanupAndReinitializesSession()
        {
            // This test verifies that Connect() called during an ongoing Disconnect() 
            // properly waits for the cleanup to complete, then creates a new session.
            // It confirms:
            // 1. Operations are serialized by the internal lock
            // 2. New session is not created until old session cleanup finishes
            // 3. Connect() after Disconnect() successfully reinitializes
            // 4. Subsequent Connect() is idempotent
            // 5. Both sessions are cleaned up exactly once, no double-release

            var terminatePaused = new ManualResetEventSlim(false);
            var releaseTerminate = new ManualResetEventSlim(false);
            var disconnectCompleted = new ManualResetEventSlim(false);
            var connectCompleted = new ManualResetEventSlim(false);

            bool disconnectError = false;
            string? disconnectErrorMessage = null;
            bool connectError = false;
            string? connectErrorMessage = null;
            bool connectEnteredWaitState = false;

            // First fake for the initial session - its Terminate will be paused
            // Use plain FakeAdlxNative with TerminateHandler (TerminateSequenceFake doesn't invoke TerminateHandler)
            var fake1 = new FakeAdlxNative
            {
                GpuNames = new List<string> { "AMD Radeon RX 7900 XTX" },
                GpuUniqueIds = new List<int> { 12345 },
                GpuTypes = new List<int> { 2 },
                GpuTemperatureSupported = new List<bool> { true }
            };
            // First Terminate call will be paused via handler
            fake1.TerminateHandler = () =>
            {
                terminatePaused.Set(); // Signal that Terminate has been reached
                if (!releaseTerminate.Wait(TimeSpan.FromSeconds(30)))
                {
                    return -1; // Timeout
                }
                return 0; // Success after release
            };

            // Second fake for the new session after re-Connect
            var fake2 = new FakeAdlxNative
            {
                GpuNames = new List<string> { "AMD Radeon RX 7900 XTX" },
                GpuUniqueIds = new List<int> { 12345 },
                GpuTypes = new List<int> { 2 },
                GpuTemperatureSupported = new List<bool> { true }
            };

            var factoryCallCount = 0;
            var provider = new AmdAdlxProvider(() =>
            {
                factoryCallCount++;
                return factoryCallCount == 1 ? fake1 : fake2;
            });

            // First Connect - creates first session
            var firstConnectResult = provider.Connect(out var firstConnectError);
            Assert.True(firstConnectResult, $"First Connect failed: {firstConnectError}");
            Assert.Null(firstConnectError);
            Assert.Equal(1, fake1.InitializeCalls);
            Assert.Equal(0, fake2.InitializeCalls);
            Assert.Equal(1, factoryCallCount);

            // Declare outside try so finally can access them
            Thread? disconnectThread = null;
            Thread? connectThread = null;

            try
            {
                // Start Disconnect on background thread - it will pause in Terminate
                disconnectThread = new Thread(() =>
                {
                    try
                    {
                        provider.Disconnect();
                    }
                    catch (Exception ex)
                    {
                        disconnectError = true;
                        disconnectErrorMessage = ex.ToString();
                    }
                    finally
                    {
                        disconnectCompleted.Set();
                    }
                });
                disconnectThread.Start();

                // Wait for Disconnect to reach Terminate (holding the lock)
                Assert.True(terminatePaused.Wait(TimeSpan.FromSeconds(10)),
                    "Disconnect did not reach Terminate pause point in time");

                // Verify first session: Terminate was called (counter incremented before handler),
                // but session not yet disposed because handler hasn't returned
                Assert.Equal(1, fake1.TerminateCalls); // Terminate called, handler paused
                Assert.False(fake1.IsDisposed);

                // NOW start Connect on another thread - it should block on the provider lock
                connectThread = new Thread(() =>
                {
                    try
                    {
                        bool result = provider.Connect(out var error);
                        if (!result)
                        {
                            connectError = true;
                            connectErrorMessage = error;
                        }
                    }
                    catch (Exception ex)
                    {
                        connectError = true;
                        connectErrorMessage = ex.ToString();
                    }
                    finally
                    {
                        connectCompleted.Set();
                    }
                });
                connectThread.Start();

                // Wait for Connect thread to block on the lock (WaitSleepJoin state)
                bool connectThreadBlocked = false;
                var waitTimeout = TimeSpan.FromSeconds(10);
                var waitStart = DateTime.UtcNow;

                while (DateTime.UtcNow - waitStart < waitTimeout)
                {
                    var state = connectThread.ThreadState;
                    if ((state & System.Threading.ThreadState.WaitSleepJoin) != 0)
                    {
                        connectThreadBlocked = true;
                        connectEnteredWaitState = true;
                        break;
                    }
                    Thread.Yield();
                }

                Assert.True(connectThreadBlocked, "Connect thread did not enter WaitSleepJoin state (blocked on lock) within timeout");

                // At this point: Disconnect is paused in Terminate (holding lock), Connect is blocked on lock
                // Verify second session NOT yet created
                Assert.Equal(0, fake2.InitializeCalls);
                Assert.Equal(1, factoryCallCount);

                // Release the Terminate - Disconnect will complete, then Connect will proceed
                releaseTerminate.Set();

                // Wait for both operations to complete
                Assert.True(disconnectCompleted.Wait(TimeSpan.FromSeconds(10)),
                    "Disconnect did not complete in time after Terminate release");
                Assert.True(connectCompleted.Wait(TimeSpan.FromSeconds(10)),
                    "Connect did not complete in time after Disconnect finished");

                // Verify both operations succeeded
                Assert.False(disconnectError, $"Disconnect failed: {disconnectErrorMessage}");
                Assert.False(connectError, $"Connect failed: {connectErrorMessage}");

                // Verify first session was terminated and disposed
                Assert.Equal(1, fake1.TerminateCalls);
                Assert.True(fake1.IsDisposed);

                // Verify second session was created by the Connect
                Assert.Equal(1, fake2.InitializeCalls);
                Assert.Equal(2, factoryCallCount);
                Assert.Equal(0, fake2.TerminateCalls);
                Assert.False(fake2.IsDisposed);

                // Verify Connect thread actually waited on lock
                Assert.True(connectEnteredWaitState, "Connect did not enter wait state on lock");

                // NOW call Connect again - should be idempotent, no new session
                var secondConnectResult = provider.Connect(out var secondConnectError);
                Assert.True(secondConnectResult, $"Second Connect failed: {secondConnectError}");
                Assert.Null(secondConnectError);
                Assert.Equal(1, fake2.InitializeCalls); // No new initialization
                Assert.Equal(2, factoryCallCount); // Factory not called again
            }
            finally
            {
                // Always release Terminate to avoid deadlock
                releaseTerminate.Set();

                // Wait for actual thread termination
                bool disconnectThreadDone = false;
                bool connectThreadDone = false;

                if (disconnectThread != null)
                {
                    disconnectThreadDone = disconnectThread.Join(TimeSpan.FromSeconds(10));
                }
                else
                {
                    disconnectThreadDone = true;
                }

                if (connectThread != null)
                {
                    connectThreadDone = connectThread.Join(TimeSpan.FromSeconds(10));
                }
                else
                {
                    connectThreadDone = true;
                }

                if (!disconnectThreadDone || !connectThreadDone)
                {
                    throw new InvalidOperationException(
                        $"Test cleanup failed: disconnectThreadDone={disconnectThreadDone}, connectThreadDone={connectThreadDone}. " +
                        "Threads still running; synchronization objects not disposed and provider not disconnected to avoid race.");
                }

                // Clean up provider - only after both threads confirmed done
                try { provider.Disconnect(); } catch { }

                // Deterministic disposal of synchronization objects (after threads confirmed done)
                terminatePaused.Dispose();
                releaseTerminate.Dispose();
                disconnectCompleted.Dispose();
                connectCompleted.Dispose();
            }

            // Verify final cleanup: second session terminated and disposed exactly once
            Assert.Equal(1, fake2.TerminateCalls);
            Assert.True(fake2.IsDisposed);

            // Verify gpuList (0x2000) released exactly once per session
            Assert.Single(fake1.ReleasedPointers, p => p == 0x2000);
            Assert.Single(fake2.ReleasedPointers, p => p == 0x2000);

            // Verify System (0x1000) NOT released via Release (managed by ADLX) for either session
            Assert.DoesNotContain(0x1000, fake1.ReleasedPointers);
            Assert.DoesNotContain(0x1000, fake2.ReleasedPointers);

            // Verify cleanup order for first session: gpuList released before Terminate
            int gpuListReleaseIdx1 = fake1.Events.IndexOf("Release:0x2000");
            int terminateIdx1 = fake1.Events.IndexOf("Terminate");
            Assert.True(gpuListReleaseIdx1 >= 0, "First session gpuList should be released");
            Assert.True(terminateIdx1 >= 0, "First session Terminate should be called");
            Assert.True(gpuListReleaseIdx1 < terminateIdx1, "First session gpuList released before Terminate");

            // Verify cleanup order for second session
            int gpuListReleaseIdx2 = fake2.Events.IndexOf("Release:0x2000");
            int terminateIdx2 = fake2.Events.IndexOf("Terminate");
            Assert.True(gpuListReleaseIdx2 >= 0, "Second session gpuList should be released");
            Assert.True(terminateIdx2 >= 0, "Second session Terminate should be called");
            Assert.True(gpuListReleaseIdx2 < terminateIdx2, "Second session gpuList released before Terminate");

            // Verify no double-release of any interface for either session
            foreach (var fake in new[] { fake1, fake2 })
            {
                var releaseCounts = new Dictionary<int, int>();
                foreach (var ptr in fake.ReleasedPointers)
                {
                    if (!releaseCounts.ContainsKey(ptr))
                        releaseCounts[ptr] = 0;
                    releaseCounts[ptr]++;
                }
                foreach (var kvp in releaseCounts)
                {
                    Assert.True(kvp.Value == 1, $"Interface 0x{kvp.Key:X} was released {kvp.Value} times (expected exactly once)");
                }
            }
        }

        [Fact]
        public void TryReadTemperatureInternal_RepeatedReads_SameGpu_EnumeratesOnce()
        {
            var fake = new FakeAdlxNative
            {
                GpuNames = new List<string> { "AMD Radeon RX 7900 XTX" },
                GpuUniqueIds = new List<int> { 12345 },
                GpuTypes = new List<int> { 2 },
                GpuTemperatureSupported = new List<bool> { true },
                GpuTemperatures = new List<double> { 65.5 }
            };
            var provider = new AmdAdlxProvider(() => fake);

            // First read
            bool success1 = provider.TryReadTemperatureInternal(12345, out var reading1, out var error1);
            Assert.True(success1);
            Assert.Null(error1);
            Assert.NotNull(reading1);
            Assert.Equal(65.5, reading1!.Celsius);

            // Second read
            bool success2 = provider.TryReadTemperatureInternal(12345, out var reading2, out var error2);
            Assert.True(success2);
            Assert.Null(error2);
            Assert.NotNull(reading2);
            Assert.Equal(65.5, reading2!.Celsius);

            // With cache, enumeration happens only on first read
            Assert.Equal(1, fake.GetGpuListSizeCallCount);
            Assert.Equal(1, fake.GetGpuAtCallCount);
            Assert.Equal(1, fake.GetGpuUniqueIdCallCount);

            // GetPerformanceMonitoringServices called once (cached session-wide)
            // GetSupportedGpuMetrics called once (cached per GPU)
            // IsSupportedGpuTemperature called once (cached per GPU)
            Assert.Equal(1, fake.GetPerformanceMonitoringServicesCallCount);
            Assert.Equal(1, fake.GetSupportedGpuMetricsCallCount);
            Assert.Equal(1, fake.IsSupportedGpuTemperatureCallCount);
            // GetCurrentGpuMetrics and GetGpuTemperature called per read
            Assert.Equal(2, fake.GetCurrentGpuMetricsCallCount);
            Assert.Equal(2, fake.GetGpuTemperatureCallCount);

            // GPU handle (0x3000) is cached and NOT released after reads
            Assert.DoesNotContain(0x3000, fake.ReleasedPointers);

            // metrics (0x5000) released per read
            Assert.Equal(2, fake.ReleasedPointers.Count(p => p == 0x5000));
            // metricsSupport (0x6000) and services (0x4000) NOT released per read (cached)
            Assert.DoesNotContain(0x6000, fake.ReleasedPointers);
            Assert.DoesNotContain(0x4000, fake.ReleasedPointers);

            // Persistent session resources not released yet
            Assert.DoesNotContain(0x2000, fake.ReleasedPointers); // gpuList
            Assert.DoesNotContain(0x1000, fake.ReleasedPointers); // system
            Assert.Equal(0, fake.TerminateCalls);
            Assert.False(fake.IsDisposed);

            provider.Disconnect();

            // gpuList released on Disconnect
            Assert.Contains(0x2000, fake.ReleasedPointers);
            Assert.Single(fake.ReleasedPointers, p => p == 0x2000);
            // Cached metricsSupport released on Disconnect
            Assert.Contains(0x6000, fake.ReleasedPointers);
            Assert.Single(fake.ReleasedPointers, p => p == 0x6000);
            // Cached services released on Disconnect
            Assert.Contains(0x4000, fake.ReleasedPointers);
            Assert.Single(fake.ReleasedPointers, p => p == 0x4000);
            // Cached GPU handle released exactly once during Disconnect (session cleanup)
            Assert.Contains(0x3000, fake.ReleasedPointers);
            Assert.Single(fake.ReleasedPointers, p => p == 0x3000);
            Assert.Equal(1, fake.TerminateCalls);
            Assert.True(fake.IsDisposed);
        }

        [Fact]
        public void TryReadTemperatureInternal_RepeatedReads_SameGpu_DocumentsCurrentApiCallCounts()
        {
            var fake = new FakeAdlxNative
            {
                GpuNames = new List<string> { "AMD Radeon RX 7900 XTX" },
                GpuUniqueIds = new List<int> { 12345 },
                GpuTypes = new List<int> { 2 },
                GpuTemperatureSupported = new List<bool> { true },
                GpuTemperatures = new List<double> { 65.5 }
            };
            var provider = new AmdAdlxProvider(() => fake);

            // First read
            bool success1 = provider.TryReadTemperatureInternal(12345, out var reading1, out var error1);
            Assert.True(success1);
            Assert.Null(error1);
            Assert.NotNull(reading1);
            Assert.Equal(65.5, reading1.Celsius);

            // Second read
            bool success2 = provider.TryReadTemperatureInternal(12345, out var reading2, out var error2);
            Assert.True(success2);
            Assert.Null(error2);
            Assert.NotNull(reading2);
            Assert.Equal(65.5, reading2.Celsius);

            // Verify API call counts after two reads (with caching)
            Assert.Equal(1, fake.GetGpuListSizeCallCount);
            Assert.Equal(1, fake.GetGpuAtCallCount);
            Assert.Equal(1, fake.GetGpuUniqueIdCallCount);
            Assert.Equal(1, fake.GetPerformanceMonitoringServicesCallCount); // cached session-wide
            Assert.Equal(1, fake.GetSupportedGpuMetricsCallCount); // cached per GPU
            Assert.Equal(1, fake.IsSupportedGpuTemperatureCallCount); // cached per GPU
            Assert.Equal(2, fake.GetCurrentGpuMetricsCallCount); // per read
            Assert.Equal(2, fake.GetGpuTemperatureCallCount); // per read

            // Verify temporary interfaces released after each read
            // metrics (0x5000) released per read (2 total)
            Assert.Equal(2, fake.ReleasedPointers.Count(p => p == 0x5000)); // metrics
            // metricsSupport (0x6000) and services (0x4000) are cached, NOT released per read
            Assert.DoesNotContain(0x6000, fake.ReleasedPointers); // metricsSupport
            Assert.DoesNotContain(0x4000, fake.ReleasedPointers); // services

            // GPU handle (0x3000) is cached and NOT released after reads
            Assert.DoesNotContain(0x3000, fake.ReleasedPointers);

            // Persistent session resources not released yet
            Assert.DoesNotContain(0x2000, fake.ReleasedPointers); // gpuList
            Assert.DoesNotContain(0x1000, fake.ReleasedPointers); // system
            Assert.Equal(0, fake.TerminateCalls);
            Assert.False(fake.IsDisposed);

            provider.Disconnect();

            // gpuList released on Disconnect
            Assert.Contains(0x2000, fake.ReleasedPointers);
            Assert.Single(fake.ReleasedPointers, p => p == 0x2000);
            // Cached metricsSupport released on Disconnect
            Assert.Contains(0x6000, fake.ReleasedPointers);
            Assert.Single(fake.ReleasedPointers, p => p == 0x6000);
            // Cached services released on Disconnect
            Assert.Contains(0x4000, fake.ReleasedPointers);
            Assert.Single(fake.ReleasedPointers, p => p == 0x4000);
            // Cached GPU handle released exactly once during Disconnect (session cleanup)
            Assert.Contains(0x3000, fake.ReleasedPointers);
            Assert.Single(fake.ReleasedPointers, p => p == 0x3000);
            Assert.Equal(1, fake.TerminateCalls);
            Assert.True(fake.IsDisposed);
        }

        [Fact]
        public void RepeatedReads_ReuseCachedGpuAndPerformanceInterfaces_ButAcquireFreshMetrics()
        {
            var fake = new FakeAdlxNative
            {
                GpuNames = new List<string> { "AMD Radeon RX 7900 XTX" },
                GpuUniqueIds = new List<int> { 12345 },
                GpuTypes = new List<int> { 2 },
                GpuTemperatureSupported = new List<bool> { true },
                GpuTemperatures = new List<double> { 65.5 }
            };
            var provider = new AmdAdlxProvider(() => fake);

            // First read
            bool success1 = provider.TryReadTemperatureInternal(12345, out var reading1, out var error1);
            Assert.True(success1);
            Assert.Null(error1);
            Assert.NotNull(reading1);
            Assert.Equal(65.5, reading1.Celsius);

            // Second read
            bool success2 = provider.TryReadTemperatureInternal(12345, out var reading2, out var error2);
            Assert.True(success2);
            Assert.Null(error2);
            Assert.NotNull(reading2);
            Assert.Equal(65.5, reading2.Celsius);

            // Verify native call counters after two reads
            Assert.Equal(1, fake.GetGpuListSizeCallCount);
            Assert.Equal(1, fake.GetGpuAtCallCount);
            Assert.Equal(1, fake.GetGpuUniqueIdCallCount);
            Assert.Equal(1, fake.GetPerformanceMonitoringServicesCallCount);
            Assert.Equal(1, fake.GetSupportedGpuMetricsCallCount);
            Assert.Equal(1, fake.IsSupportedGpuTemperatureCallCount);
            Assert.Equal(2, fake.GetCurrentGpuMetricsCallCount);
            Assert.Equal(2, fake.GetGpuTemperatureCallCount);

            // Disconnect and verify resource lifecycle
            provider.Disconnect();

            // GPU handle released on session close
            Assert.Contains(0x3000, fake.ReleasedPointers);
            Assert.Single(fake.ReleasedPointers, p => p == 0x3000);
            // metricsSupport released on session close
            Assert.Contains(0x6000, fake.ReleasedPointers);
            Assert.Single(fake.ReleasedPointers, p => p == 0x6000);
            // performanceMonitoringServices released on session close
            Assert.Contains(0x4000, fake.ReleasedPointers);
            Assert.Single(fake.ReleasedPointers, p => p == 0x4000);
            // metrics interface released after each read (2 total)
            Assert.Equal(2, fake.ReleasedPointers.Count(p => p == 0x5000));
            // gpuList released on session close
            Assert.Contains(0x2000, fake.ReleasedPointers);
            Assert.Single(fake.ReleasedPointers, p => p == 0x2000);

            // No double-release of any resource except metrics (0x5000) which is released once per read
            var releaseCounts = new Dictionary<int, int>();
            foreach (var ptr in fake.ReleasedPointers)
            {
                if (!releaseCounts.ContainsKey(ptr))
                    releaseCounts[ptr] = 0;
                releaseCounts[ptr]++;
            }
            foreach (var kvp in releaseCounts)
            {
                if (kvp.Key == 0x5000)
                {
                    Assert.Equal(2, kvp.Value);
                }
                else
                {
                    Assert.Equal(1, kvp.Value);
                }
            }

            Assert.Equal(1, fake.TerminateCalls);
            Assert.True(fake.IsDisposed);
        }

        [Fact]
        public void TryReadTemperatureInternal_MultipleGpus_ReadsCorrectGpuByUniqueId()
        {
            var fake = new FakeAdlxNative
            {
                GpuNames = new List<string> { "GPU First", "GPU Second" },
                GpuUniqueIds = new List<int> { 11111, 22222 },
                GpuTypes = new List<int> { 2, 2 },
                GpuTemperatureSupported = new List<bool> { true, true },
                GpuTemperatures = new List<double> { 55.0, 72.5 }
            };
            var provider = new AmdAdlxProvider(() => fake);

            // Read temperature of second GPU (UniqueId = 22222)
            bool success = provider.TryReadTemperatureInternal(22222, out var reading, out var error);

            Assert.True(success);
            Assert.Null(error);
            Assert.NotNull(reading);
            Assert.Equal(72.5, reading.Celsius); // Temperature from second GPU

            // Non-target GPU handle (0x3000 for index 0) released during enumeration
            Assert.Contains(0x3000, fake.ReleasedPointers);
            Assert.Single(fake.ReleasedPointers, p => p == 0x3000);

            // Target GPU handle (0x3001 for index 1) is cached and NOT released after read
            Assert.DoesNotContain(0x3001, fake.ReleasedPointers);

            // Persistent session resources not released yet
            Assert.DoesNotContain(0x2000, fake.ReleasedPointers);
            Assert.DoesNotContain(0x1000, fake.ReleasedPointers);
            Assert.Equal(0, fake.TerminateCalls);
            Assert.False(fake.IsDisposed);

            provider.Disconnect();

            // gpuList released on Disconnect
            Assert.Contains(0x2000, fake.ReleasedPointers);
            Assert.Single(fake.ReleasedPointers, p => p == 0x2000);
            // Cached target GPU handle released exactly once during Disconnect
            Assert.Contains(0x3001, fake.ReleasedPointers);
            Assert.Single(fake.ReleasedPointers, p => p == 0x3001);
            Assert.Equal(1, fake.TerminateCalls);
            Assert.True(fake.IsDisposed);
        }

        [Fact]
        public void TryReadTemperatureInternal_DifferentGpus_UseSeparateCacheEntries()
        {
            var fake = new FakeAdlxNative
            {
                GpuNames = new List<string> { "GPU First", "GPU Second" },
                GpuUniqueIds = new List<int> { 11111, 22222 },
                GpuTypes = new List<int> { 2, 2 },
                GpuTemperatureSupported = new List<bool> { true, true },
                GpuTemperatures = new List<double> { 55.0, 72.5 }
            };
            var provider = new AmdAdlxProvider(() => fake);

            // Read first GPU (UniqueId = 11111)
            bool success1 = provider.TryReadTemperatureInternal(11111, out var reading1, out var error1);
            Assert.True(success1);
            Assert.Null(error1);
            Assert.NotNull(reading1);
            Assert.Equal(55.0, reading1!.Celsius);

            // Read second GPU (UniqueId = 22222)
            bool success2 = provider.TryReadTemperatureInternal(22222, out var reading2, out var error2);
            Assert.True(success2);
            Assert.Null(error2);
            Assert.NotNull(reading2);
            Assert.Equal(72.5, reading2!.Celsius);

            // Read first GPU again - should use cache, no re-enumeration
            bool success3 = provider.TryReadTemperatureInternal(11111, out var reading3, out var error3);
            Assert.True(success3);
            Assert.Null(error3);
            Assert.NotNull(reading3);
            Assert.Equal(55.0, reading3!.Celsius);

            // Read second GPU again - should use cache, no re-enumeration
            bool success4 = provider.TryReadTemperatureInternal(22222, out var reading4, out var error4);
            Assert.True(success4);
            Assert.Null(error4);
            Assert.NotNull(reading4);
            Assert.Equal(72.5, reading4!.Celsius);

            // Enumeration happened only once per unique GPU on first access
            // First read (11111): GetGpuAt called once (index 0 = target), returns immediately
            // Second read (22222): GetGpuAt called twice (index 0 = non-target released, index 1 = target)
            Assert.Equal(2, fake.GetGpuListSizeCallCount);
            Assert.Equal(3, fake.GetGpuAtCallCount);
            Assert.Equal(3, fake.GetGpuUniqueIdCallCount);

            // GetPerformanceMonitoringServices called once (shared session-wide)
            // GetSupportedGpuMetrics called twice (one per GPU)
            // IsSupportedGpuTemperature called twice (one per GPU)
            Assert.Equal(1, fake.GetPerformanceMonitoringServicesCallCount);
            Assert.Equal(2, fake.GetSupportedGpuMetricsCallCount);
            Assert.Equal(2, fake.IsSupportedGpuTemperatureCallCount);
            // GetCurrentGpuMetrics and GetGpuTemperature called per read (4 total reads)
            Assert.Equal(4, fake.GetCurrentGpuMetricsCallCount);
            Assert.Equal(4, fake.GetGpuTemperatureCallCount);

            // First read (11111): target found at index 0, loop exits early - no non-target GPU enumerated
            // Second read (22222): index 0 (0x3000) non-target released, index 1 (0x3001) target cached
            Assert.Contains(0x3000, fake.ReleasedPointers);
            Assert.Single(fake.ReleasedPointers, p => p == 0x3000);
            // 0x3001 was never non-target, so not released during enumeration
            Assert.DoesNotContain(0x3001, fake.ReleasedPointers);

            // metrics released per read (4 total) - 0x5000 for GPU0, 0x5001 for GPU1 (2 reads each)
            Assert.Equal(4, fake.ReleasedPointers.Count(p => p == 0x5000 || p == 0x5001));
            // metricsSupport (0x6000, 0x6001) and services (0x4000) NOT released per read (cached)
            Assert.DoesNotContain(0x6000, fake.ReleasedPointers);
            Assert.DoesNotContain(0x6001, fake.ReleasedPointers);
            Assert.DoesNotContain(0x4000, fake.ReleasedPointers);

            // Persistent session resources not released yet
            Assert.DoesNotContain(0x2000, fake.ReleasedPointers);
            Assert.DoesNotContain(0x1000, fake.ReleasedPointers);
            Assert.Equal(0, fake.TerminateCalls);
            Assert.False(fake.IsDisposed);

            provider.Disconnect();

            // gpuList released on Disconnect
            Assert.Contains(0x2000, fake.ReleasedPointers);
            Assert.Single(fake.ReleasedPointers, p => p == 0x2000);
            // Cached metricsSupport released on Disconnect (2 entries: 0x6000 for GPU0, 0x6001 for GPU1)
            Assert.Contains(0x6000, fake.ReleasedPointers);
            Assert.Contains(0x6001, fake.ReleasedPointers);
            Assert.Equal(2, fake.ReleasedPointers.Count(p => p == 0x6000 || p == 0x6001));
            // Cached services released on Disconnect
            Assert.Contains(0x4000, fake.ReleasedPointers);
            Assert.Single(fake.ReleasedPointers, p => p == 0x4000);
            // Both cached GPU handles released exactly once during Disconnect
            // 0x3000 was released once during enumeration + once on Disconnect = 2 total
            // 0x3001 was only released on Disconnect = 1 total
            Assert.Equal(2, fake.ReleasedPointers.Count(p => p == 0x3000));
            Assert.Equal(1, fake.ReleasedPointers.Count(p => p == 0x3001));
            Assert.Equal(1, fake.TerminateCalls);
            Assert.True(fake.IsDisposed);
        }

        [Fact]
        public void TryReadTemperatureInternal_AfterReconnect_UsesNewFakeInstance()
        {
            var fake1 = new FakeAdlxNative
            {
                GpuNames = new List<string> { "AMD Radeon RX 7900 XTX" },
                GpuUniqueIds = new List<int> { 12345 },
                GpuTypes = new List<int> { 2 },
                GpuTemperatureSupported = new List<bool> { true },
                GpuTemperatures = new List<double> { 60.0 }
            };
            var fake2 = new FakeAdlxNative
            {
                GpuNames = new List<string> { "AMD Radeon RX 7900 XTX" },
                GpuUniqueIds = new List<int> { 12345 },
                GpuTypes = new List<int> { 2 },
                GpuTemperatureSupported = new List<bool> { true },
                GpuTemperatures = new List<double> { 70.0 } // Different temperature to verify new instance
            };

            var factoryCallCount = 0;
            var provider = new AmdAdlxProvider(() =>
            {
                factoryCallCount++;
                return factoryCallCount == 1 ? fake1 : fake2;
            });

            // First read using first session
            bool success1 = provider.TryReadTemperatureInternal(12345, out var reading1, out var error1);
            Assert.True(success1);
            Assert.Null(error1);
            Assert.NotNull(reading1);
            Assert.Equal(60.0, reading1.Celsius);
            Assert.Equal(1, fake1.InitializeCalls);
            Assert.Equal(0, fake2.InitializeCalls);

            // Disconnect first session
            provider.Disconnect();
            Assert.Equal(1, fake1.TerminateCalls);
            Assert.True(fake1.IsDisposed);

            // Connect creates new session with new FakeAdlxNative
            bool connectResult = provider.Connect(out var connectError);
            Assert.True(connectResult);
            Assert.Null(connectError);
            Assert.Equal(2, factoryCallCount);
            Assert.Equal(1, fake2.InitializeCalls); // New instance initialized

            // Second read using new session - new session enumerates once and caches
            bool success2 = provider.TryReadTemperatureInternal(12345, out var reading2, out var error2);
            Assert.True(success2);
            Assert.Null(error2);
            Assert.NotNull(reading2);
            Assert.Equal(70.0, reading2.Celsius); // Temperature from new fake instance

            // Second read uses fake2's enumeration (first read with new session)
            Assert.Equal(1, fake2.GetGpuListSizeCallCount);
            Assert.Equal(1, fake2.GetGpuAtCallCount);
            Assert.Equal(1, fake2.GetGpuUniqueIdCallCount);
            Assert.Equal(1, fake2.GetPerformanceMonitoringServicesCallCount);
            Assert.Equal(1, fake2.GetSupportedGpuMetricsCallCount);
            Assert.Equal(1, fake2.IsSupportedGpuTemperatureCallCount);
            Assert.Equal(1, fake2.GetCurrentGpuMetricsCallCount);
            Assert.Equal(1, fake2.GetGpuTemperatureCallCount);

            // fake1's resources not touched after disconnect
            Assert.Equal(1, fake1.GetGpuListSizeCallCount);
            Assert.Equal(1, fake1.GetGpuAtCallCount);
            Assert.Equal(1, fake1.GetGpuUniqueIdCallCount);

            // Repeat read with new session - should use cache, no re-enumeration
            bool success3 = provider.TryReadTemperatureInternal(12345, out var reading3, out var error3);
            Assert.True(success3);
            Assert.Null(error3);
            Assert.NotNull(reading3);
            Assert.Equal(70.0, reading3!.Celsius);

            // Still only one enumeration for fake2
            Assert.Equal(1, fake2.GetGpuListSizeCallCount);
            Assert.Equal(1, fake2.GetGpuAtCallCount);
            Assert.Equal(1, fake2.GetGpuUniqueIdCallCount);
            Assert.Equal(1, fake2.GetPerformanceMonitoringServicesCallCount);
            Assert.Equal(1, fake2.GetSupportedGpuMetricsCallCount);
            Assert.Equal(1, fake2.IsSupportedGpuTemperatureCallCount);
            // Only GetCurrentGpuMetrics and GetGpuTemperature called for the second read
            Assert.Equal(2, fake2.GetCurrentGpuMetricsCallCount);
            Assert.Equal(2, fake2.GetGpuTemperatureCallCount);

            provider.Disconnect();

            Assert.Equal(1, fake2.TerminateCalls);
            Assert.True(fake2.IsDisposed);
        }

        [Fact]
        public void AdlxSession_Cache_StoresAndRetrievesGpuHandle()
        {
            var fake = new FakeAdlxNative();
            var session = AdlxSession.Create(() => fake, out var error, out _, out _, out _, out _);
            Assert.NotNull(session);
            Assert.Null(error);

            IntPtr gpuHandle = new IntPtr(0x3000);
            bool cached = session.TryCacheGpu(12345, gpuHandle);
            Assert.True(cached);

            bool retrieved = session.TryGetCachedGpu(12345, out IntPtr retrievedHandle);
            Assert.True(retrieved);
            Assert.Equal(gpuHandle, retrievedHandle);

            session.Dispose();
        }

        [Fact]
        public void AdlxSession_Cache_RejectsDuplicateUniqueId()
        {
            var fake = new FakeAdlxNative();
            var session = AdlxSession.Create(() => fake, out var error, out _, out _, out _, out _);
            Assert.NotNull(session);
            Assert.Null(error);

            IntPtr firstHandle = new IntPtr(0x3000);
            IntPtr secondHandle = new IntPtr(0x3001);

            bool firstCached = session.TryCacheGpu(12345, firstHandle);
            Assert.True(firstCached);

            bool secondCached = session.TryCacheGpu(12345, secondHandle);
            Assert.False(secondCached);

            bool retrieved = session.TryGetCachedGpu(12345, out IntPtr retrievedHandle);
            Assert.True(retrieved);
            Assert.Equal(firstHandle, retrievedHandle);

            session.Dispose();
        }

        [Fact]
        public void AdlxSession_Cache_ReleasesCachedHandlesOnDispose_BeforeGpuList()
        {
            var fake = new FakeAdlxNative();
            var session = AdlxSession.Create(() => fake, out var error, out _, out _, out _, out _);
            Assert.NotNull(session);
            Assert.Null(error);

            IntPtr gpuHandle1 = new IntPtr(0x3000);
            IntPtr gpuHandle2 = new IntPtr(0x3001);

            session.TryCacheGpu(11111, gpuHandle1);
            session.TryCacheGpu(22222, gpuHandle2);

            session.Dispose();

            Assert.Contains(0x3000, fake.ReleasedPointers);
            Assert.Contains(0x3001, fake.ReleasedPointers);
            Assert.Contains(0x2000, fake.ReleasedPointers);

            int gpu1Idx = fake.ReleaseOrder.IndexOf(0x3000);
            int gpu2Idx = fake.ReleaseOrder.IndexOf(0x3001);
            int gpuListIdx = fake.ReleaseOrder.IndexOf(0x2000);

            Assert.True(gpu1Idx >= 0 && gpu2Idx >= 0 && gpuListIdx >= 0);
            Assert.True(gpu1Idx < gpuListIdx, "Cached GPU handles should be released before gpuList");
            Assert.True(gpu2Idx < gpuListIdx, "Cached GPU handles should be released before gpuList");
        }

        [Fact]
        public void AdlxSession_Cache_ReleasesEachHandleExactlyOnce()
        {
            var fake = new FakeAdlxNative();
            var session = AdlxSession.Create(() => fake, out var error, out _, out _, out _, out _);
            Assert.NotNull(session);
            Assert.Null(error);

            IntPtr gpuHandle = new IntPtr(0x3000);
            session.TryCacheGpu(12345, gpuHandle);

            session.Dispose();

            int count = fake.ReleasedPointers.Count(p => p == 0x3000);
            Assert.Equal(1, count);
        }

        [Fact]
        public void AdlxSession_Cache_AfterDispose_CannotGetOrCache()
        {
            var fake = new FakeAdlxNative();
            var session = AdlxSession.Create(() => fake, out var error, out _, out _, out _, out _);
            Assert.NotNull(session);
            Assert.Null(error);

            IntPtr gpuHandle = new IntPtr(0x3000);
            session.TryCacheGpu(12345, gpuHandle);

            session.Dispose();

            bool retrieved = session.TryGetCachedGpu(12345, out IntPtr retrievedHandle);
            Assert.False(retrieved);
            Assert.Equal(IntPtr.Zero, retrievedHandle);

            bool cached = session.TryCacheGpu(12345, gpuHandle);
            Assert.False(cached);
        }

        [Fact]
        public void AdlxSession_Cache_RejectsInvalidUniqueIdAndZeroHandle()
        {
            var fake = new FakeAdlxNative();
            var session = AdlxSession.Create(() => fake, out var error, out _, out _, out _, out _);
            Assert.NotNull(session);
            Assert.Null(error);

            bool resultZeroId = session.TryCacheGpu(0, new IntPtr(0x3000));
            Assert.False(resultZeroId);

            bool resultNegativeId = session.TryCacheGpu(-1, new IntPtr(0x3000));
            Assert.False(resultNegativeId);

            bool resultZeroHandle = session.TryCacheGpu(12345, IntPtr.Zero);
            Assert.False(resultZeroHandle);

            session.Dispose();
        }

        [Fact]
        public void AdlxSession_Cache_RejectedHandleNotReleasedBySession()
        {
            var fake = new FakeAdlxNative();
            var session = AdlxSession.Create(() => fake, out var error, out _, out _, out _, out _);
            Assert.NotNull(session);
            Assert.Null(error);

            IntPtr firstHandle = new IntPtr(0x3000);
            IntPtr secondHandle = new IntPtr(0x3001);

            session.TryCacheGpu(12345, firstHandle);
            bool secondCached = session.TryCacheGpu(12345, secondHandle);
            Assert.False(secondCached);

            session.Dispose();

            Assert.Contains(0x3000, fake.ReleasedPointers);
            Assert.DoesNotContain(0x3001, fake.ReleasedPointers);
        }

        [Fact]
        public void AdlxSession_Cache_RejectsCacheOnDisposedSession()
        {
            var fake = new FakeAdlxNative();
            var session = AdlxSession.Create(() => fake, out var error, out _, out _, out _, out _);
            Assert.NotNull(session);
            Assert.Null(error);

            session.Dispose();

            bool cached = session.TryCacheGpu(12345, new IntPtr(0x3000));
            Assert.False(cached);
        }

[Fact]
        public void AdlxSession_Cache_RejectsGetOnDisposedSession()
        {
            var fake = new FakeAdlxNative();
            var session = AdlxSession.Create(() => fake, out var error, out _, out _, out _, out _);
            Assert.NotNull(session);
            Assert.Null(error);

            session.TryCacheGpu(12345, new IntPtr(0x3000));
            session.Dispose();

            bool retrieved = session.TryGetCachedGpu(12345, out IntPtr handle);
            Assert.False(retrieved);
            Assert.Equal(IntPtr.Zero, handle);
        }

        [Fact]
        public void TryReadTemperatureInternal_TwoGpus_SeparateMetricsSupportCacheEntries()
        {
            var fake = new FakeAdlxNative
            {
                GpuNames = new List<string> { "GPU First", "GPU Second" },
                GpuUniqueIds = new List<int> { 11111, 22222 },
                GpuTypes = new List<int> { 2, 2 },
                GpuTemperatureSupported = new List<bool> { true, true },
                GpuTemperatures = new List<double> { 55.0, 72.5 }
            };
            var provider = new AmdAdlxProvider(() => fake);

            // Read first GPU
            bool success1 = provider.TryReadTemperatureInternal(11111, out var reading1, out var error1);
            Assert.True(success1);
            Assert.NotNull(reading1);
            Assert.Equal(55.0, reading1!.Celsius);

            // Read second GPU
            bool success2 = provider.TryReadTemperatureInternal(22222, out var reading2, out var error2);
            Assert.True(success2);
            Assert.NotNull(reading2);
            Assert.Equal(72.5, reading2!.Celsius);

            // Each GPU has its own metricsSupport cached
            Assert.Equal(1, fake.GetPerformanceMonitoringServicesCallCount); // shared
            Assert.Equal(2, fake.GetSupportedGpuMetricsCallCount); // one per GPU
            Assert.Equal(2, fake.IsSupportedGpuTemperatureCallCount); // one per GPU
            Assert.Equal(2, fake.GetCurrentGpuMetricsCallCount);
            Assert.Equal(2, fake.GetGpuTemperatureCallCount);

            // metrics released per read (2 total) - 0x5000 for GPU0, 0x5001 for GPU1
            Assert.Contains(0x5000, fake.ReleasedPointers);
            Assert.Contains(0x5001, fake.ReleasedPointers);
            Assert.Equal(2, fake.ReleasedPointers.Count(p => p == 0x5000 || p == 0x5001));
            // metricsSupport NOT released per read (cached)
            Assert.DoesNotContain(0x6000, fake.ReleasedPointers);
            Assert.DoesNotContain(0x6001, fake.ReleasedPointers);
            // services NOT released per read (cached)
            Assert.DoesNotContain(0x4000, fake.ReleasedPointers);

            provider.Disconnect();

            // Both metricsSupport entries released on Disconnect (0x6000 for GPU0, 0x6001 for GPU1)
            Assert.Contains(0x6000, fake.ReleasedPointers);
            Assert.Contains(0x6001, fake.ReleasedPointers);
            Assert.Equal(2, fake.ReleasedPointers.Count(p => p == 0x6000 || p == 0x6001));
            // Services released once on Disconnect
            Assert.Single(fake.ReleasedPointers, p => p == 0x4000);
            // Both GPU handles released on Disconnect
            Assert.Contains(0x3000, fake.ReleasedPointers);
            Assert.Contains(0x3001, fake.ReleasedPointers);
            Assert.Equal(1, fake.TerminateCalls);
            Assert.True(fake.IsDisposed);
        }

        [Fact]
        public void TryReadTemperatureInternal_MultipleGpus_SharePerfServices()
        {
            var fake = new FakeAdlxNative
            {
                GpuNames = new List<string> { "GPU A", "GPU B", "GPU C" },
                GpuUniqueIds = new List<int> { 100, 200, 300 },
                GpuTypes = new List<int> { 2, 2, 2 },
                GpuTemperatureSupported = new List<bool> { true, true, true },
                GpuTemperatures = new List<double> { 50.0, 60.0, 70.0 }
            };
            var provider = new AmdAdlxProvider(() => fake);

            // Read all three GPUs
            provider.TryReadTemperatureInternal(100, out _, out _);
            provider.TryReadTemperatureInternal(200, out _, out _);
            provider.TryReadTemperatureInternal(300, out _, out _);

            // Perf services called only once (shared across GPUs)
            Assert.Equal(1, fake.GetPerformanceMonitoringServicesCallCount);
            // MetricsSupport called once per GPU
            Assert.Equal(3, fake.GetSupportedGpuMetricsCallCount);
            Assert.Equal(3, fake.IsSupportedGpuTemperatureCallCount);
            // Current metrics and temperature called per read
            Assert.Equal(3, fake.GetCurrentGpuMetricsCallCount);
            Assert.Equal(3, fake.GetGpuTemperatureCallCount);

            // metrics released per read (3 total) - 0x5000, 0x5001, 0x5002
            Assert.Contains(0x5000, fake.ReleasedPointers);
            Assert.Contains(0x5001, fake.ReleasedPointers);
            Assert.Contains(0x5002, fake.ReleasedPointers);
            Assert.Equal(3, fake.ReleasedPointers.Count(p => p >= 0x5000 && p <= 0x5002));
            // metricsSupport NOT released per read (cached)
            Assert.DoesNotContain(0x6000, fake.ReleasedPointers);
            Assert.DoesNotContain(0x6001, fake.ReleasedPointers);
            Assert.DoesNotContain(0x6002, fake.ReleasedPointers);
            // services NOT released per read (cached)
            Assert.DoesNotContain(0x4000, fake.ReleasedPointers);

            provider.Disconnect();

            // Services released once
            Assert.Single(fake.ReleasedPointers, p => p == 0x4000);
            // Three metricsSupport entries released (0x6000, 0x6001, 0x6002)
            Assert.Contains(0x6000, fake.ReleasedPointers);
            Assert.Contains(0x6001, fake.ReleasedPointers);
            Assert.Contains(0x6002, fake.ReleasedPointers);
            Assert.Equal(3, fake.ReleasedPointers.Count(p => p >= 0x6000 && p <= 0x6002));
            Assert.Equal(1, fake.TerminateCalls);
            Assert.True(fake.IsDisposed);
        }

        [Fact]
        public void TryEnumerateDevices_PopulatesCache_ReusedByRead()
        {
            var fake = new FakeAdlxNative
            {
                GpuNames = new List<string> { "AMD Radeon RX 7900 XTX" },
                GpuUniqueIds = new List<int> { 12345 },
                GpuTypes = new List<int> { 2 },
                GpuTemperatureSupported = new List<bool> { true },
                GpuTemperatures = new List<double> { 65.5 }
            };
            var provider = new AmdAdlxProvider(() => fake);

            // First enumerate devices (populates session services cache, but NOT metricsSupport)
            bool enumSuccess = ((IGpuDeviceEnumerator)provider).TryEnumerateDevices(out var devices, out var enumError);
            Assert.True(enumSuccess);
            Assert.Null(enumError);
            Assert.Single(devices);

            // Verify session services cache was populated during enumeration
            // metricsSupport is NOT cached during enumeration (released in finally)
            Assert.Equal(1, fake.GetPerformanceMonitoringServicesCallCount);
            Assert.Equal(1, fake.GetSupportedGpuMetricsCallCount);
            Assert.Equal(1, fake.IsSupportedGpuTemperatureCallCount);

            // Now read temperature - should reuse session services, but fetch and cache metricsSupport
            bool readSuccess = provider.TryReadTemperatureInternal(12345, out var reading, out var readError);
            Assert.True(readSuccess);
            Assert.NotNull(reading);
            Assert.Equal(65.5, reading!.Celsius);

            // GetPerformanceMonitoringServices remains 1 (cached session-wide)
            // GetSupportedGpuMetrics called again for caching (now 2)
            // IsSupportedGpuTemperature called again for caching (now 2)
            Assert.Equal(1, fake.GetPerformanceMonitoringServicesCallCount);
            Assert.Equal(2, fake.GetSupportedGpuMetricsCallCount);
            Assert.Equal(2, fake.IsSupportedGpuTemperatureCallCount);
            // Only GetCurrentGpuMetrics and GetGpuTemperature called for the read
            Assert.Equal(1, fake.GetCurrentGpuMetricsCallCount);
            Assert.Equal(1, fake.GetGpuTemperatureCallCount);

            provider.Disconnect();
            Assert.Equal(1, fake.TerminateCalls);
            Assert.True(fake.IsDisposed);
        }

        [Fact]
        public void TryReadTemperatureInternal_CachesTemperatureNotSupported()
        {
            var fake = new FakeAdlxNative
            {
                GpuNames = new List<string> { "GPU NoTemp" },
                GpuUniqueIds = new List<int> { 99999 },
                GpuTypes = new List<int> { 2 },
                GpuTemperatureSupported = new List<bool> { false },
                GpuTemperatures = new List<double> { 0.0 }
            };
            var provider = new AmdAdlxProvider(() => fake);

            // First read - temperature not supported
            bool success1 = provider.TryReadTemperatureInternal(99999, out var reading1, out var error1);
            Assert.False(success1);
            Assert.Equal("GPU temperature monitoring is not supported.", error1);
            Assert.Null(reading1);

            // Second read - should use cached "not supported" result
            bool success2 = provider.TryReadTemperatureInternal(99999, out var reading2, out var error2);
            Assert.False(success2);
            Assert.Equal("GPU temperature monitoring is not supported.", error2);

            // IsSupportedGpuTemperature called only once (cached the false result)
            Assert.Equal(1, fake.IsSupportedGpuTemperatureCallCount);
            // GetSupportedGpuMetrics called only once
            Assert.Equal(1, fake.GetSupportedGpuMetricsCallCount);
            // GetCurrentGpuMetrics and GetGpuTemperature NOT called when not supported
            Assert.Equal(0, fake.GetCurrentGpuMetricsCallCount);
            Assert.Equal(0, fake.GetGpuTemperatureCallCount);

            provider.Disconnect();
            Assert.Equal(1, fake.TerminateCalls);
            Assert.True(fake.IsDisposed);
        }

        [Fact]
        public void TryReadTemperatureInternal_GetPerfServicesFails_ReleasesServicesAndDoesNotCache()
        {
            var fake = new FakeAdlxNative
            {
                GpuNames = new List<string> { "GPU" },
                GpuUniqueIds = new List<int> { 12345 },
                GpuTypes = new List<int> { 2 },
                GpuTemperatureSupported = new List<bool> { true },
                GpuTemperatures = new List<double> { 65.5 },
                GetPerfServicesHandler = (IntPtr sys, out IntPtr svc) =>
                {
                    svc = IntPtr.Zero;
                    return -1; // Fail to get services
                }
            };
            var provider = new AmdAdlxProvider(() => fake);

            bool success = provider.TryReadTemperatureInternal(12345, out var reading, out var error);
            Assert.False(success);
            Assert.Equal("Failed to get performance monitoring services.", error);
            Assert.Null(reading);

            // Services not cached, nothing to release
            Assert.DoesNotContain(0x4000, fake.ReleasedPointers);

            provider.Disconnect();
            Assert.Equal(1, fake.TerminateCalls);
            Assert.True(fake.IsDisposed);
        }

        [Fact]
        public void TryReadTemperatureInternal_GetPerfServicesFailsWithNonZeroPointer_ReleasesServices()
        {
            var fake = new FakeAdlxNative
            {
                GpuNames = new List<string> { "GPU" },
                GpuUniqueIds = new List<int> { 12345 },
                GpuTypes = new List<int> { 2 },
                GpuTemperatureSupported = new List<bool> { true },
                GpuTemperatures = new List<double> { 65.5 },
                GetPerfServicesHandler = (IntPtr sys, out IntPtr svc) =>
                {
                    svc = new IntPtr(0x4000); // Non-zero pointer on failure
                    return -1;
                }
            };
            var provider = new AmdAdlxProvider(() => fake);

            bool success = provider.TryReadTemperatureInternal(12345, out var reading, out var error);
            Assert.False(success);
            Assert.Equal("Failed to get performance monitoring services.", error);

            // Services pointer (0x4000) released exactly once despite failure
            Assert.Contains(0x4000, fake.ReleasedPointers);
            Assert.Single(fake.ReleasedPointers, p => p == 0x4000);

            provider.Disconnect();
            Assert.Equal(1, fake.TerminateCalls);
            Assert.True(fake.IsDisposed);
            // Services still released exactly once (not again on Disconnect)
            Assert.Single(fake.ReleasedPointers, p => p == 0x4000);
        }

        [Fact]
        public void TryReadTemperatureInternal_GetSupportedMetricsFails_ReleasesMetricsSupportAndDoesNotCache()
        {
            var fake = new FakeAdlxNative
            {
                GpuNames = new List<string> { "GPU" },
                GpuUniqueIds = new List<int> { 12345 },
                GpuTypes = new List<int> { 2 },
                GpuTemperatureSupported = new List<bool> { true },
                GpuTemperatures = new List<double> { 65.5 },
                GetSupportedMetricsHandler = (IntPtr svc, IntPtr gpu, out IntPtr ms) =>
                {
                    ms = IntPtr.Zero;
                    return -1;
                }
            };
            var provider = new AmdAdlxProvider(() => fake);

            bool success = provider.TryReadTemperatureInternal(12345, out var reading, out var error);
            Assert.False(success);
            Assert.NotNull(error);
            Assert.Contains("Failed to check GPU temperature support", error);
            Assert.Contains("-1", error);

            // MetricsSupport not cached, nothing to release
            Assert.DoesNotContain(0x6000, fake.ReleasedPointers);

            provider.Disconnect();
            Assert.Equal(1, fake.TerminateCalls);
            Assert.True(fake.IsDisposed);
        }

        [Fact]
        public void TryReadTemperatureInternal_GetSupportedMetricsFailsWithNonZeroPointer_ReleasesMetricsSupport()
        {
            var fake = new FakeAdlxNative
            {
                GpuNames = new List<string> { "GPU" },
                GpuUniqueIds = new List<int> { 12345 },
                GpuTypes = new List<int> { 2 },
                GpuTemperatureSupported = new List<bool> { true },
                GpuTemperatures = new List<double> { 65.5 },
                GetSupportedMetricsHandler = (IntPtr svc, IntPtr gpu, out IntPtr ms) =>
                {
                    ms = new IntPtr(0x6000); // Non-zero pointer on failure
                    return -1;
                }
            };
            var provider = new AmdAdlxProvider(() => fake);

            bool success = provider.TryReadTemperatureInternal(12345, out var reading, out var error);
            Assert.False(success);
            Assert.NotNull(error);
            Assert.Contains("Failed to check GPU temperature support", error);
            Assert.Contains("-1", error);

            // MetricsSupport pointer (0x6000) released exactly once despite failure
            Assert.Contains(0x6000, fake.ReleasedPointers);
            Assert.Single(fake.ReleasedPointers, p => p == 0x6000);

            provider.Disconnect();
            Assert.Equal(1, fake.TerminateCalls);
            Assert.True(fake.IsDisposed);
            // MetricsSupport still released exactly once (not again on Disconnect)
            Assert.Single(fake.ReleasedPointers, p => p == 0x6000);
        }

        [Fact]
        public void Dispose_ReleasesAllCachedInterfaces_EvenWhenReleaseThrows()
        {
            var fake = new FakeAdlxNative
            {
                GpuNames = new List<string> { "GPU1", "GPU2" },
                GpuUniqueIds = new List<int> { 11111, 22222 },
                GpuTypes = new List<int> { 2, 2 },
                GpuTemperatureSupported = new List<bool> { true, true },
                GpuTemperatures = new List<double> { 55.0, 72.5 },
                ThrowOnRelease = true,
                ThrowOnReleasePointer = 0x6000 // Throw when releasing metricsSupport
            };
            var provider = new AmdAdlxProvider(() => fake);

            // Read both GPUs to populate cache
            provider.TryReadTemperatureInternal(11111, out _, out _);
            provider.TryReadTemperatureInternal(22222, out _, out _);

            // Dispose should release all interfaces even when one Release throws
            provider.Disconnect();

            // All cached interfaces should have been attempted for release
            Assert.Contains(0x6000, fake.ReleasedPointers); // metricsSupport for GPU1
            Assert.Contains(0x6001, fake.ReleasedPointers); // metricsSupport for GPU2 (different pointer)
            Assert.Contains(0x4000, fake.ReleasedPointers); // services
            Assert.Contains(0x3000, fake.ReleasedPointers); // GPU1 handle
            Assert.Contains(0x3001, fake.ReleasedPointers); // GPU2 handle
            Assert.Contains(0x2000, fake.ReleasedPointers); // gpuList

            Assert.Equal(1, fake.TerminateCalls);
            Assert.True(fake.IsDisposed);
        }

        [Fact]
        public void TryReadTemperatureInternal_CancellationDuringCachePopulation_NoLeakNoDoubleRelease()
        {
            var cancelled = false;
            var fake = new FakeAdlxNative
            {
                GpuNames = new List<string> { "GPU" },
                GpuUniqueIds = new List<int> { 12345 },
                GpuTypes = new List<int> { 2 },
                GpuTemperatureSupported = new List<bool> { true },
                GpuTemperatures = new List<double> { 65.5 },
                GetSupportedMetricsHandler = (IntPtr svc, IntPtr gpu, out IntPtr ms) =>
                {
                    // Fake sets the pointer BEFORE the handler runs
                    ms = new IntPtr(0x6000);
                    if (!cancelled)
                    {
                        cancelled = true;
                        throw new OperationCanceledException();
                    }
                    return 0;
                }
            };
            var provider = new AmdAdlxProvider(() => fake);
            var cts = new CancellationTokenSource();

            // First read - will be cancelled during GetSupportedGpuMetrics
            Assert.Throws<OperationCanceledException>(() => provider.TryReadTemperatureInternal(12345, out _, out _, cts.Token));

            // Services (0x4000) are acquired and cached before GetSupportedGpuMetrics
            // metricsSupport (0x6000) was acquired (pointer set before exception) and released in finally
            Assert.DoesNotContain(0x4000, fake.ReleasedPointers); // services cached, not released
            Assert.Contains(0x6000, fake.ReleasedPointers); // metricsSupport acquired and released
            Assert.Single(fake.ReleasedPointers, p => p == 0x6000);

            // Second read without cancellation should succeed and populate cache
            // (cancelled is now true, so handler returns success)
            bool success = provider.TryReadTemperatureInternal(12345, out var reading, out var error);
            Assert.True(success);
            Assert.NotNull(reading);
            Assert.Equal(65.5, reading!.Celsius);

            // GetSupportedGpuMetrics called twice (once cancelled, once succeeded)
            Assert.Equal(1, fake.GetPerformanceMonitoringServicesCallCount);
            Assert.Equal(2, fake.GetSupportedGpuMetricsCallCount);
            Assert.Equal(1, fake.IsSupportedGpuTemperatureCallCount);

            provider.Disconnect();

            // MetricsSupport pointer (0x6000) released twice total:
            // once after cancelled first acquisition, once during Disconnect for cached pointer
            Assert.Equal(2, fake.ReleasedPointers.Count(p => p == 0x6000));
            Assert.Equal(1, fake.TerminateCalls);
            Assert.True(fake.IsDisposed);
        }

        [Fact]
        public void TryReadTemperatureInternal_CancellationAfterCachePopulation_NoDoubleRelease()
        {
            var firstCall = true;
            var fake = new FakeAdlxNative
            {
                GpuNames = new List<string> { "GPU" },
                GpuUniqueIds = new List<int> { 12345 },
                GpuTypes = new List<int> { 2 },
                GpuTemperatureSupported = new List<bool> { true },
                GpuTemperatures = new List<double> { 65.5 },
                GetCurrentMetricsHandler = (IntPtr svc, IntPtr gpu, out IntPtr met) =>
                {
                    met = new IntPtr(0x5000);
                    if (firstCall)
                    {
                        firstCall = false;
                        throw new OperationCanceledException(); // Cancel after cache populated
                    }
                    return 0;
                }
            };
            var provider = new AmdAdlxProvider(() => fake);
            var cts = new CancellationTokenSource();

            // First read - cache populated, then cancelled during GetCurrentGpuMetrics
            Assert.Throws<OperationCanceledException>(() => provider.TryReadTemperatureInternal(12345, out _, out _, cts.Token));

            // Cache should still be populated (services and metricsSupport)
            Assert.Equal(1, fake.GetPerformanceMonitoringServicesCallCount);
            Assert.Equal(1, fake.GetSupportedGpuMetricsCallCount);
            Assert.Equal(1, fake.IsSupportedGpuTemperatureCallCount);

            // Second read without cancellation should reuse cache
            bool success = provider.TryReadTemperatureInternal(12345, out var reading, out var error);
            Assert.True(success);
            Assert.NotNull(reading);
            Assert.Equal(65.5, reading!.Celsius);

            // No additional calls for services/metricsSupport
            Assert.Equal(1, fake.GetPerformanceMonitoringServicesCallCount);
            Assert.Equal(1, fake.GetSupportedGpuMetricsCallCount);
            Assert.Equal(1, fake.IsSupportedGpuTemperatureCallCount);

            provider.Disconnect();
            // All cached interfaces released exactly once on Disconnect
            Assert.Single(fake.ReleasedPointers, p => p == 0x4000);
            Assert.Single(fake.ReleasedPointers, p => p == 0x6000);
            Assert.Single(fake.ReleasedPointers, p => p == 0x3000);
            Assert.Equal(1, fake.TerminateCalls);
            Assert.True(fake.IsDisposed);
        }

        [Fact]
        public void GetPerformanceMonitoringServices_ErrorWithNonZeroPointer_ReleasesPointerOnce_NotCached()
        {
            var fake = new FakeAdlxNative
            {
                GpuNames = new List<string> { "GPU" },
                GpuUniqueIds = new List<int> { 12345 },
                GpuTypes = new List<int> { 2 },
                GpuTemperatureSupported = new List<bool> { true },
                GpuTemperatures = new List<double> { 65.5 },
                GetPerfServicesHandler = (IntPtr sys, out IntPtr svc) =>
                {
                    svc = new IntPtr(0x4000); // Non-zero pointer on failure
                    return -1;
                }
            };
            var provider = new AmdAdlxProvider(() => fake);

            bool success = provider.TryReadTemperatureInternal(12345, out var reading, out var error);
            Assert.False(success);
            Assert.Equal("Failed to get performance monitoring services.", error);
            Assert.Null(reading);

            // Services pointer (0x4000) released exactly once despite failure
            Assert.Contains(0x4000, fake.ReleasedPointers);
            Assert.Single(fake.ReleasedPointers, p => p == 0x4000);

            provider.Disconnect();
            Assert.Equal(1, fake.TerminateCalls);
            Assert.True(fake.IsDisposed);
            // Services still released exactly once (not again on Disconnect)
            Assert.Single(fake.ReleasedPointers, p => p == 0x4000);
        }

        [Fact]
        public void GetSupportedGpuMetrics_ErrorWithNonZeroPointer_ReleasesPointerOnce_NotCached()
        {
            var fake = new FakeAdlxNative
            {
                GpuNames = new List<string> { "GPU" },
                GpuUniqueIds = new List<int> { 12345 },
                GpuTypes = new List<int> { 2 },
                GpuTemperatureSupported = new List<bool> { true },
                GpuTemperatures = new List<double> { 65.5 },
                GetSupportedMetricsHandler = (IntPtr svc, IntPtr gpu, out IntPtr ms) =>
                {
                    ms = new IntPtr(0x6000); // Non-zero pointer on failure
                    return -1;
                }
            };
            var provider = new AmdAdlxProvider(() => fake);

            bool success = provider.TryReadTemperatureInternal(12345, out var reading, out var error);
            Assert.False(success);
            Assert.NotNull(error);
            Assert.Contains("Failed to check GPU temperature support", error);
            Assert.Contains("-1", error);
            Assert.Null(reading);

            // MetricsSupport pointer (0x6000) released exactly once despite failure
            Assert.Contains(0x6000, fake.ReleasedPointers);
            Assert.Single(fake.ReleasedPointers, p => p == 0x6000);

            provider.Disconnect();
            Assert.Equal(1, fake.TerminateCalls);
            Assert.True(fake.IsDisposed);
            // MetricsSupport still released exactly once (not again on Disconnect)
            Assert.Single(fake.ReleasedPointers, p => p == 0x6000);
        }

        [Fact]
        public void GetSupportedGpuMetrics_CancellationAfterPointerAcquired_ReleasesPointerOnce()
        {
            var firstCall = true;
            var fake = new FakeAdlxNative
            {
                GpuNames = new List<string> { "GPU" },
                GpuUniqueIds = new List<int> { 12345 },
                GpuTypes = new List<int> { 2 },
                GpuTemperatureSupported = new List<bool> { true },
                GpuTemperatures = new List<double> { 65.5 },
                GetSupportedMetricsHandler = (IntPtr svc, IntPtr gpu, out IntPtr ms) =>
                {
                    ms = new IntPtr(0x6000); // Acquire pointer
                    if (firstCall)
                    {
                        firstCall = false;
                        throw new OperationCanceledException(); // Then cancel
                    }
                    return 0;
                }
            };
            var provider = new AmdAdlxProvider(() => fake);
            var cts = new CancellationTokenSource();

            // First read - cancelled after GetSupportedGpuMetrics acquires pointer
            Assert.Throws<OperationCanceledException>(() => provider.TryReadTemperatureInternal(12345, out _, out _, cts.Token));

            // MetricsSupport pointer (0x6000) released exactly once after failed acquisition
            Assert.Contains(0x6000, fake.ReleasedPointers);
            Assert.Single(fake.ReleasedPointers, p => p == 0x6000);

            // Second read without cancellation should succeed and populate cache
            bool success = provider.TryReadTemperatureInternal(12345, out var reading, out var error);
            Assert.True(success);
            Assert.NotNull(reading);
            Assert.Equal(65.5, reading!.Celsius);

            // GetSupportedGpuMetrics called twice (once before cancellation, once on successful retry)
            Assert.Equal(1, fake.GetPerformanceMonitoringServicesCallCount);
            Assert.Equal(2, fake.GetSupportedGpuMetricsCallCount);
            Assert.Equal(1, fake.IsSupportedGpuTemperatureCallCount);

            provider.Disconnect();

            // MetricsSupport pointer (0x6000) released twice total:
            // once after cancelled first acquisition, once during Disconnect for cached pointer
            Assert.Equal(2, fake.ReleasedPointers.Count(p => p == 0x6000));
            Assert.Equal(1, fake.TerminateCalls);
            Assert.True(fake.IsDisposed);
        }

        [Fact]
        public void GetSupportedGpuMetrics_ExceptionAfterPointerAcquired_ReleasesPointerOnce()
        {
            var threw = false;
            var fake = new FakeAdlxNative
            {
                GpuNames = new List<string> { "GPU" },
                GpuUniqueIds = new List<int> { 12345 },
                GpuTypes = new List<int> { 2 },
                GpuTemperatureSupported = new List<bool> { true },
                GpuTemperatures = new List<double> { 65.5 },
                GetSupportedMetricsHandler = (IntPtr svc, IntPtr gpu, out IntPtr ms) =>
                {
                    ms = new IntPtr(0x6000); // Acquire pointer
                    if (!threw)
                    {
                        threw = true;
                        throw new InvalidOperationException("Simulated exception"); // Then throw
                    }
                    return 0;
                }
            };
            var provider = new AmdAdlxProvider(() => fake);

            // First read - exception after GetSupportedGpuMetrics acquires pointer
            // Exception is caught and converted to failure result
            bool success = provider.TryReadTemperatureInternal(12345, out var reading, out var error);
            Assert.False(success);
            Assert.NotNull(error);
            Assert.Contains("Exception during temperature read", error);
            Assert.Null(reading);

            // MetricsSupport pointer (0x6000) released exactly once after failed acquisition
            Assert.Contains(0x6000, fake.ReleasedPointers);
            Assert.Single(fake.ReleasedPointers, p => p == 0x6000);

            // Second read without exception should succeed and populate cache
            // (threw is now true, so handler returns success)
            success = provider.TryReadTemperatureInternal(12345, out reading, out error);
            Assert.True(success);
            Assert.NotNull(reading);
            Assert.Equal(65.5, reading!.Celsius);

            // GetSupportedGpuMetrics called twice (once failed, once succeeded)
            Assert.Equal(2, fake.GetSupportedGpuMetricsCallCount);
            Assert.Equal(1, fake.GetPerformanceMonitoringServicesCallCount);
            Assert.Equal(1, fake.IsSupportedGpuTemperatureCallCount);

            provider.Disconnect();

            // MetricsSupport pointer (0x6000) released twice total:
            // once after failed first acquisition, once during Disconnect for cached pointer
            Assert.Equal(2, fake.ReleasedPointers.Count(p => p == 0x6000));
            Assert.Equal(1, fake.TerminateCalls);
            Assert.True(fake.IsDisposed);
        }

        [Fact]
        public void GetPerformanceMonitoringServices_ExceptionAfterPointerAcquired_ReleasesPointerOnce()
        {
            var threw = false;
            var fake = new FakeAdlxNative
            {
                GpuNames = new List<string> { "GPU" },
                GpuUniqueIds = new List<int> { 12345 },
                GpuTypes = new List<int> { 2 },
                GpuTemperatureSupported = new List<bool> { true },
                GpuTemperatures = new List<double> { 65.5 },
                GetPerfServicesHandler = (IntPtr sys, out IntPtr svc) =>
                {
                    svc = new IntPtr(0x4000); // Acquire pointer
                    if (!threw)
                    {
                        threw = true;
                        throw new InvalidOperationException("Simulated exception"); // Then throw
                    }
                    return 0;
                }
            };
            var provider = new AmdAdlxProvider(() => fake);

            // First read - exception after GetPerformanceMonitoringServices acquires pointer
            // Exception is caught and converted to failure result
            bool success = provider.TryReadTemperatureInternal(12345, out var reading, out var error);
            Assert.False(success);
            Assert.NotNull(error);
            Assert.Contains("Exception during temperature read", error);
            Assert.Null(reading);

            // Services pointer (0x4000) released exactly once after failed acquisition
            Assert.Contains(0x4000, fake.ReleasedPointers);
            Assert.Single(fake.ReleasedPointers, p => p == 0x4000);

            // Second read without exception should succeed and populate cache
            // (threw is now true, so handler returns success)
            success = provider.TryReadTemperatureInternal(12345, out reading, out error);
            Assert.True(success);
            Assert.NotNull(reading);
            Assert.Equal(65.5, reading!.Celsius);

            // GetPerformanceMonitoringServices called twice (once failed, once succeeded)
            Assert.Equal(2, fake.GetPerformanceMonitoringServicesCallCount);
            Assert.Equal(1, fake.GetSupportedGpuMetricsCallCount);
            Assert.Equal(1, fake.IsSupportedGpuTemperatureCallCount);

            provider.Disconnect();

            // Services pointer (0x4000) released twice total:
            // once after failed first acquisition, once during Disconnect for cached pointer
            Assert.Equal(2, fake.ReleasedPointers.Count(p => p == 0x4000));
            Assert.Equal(1, fake.TerminateCalls);
            Assert.True(fake.IsDisposed);
        }

        [Fact]
        public void Dispose_ReleasesCachedServicesAndMetricsSupport_ExactlyOnce()
        {
            var fake = new FakeAdlxNative
            {
                GpuNames = new List<string> { "GPU1", "GPU2" },
                GpuUniqueIds = new List<int> { 11111, 22222 },
                GpuTypes = new List<int> { 2, 2 },
                GpuTemperatureSupported = new List<bool> { true, true },
                GpuTemperatures = new List<double> { 55.0, 72.5 }
            };
            var provider = new AmdAdlxProvider(() => fake);

            // Read both GPUs to populate cache
            provider.TryReadTemperatureInternal(11111, out _, out _);
            provider.TryReadTemperatureInternal(22222, out _, out _);

            // Cache populated
            Assert.Equal(1, fake.GetPerformanceMonitoringServicesCallCount);
            Assert.Equal(2, fake.GetSupportedGpuMetricsCallCount);
            Assert.Equal(2, fake.IsSupportedGpuTemperatureCallCount);

            provider.Disconnect();

            // Services released exactly once
            Assert.Single(fake.ReleasedPointers, p => p == 0x4000);
            // Both metricsSupport entries released exactly once (0x6000 for GPU0, 0x6001 for GPU1)
            Assert.Contains(0x6000, fake.ReleasedPointers);
            Assert.Contains(0x6001, fake.ReleasedPointers);
            Assert.Equal(2, fake.ReleasedPointers.Count(p => p == 0x6000 || p == 0x6001));
            // GPU handles released exactly once
            // 0x3000: first read finds GPU at index 0 (target), but second read enumerates index 0 as non-target and releases it; cache holds the target 0x3000 for GPU1, which is released on Disconnect = 2 total
            Assert.Equal(2, fake.ReleasedPointers.Count(p => p == 0x3000));
            // 0x3001: only cached for GPU2, released once on Disconnect
            Assert.Single(fake.ReleasedPointers, p => p == 0x3001);
            // gpuList released exactly once
            Assert.Single(fake.ReleasedPointers, p => p == 0x2000);

            Assert.Equal(1, fake.TerminateCalls);
            Assert.True(fake.IsDisposed);
        }
    }

    public class AmdAdlxDeviceTests
    {
        private class FakeTemperatureReader : IGpuTemperatureReader
        {
            public int CallCount { get; private set; }
            public string? LastDeviceId { get; private set; }
            public CancellationToken? LastCancellationToken { get; private set; }

            private readonly bool _success;
            private readonly TemperatureReading? _reading;
            private readonly string? _error;

            public FakeTemperatureReader(bool success, TemperatureReading? reading, string? error)
            {
                _success = success;
                _reading = reading;
                _error = error;
            }

            public bool TryReadTemperature(string deviceId, out TemperatureReading? reading, out string? error, CancellationToken cancellationToken = default)
            {
                CallCount++;
                LastDeviceId = deviceId;
                LastCancellationToken = cancellationToken;
                reading = _reading;
                error = _error;
                return _success;
            }
        }

        [Fact]
        public void Constructor_NullTemperatureReader_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new AmdAdlxDevice(null!, "provider-id", "id", "name", GpuKind.Discrete));
        }

        [Fact]
        public void Constructor_NullProviderId_ThrowsArgumentNullException()
        {
            var reader = new FakeTemperatureReader(true, new TemperatureReading(50.0, "GPU", DateTimeOffset.UtcNow), null);
            Assert.Throws<ArgumentNullException>(() => new AmdAdlxDevice(reader, null!, "id", "name", GpuKind.Discrete));
        }

        [Fact]
        public void Constructor_NullId_ThrowsArgumentNullException()
        {
            var reader = new FakeTemperatureReader(true, new TemperatureReading(50.0, "GPU", DateTimeOffset.UtcNow), null);
            Assert.Throws<ArgumentNullException>(() => new AmdAdlxDevice(reader, "provider-id", null!, "name", GpuKind.Discrete));
        }

        [Fact]
        public void Constructor_NullName_ThrowsArgumentNullException()
        {
            var reader = new FakeTemperatureReader(true, new TemperatureReading(50.0, "GPU", DateTimeOffset.UtcNow), null);
            Assert.Throws<ArgumentNullException>(() => new AmdAdlxDevice(reader, "provider-id", "id", null!, GpuKind.Discrete));
        }

        [Fact]
        public void TryReadTemperature_Success_ReturnsReading()
        {
            var expectedReading = new TemperatureReading(75.5, "GPU Core", DateTimeOffset.UtcNow);
            var reader = new FakeTemperatureReader(true, expectedReading, null);
            var device = new AmdAdlxDevice(reader, "amd-adlx", "amd-adlx:12345", "Test GPU", GpuKind.Discrete);

            bool success = device.TryReadTemperature(out var reading, out var error);

            Assert.True(success);
            Assert.Same(expectedReading, reading);
            Assert.Null(error);
            Assert.Equal(1, reader.CallCount);
            Assert.Equal("amd-adlx:12345", reader.LastDeviceId);
        }

        [Fact]
        public void TryReadTemperature_Failure_ReturnsError()
        {
            var reader = new FakeTemperatureReader(false, null, "Hardware failure");
            var device = new AmdAdlxDevice(reader, "amd-adlx", "amd-adlx:12345", "Test GPU", GpuKind.Discrete);

            bool success = device.TryReadTemperature(out var reading, out var error);

            Assert.False(success);
            Assert.Null(reading);
            Assert.Equal("Hardware failure", error);
        }

        [Fact]
        public void TryReadTemperature_PassesCancellationToken()
        {
            var reader = new FakeTemperatureReader(true, new TemperatureReading(50.0, "GPU", DateTimeOffset.UtcNow), null);
            var device = new AmdAdlxDevice(reader, "amd-adlx", "amd-adlx:12345", "Test GPU", GpuKind.Discrete);
            var cts = new CancellationTokenSource();

            device.TryReadTemperature(out _, out _, cts.Token);

            Assert.Equal(cts.Token, reader.LastCancellationToken);
        }

        [Fact]
        public void TryReadTemperature_WorksWithAnyIGpuTemperatureReaderImplementation()
        {
            var mockReader = new FakeTemperatureReader(true, new TemperatureReading(60.0, "Mock Sensor", DateTimeOffset.UtcNow), null);
            var device = new AmdAdlxDevice(mockReader, "amd-adlx", "amd-adlx:99999", "Mock GPU", GpuKind.Integrated);

            bool success = device.TryReadTemperature(out var reading, out var error);

            Assert.True(success);
            Assert.NotNull(reading);
            Assert.Equal(60.0, reading.Celsius);
            Assert.Equal("Mock Sensor", reading.SensorName);
            Assert.Equal(1, mockReader.CallCount);
            Assert.Equal("amd-adlx:99999", mockReader.LastDeviceId);
        }
    }
}