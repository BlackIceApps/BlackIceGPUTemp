using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using BlackIceGPUTemp.Monitoring.Providers.Amd.Native;
using Xunit;

namespace BlackIceGPUTemp.Tests
{
    public class AdlxNativeTests
    {
        private static AdlxNative CreateTestNative(bool isInitialized, AdlxNative.ADLXTerminateDelegate? terminate = null)
        {
            return new AdlxNative(new IntPtr(1), _ => true, isInitialized, terminate);
        }

        [Fact]
        public void Release_AfterTerminate_ThrowsInvalidOperationException()
        {
            bool terminated = false;
            var native = CreateTestNative(true, () =>
            {
                terminated = true;
                return 0;
            });
            
            native.Terminate();
            Assert.True(terminated);
            
            var ex = Assert.Throws<InvalidOperationException>(() => native.Release(new IntPtr(0x1234)));
            Assert.Contains("not initialized", ex.Message);
        }

        [Fact]
        public void Release_WhenNotInitialized_ThrowsInvalidOperationException()
        {
            var native = CreateTestNative(false);
            
            var ex = Assert.Throws<InvalidOperationException>(() => native.Release(new IntPtr(0x1234)));
            Assert.Contains("not initialized", ex.Message);
        }

        [Fact]
        public void Release_IntPtrZero_ReturnsZero_WhenInitialized()
        {
            var native = CreateTestNative(true);
            
            int result = native.Release(IntPtr.Zero);
            Assert.Equal(0, result);
        }

        [Fact]
        public void Dispose_WhenFreeLibraryThrows_DoesNotMaskOriginalException()
        {
            bool freeLibraryCalled = false;
            
            Func<IntPtr, bool> failingFreeLibrary = _ =>
            {
                freeLibraryCalled = true;
                throw new InvalidOperationException("FreeLibrary failed");
            };
            
            var native = new AdlxNative(new IntPtr(1), failingFreeLibrary, false);
            
            // The FreeLibrary exception should propagate even if Trace logging fails
            var ex = Assert.Throws<InvalidOperationException>(() => native.Dispose());
            Assert.Equal("FreeLibrary failed", ex.Message);
            Assert.True(freeLibraryCalled);
        }

        [Fact]
        public void Dispose_WhenFreeLibraryReturnsFalse_ThrowsWin32Exception()
        {
            var native = new AdlxNative(new IntPtr(1), _ => false, false);
            
            var ex = Assert.Throws<System.ComponentModel.Win32Exception>(() => native.Dispose());
            Assert.Contains("Failed to free ADLX library", ex.Message);
        }

        [Fact]
        public void Dispose_WhenFreeLibrarySucceeds_SetsLibHandleToZero()
        {
            var native = new AdlxNative(new IntPtr(1), _ => true, false);
            
            native.Dispose();
            
            Assert.Equal(IntPtr.Zero, native.LibHandle);
        }

        [Fact]
        public void Dispose_WhenDisposedTwice_DoesNotThrow()
        {
            var native = new AdlxNative(new IntPtr(1), _ => true, false);
            
            native.Dispose();
            native.Dispose(); // Should not throw
        }

        [Fact]
        public void Dispose_WhenTerminateNotCalled_ThrowsInvalidOperationException()
        {
            var native = new AdlxNative(IntPtr.Zero, _ => true, true);
            
            var ex = Assert.Throws<InvalidOperationException>(() => native.Dispose());
            Assert.Contains("still initialized", ex.Message);
        }

        [Fact]
        public void Initialize_WhenNotLoaded_ThrowsInvalidOperationException()
        {
            var native = CreateTestNative(false);
            // _initialize is null, so this will throw "not loaded"
            
            var ex = Assert.Throws<InvalidOperationException>(() => native.Initialize(out _));
            Assert.Contains("not loaded", ex.Message);
        }

        [Fact]
        public void Terminate_WhenNotInitialized_ThrowsInvalidOperationException()
        {
            var native = CreateTestNative(false, () => 0);
            
            var ex = Assert.Throws<InvalidOperationException>(() => native.Terminate());
            Assert.Contains("not initialized", ex.Message);
        }
    }
}