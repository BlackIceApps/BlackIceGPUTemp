using System;
using System.Threading;
using System.Threading.Tasks;
using BlackIceGPUTemp.Tray;
using Xunit;

namespace BlackIceGPUTemp.Tests
{
    public class MonitoringReadGateTests
    {
        [Fact]
        public void TryBeginRead_FirstCall_ReturnsTrue()
        {
            var gate = new MonitoringReadGate();

            bool result = gate.TryBeginRead();

            Assert.True(result);
        }

        [Fact]
        public void TryBeginRead_SecondCallDuringActiveRead_ReturnsFalse()
        {
            var gate = new MonitoringReadGate();

            bool first = gate.TryBeginRead();
            bool second = gate.TryBeginRead();

            Assert.True(first);
            Assert.False(second);
        }

        [Fact]
        public void TryBeginRead_AfterEndRead_ReturnsTrue()
        {
            var gate = new MonitoringReadGate();

            gate.TryBeginRead();
            gate.EndRead();
            bool result = gate.TryBeginRead();

            Assert.True(result);
        }

        [Fact]
        public void TryBeginRead_AfterStop_ReturnsFalse()
        {
            var gate = new MonitoringReadGate();

            gate.Stop();
            bool result = gate.TryBeginRead();

            Assert.False(result);
        }

        [Fact]
        public void Stop_DuringActiveRead_DoesNotThrow_AndBlocksSubsequentReads()
        {
            var gate = new MonitoringReadGate();

            gate.TryBeginRead();
            gate.Stop();
            gate.EndRead();

            bool result = gate.TryBeginRead();

            Assert.False(result);
        }

        [Fact]
        public void TryBeginRead_ConcurrentCalls_OnlyOneSucceeds()
        {
            var gate = new MonitoringReadGate();
            const int threadCount = 10;
            var barrier = new Barrier(threadCount);
            int successCount = 0;

            var tasks = new Task[threadCount];
            for (int i = 0; i < threadCount; i++)
            {
                tasks[i] = Task.Run(() =>
                {
                    barrier.SignalAndWait();
                    if (gate.TryBeginRead())
                    {
                        Interlocked.Increment(ref successCount);
                        gate.EndRead();
                    }
                });
            }

            Task.WaitAll(tasks);

            Assert.Equal(1, successCount);
        }

        [Fact]
        public void TryBeginRead_ConcurrentWithStop_StopWins()
        {
            var gate = new MonitoringReadGate();
            const int threadCount = 20;
            var startBarrier = new Barrier(threadCount);
            var stopBarrier = new Barrier(2);
            int successCount = 0;

            var readTasks = new Task[threadCount - 1];
            for (int i = 0; i < threadCount - 1; i++)
            {
                readTasks[i] = Task.Run(() =>
                {
                    startBarrier.SignalAndWait();
                    if (gate.TryBeginRead())
                    {
                        Interlocked.Increment(ref successCount);
                        gate.EndRead();
                    }
                });
            }

            var stopTask = Task.Run(() =>
            {
                startBarrier.SignalAndWait();
                gate.Stop();
            });

            Task.WaitAll(readTasks);
            stopTask.Wait();

            Assert.Equal(0, successCount);
            Assert.False(gate.TryBeginRead());
        }

        [Fact]
        public void MultipleCycles_ReadEndReadRepeat_WorksCorrectly()
        {
            var gate = new MonitoringReadGate();

            for (int i = 0; i < 5; i++)
            {
                Assert.True(gate.TryBeginRead());
                gate.EndRead();
            }
        }

        [Fact]
        public void EndRead_WithoutBeginRead_DoesNotThrow()
        {
            var gate = new MonitoringReadGate();

            gate.EndRead();

            Assert.True(gate.TryBeginRead());
        }
    }
}