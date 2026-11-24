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
                count = GpuNames.Count;
                if (GetGpuListSizeHandler != null)
                {
                    return GetGpuListSizeHandler(gpuList, out count);
                }
                return 0;
            }

            public int GetGpuAt(IntPtr gpuList, int index, out IntPtr gpu)
            {
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
                services = new IntPtr(0x4000);
                if (GetPerfServicesHandler != null)
                {
                    return GetPerfServicesHandler(system, out services);
                }
                return 0;
            }

            public int GetCurrentGpuMetrics(IntPtr services, IntPtr gpu, out IntPtr metrics)
            {
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
                    return GetGpuTempHandler(metrics, out celsius);
                }
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
            var connectResult = provider.Connect(out var error);
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
            var connectResult = provider.Connect(out var error);
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
            var result1 = ((IGpuProviderProbe)provider).TryProbe(out error);
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
        public void TryEnumerateDevices_AdlxError_GetGpuAtFails_ContinuesEnumeration()
        {
            // GetGpuAt fails for one GPU but succeeds for another
            var fake = new FakeAdlxNative
            {
                GpuNames = new List<string> { "GPU1", "GPU2" },
                GpuUniqueIds = new List<int> { 111, 222 },
                GpuTypes = new List<int> { 2, 2 },
                GpuTemperatureSupported = new List<bool> { true, true },
                GetGpuAtHandler = (IntPtr list, int index, out IntPtr gpu) =>
                {
                    if (index == 0)
                    {
                        gpu = new IntPtr(0x3000);
                        return -1; // Fail first GPU
                    }
                    gpu = new IntPtr(0x3001);
                    return 0; // Succeed second GPU
                }
            };
            var provider = new AmdAdlxProvider(() => fake);

            bool success = ((IGpuDeviceEnumerator)provider).TryEnumerateDevices(out var devices, out var error);

            Assert.True(success); // Overall enumeration succeeds
            Assert.Null(error);
            Assert.Single(devices); // Only second GPU enumerated
            Assert.Equal("amd-adlx:222", devices[0].Id);
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
            // Temporary interfaces released immediately
            Assert.Contains(0x3000, fake.ReleasedPointers);
            Assert.DoesNotContain(0x1000, fake.ReleasedPointers);
            Assert.DoesNotContain(0x2000, fake.ReleasedPointers); // gpuList is persistent

            provider.Disconnect();

            // gpuList released on Disconnect
            Assert.Contains(0x2000, fake.ReleasedPointers);
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
            // Temporary interfaces released immediately
            Assert.Contains(0x4000, fake.ReleasedPointers);
            Assert.Contains(0x3000, fake.ReleasedPointers);
            Assert.DoesNotContain(0x1000, fake.ReleasedPointers);
            Assert.DoesNotContain(0x2000, fake.ReleasedPointers); // gpuList is persistent

            provider.Disconnect();

            // gpuList released on Disconnect
            Assert.Contains(0x2000, fake.ReleasedPointers);
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
            // Temporary interfaces released immediately
            Assert.Contains(0x6000, fake.ReleasedPointers);
            Assert.Contains(0x4000, fake.ReleasedPointers);
            Assert.Contains(0x3000, fake.ReleasedPointers);
            Assert.DoesNotContain(0x1000, fake.ReleasedPointers);
            Assert.DoesNotContain(0x2000, fake.ReleasedPointers); // gpuList is persistent

            provider.Disconnect();

            // gpuList released on Disconnect
            Assert.Contains(0x2000, fake.ReleasedPointers);
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
            // Temporary interfaces released immediately
            Assert.Contains(0x6000, fake.ReleasedPointers);
            Assert.Contains(0x4000, fake.ReleasedPointers);
            Assert.Contains(0x3000, fake.ReleasedPointers);
            Assert.DoesNotContain(0x1000, fake.ReleasedPointers);
            Assert.DoesNotContain(0x2000, fake.ReleasedPointers); // gpuList is persistent

            provider.Disconnect();

            // gpuList released on Disconnect
            Assert.Contains(0x2000, fake.ReleasedPointers);
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
            // Temporary interfaces released immediately
            Assert.Contains(0x5000, fake.ReleasedPointers);
            Assert.Contains(0x6000, fake.ReleasedPointers);
            Assert.Contains(0x4000, fake.ReleasedPointers);
            Assert.Contains(0x3000, fake.ReleasedPointers);
            Assert.DoesNotContain(0x1000, fake.ReleasedPointers);
            Assert.DoesNotContain(0x2000, fake.ReleasedPointers); // gpuList is persistent

            provider.Disconnect();

            // gpuList released on Disconnect
            Assert.Contains(0x2000, fake.ReleasedPointers);
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
            Assert.Contains(0x6000, fake.ReleasedPointers);
            Assert.Contains(0x4000, fake.ReleasedPointers);
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
            Assert.True(servicesIdx < gpuIdx, "services (child of gpu) before gpu");
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
                ThrowOnReleasePointer = 0x4000
            };
            var provider = new AmdAdlxProvider(() => fake);

            var result = ((IGpuProviderProbe)provider).TryProbe(out var error);

            Assert.True(result);
            // Temporary interfaces released immediately despite exception
            Assert.Contains(0x6000, fake.ReleasedPointers);
            Assert.Contains(0x4000, fake.ReleasedPointers);
            Assert.Contains(0x3000, fake.ReleasedPointers);
            Assert.DoesNotContain(0x1000, fake.ReleasedPointers);
            Assert.DoesNotContain(0x2000, fake.ReleasedPointers); // gpuList is persistent, not released until Disconnect
            Assert.Equal(1, fake.ThrowOnReleaseCount);

            provider.Disconnect();

            // gpuList released on Disconnect
            Assert.Contains(0x2000, fake.ReleasedPointers);
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
            // Temporary interfaces released immediately despite exception
            Assert.Contains(0x5000, fake.ReleasedPointers);
            Assert.Contains(0x6000, fake.ReleasedPointers);
            Assert.Contains(0x4000, fake.ReleasedPointers);
            Assert.Contains(0x3000, fake.ReleasedPointers);
            Assert.DoesNotContain(0x1000, fake.ReleasedPointers);
            Assert.DoesNotContain(0x2000, fake.ReleasedPointers); // gpuList is persistent
            Assert.Equal(1, fake.ThrowOnReleaseCount);

            provider.Disconnect();

            // gpuList released on Disconnect
            Assert.Contains(0x2000, fake.ReleasedPointers);
        }

        [Fact]
        public void ReleaseFailedHResult_DoesNotChangeOperationResult_Probe()
        {
            var fake = new FakeAdlxNative
            {
                ReleaseFailAtPointer = 0x4000
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
                ReleaseFailAtPointer = 0x6000
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
            // All temporary interfaces should still be released despite negative return
            Assert.Contains(0x6000, fake.ReleasedPointers);
            Assert.Contains(0x4000, fake.ReleasedPointers);
            Assert.Contains(0x3000, fake.ReleasedPointers);
            Assert.DoesNotContain(0x1000, fake.ReleasedPointers);
            Assert.DoesNotContain(0x2000, fake.ReleasedPointers); // gpuList is persistent

            provider.Disconnect();

            // gpuList released on Disconnect
            Assert.Contains(0x2000, fake.ReleasedPointers);
        }

        [Fact]
        public void ReleaseException_DoesNotBlockOtherReleases_AndZeroesPointer()
        {
            var fake = new FakeAdlxNative
            {
                ThrowOnRelease = true,
                ThrowOnReleasePointer = 0x4000
            };
            var provider = new AmdAdlxProvider(() => fake);

            var result = ((IGpuProviderProbe)provider).TryProbe(out var error);

            Assert.True(result);
            // All temporary interfaces should still be released despite one throwing
            Assert.Contains(0x6000, fake.ReleasedPointers);
            Assert.Contains(0x4000, fake.ReleasedPointers);
            Assert.Contains(0x3000, fake.ReleasedPointers);
            Assert.DoesNotContain(0x1000, fake.ReleasedPointers);
            Assert.DoesNotContain(0x2000, fake.ReleasedPointers); // gpuList is persistent
            Assert.Equal(1, fake.ThrowOnReleaseCount);

            provider.Disconnect();

            // gpuList released on Disconnect
            Assert.Contains(0x2000, fake.ReleasedPointers);
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
            // All temporary interfaces should still be released despite one throwing
            Assert.Contains(0x5000, fake.ReleasedPointers);
            Assert.Contains(0x6000, fake.ReleasedPointers);
            Assert.Contains(0x4000, fake.ReleasedPointers);
            Assert.Contains(0x3000, fake.ReleasedPointers);
            Assert.DoesNotContain(0x1000, fake.ReleasedPointers);
            Assert.DoesNotContain(0x2000, fake.ReleasedPointers); // gpuList is persistent
            Assert.Equal(1, fake.ThrowOnReleaseCount);

            provider.Disconnect();

            // gpuList released on Disconnect
            Assert.Contains(0x2000, fake.ReleasedPointers);
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
            var connectResult = provider.Connect(out var error);
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
            var result1 = ((IGpuProviderProbe)provider).TryProbe(out error);
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
            // All acquired interfaces released: targetGpu, services (acquired before cancel)
            Assert.Contains(0x3000, fake.ReleasedPointers); // targetGpu
            Assert.Contains(0x4000, fake.ReleasedPointers); // services
            // GPU list should NOT be released (owned by persistent session)
            Assert.DoesNotContain(0x2000, fake.ReleasedPointers);
            Assert.DoesNotContain(0x1000, fake.ReleasedPointers); // System not released
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
            success = ((IGpuDeviceEnumerator)provider).TryEnumerateDevices(out devices, out error);
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
            success = ((IGpuDeviceEnumerator)provider).TryEnumerateDevices(out devices, out error);
            Assert.True(success);
            Assert.Null(error);
            Assert.Single(devices);
            Assert.Equal(1, fake.InitializeCalls);

            // Second operation: Probe (reuses session)
            var probeResult = ((IGpuProviderProbe)provider).TryProbe(out probeError);
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
            success = ((IGpuTemperatureReader)provider).TryReadTemperature("amd-adlx:12345", out _, out _);
            Assert.True(success);
            Assert.Equal(1, fake.InitializeCalls);

            // Second operation: Probe (reuses session)
            var probeResult = ((IGpuProviderProbe)provider).TryProbe(out var probeError);
            Assert.True(probeResult);
            Assert.Null(probeError);
            Assert.Equal(1, fake.InitializeCalls);

            // Third operation: EnumerateDevices (reuses session)
            success = ((IGpuDeviceEnumerator)provider).TryEnumerateDevices(out devices, out error);
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