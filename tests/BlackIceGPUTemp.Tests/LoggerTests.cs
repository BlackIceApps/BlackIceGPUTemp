using System;
using System.IO;
using System.Reflection;
using Xunit;

namespace BlackIceGPUTemp.Tests
{
    public class LoggerTests
    {
        private static string? GetLogFilePath()
        {
            var field = typeof(Logger).GetField("LogFilePath", BindingFlags.NonPublic | BindingFlags.Static);
            return field?.GetValue(null) as string;
        }

        private static void ClearDedupState()
        {
            var writtenSet = typeof(Logger).GetField("WrittenWarningsAndErrors", BindingFlags.NonPublic | BindingFlags.Static);
            var keyQueue = typeof(Logger).GetField("DedupKeyQueue", BindingFlags.NonPublic | BindingFlags.Static);

            writtenSet?.SetValue(null, new System.Collections.Generic.HashSet<string>());
            keyQueue?.SetValue(null, new System.Collections.Generic.Queue<string>());
        }

        [Fact]
        public void UninitializedLogPath_DoesNotThrow()
        {
            var originalPath = GetLogFilePath();

            try
            {
                var field = typeof(Logger).GetField("LogFilePath", BindingFlags.NonPublic | BindingFlags.Static);
                field?.SetValue(null, (string?)null);

                Logger.LogInfo("Test info");
                Logger.LogWarn("Test warn");
                Logger.LogError("Test error");
                Logger.LogError("Test error with exception", new Exception("test"));
            }
            finally
            {
                var field = typeof(Logger).GetField("LogFilePath", BindingFlags.NonPublic | BindingFlags.Static);
                field?.SetValue(null, originalPath);
            }
        }

        [Fact]
        public void WriteErrors_DoNotEscape()
        {
            var originalPath = GetLogFilePath();

            try
            {
                var field = typeof(Logger).GetField("LogFilePath", BindingFlags.NonPublic | BindingFlags.Static);
                field?.SetValue(null, @"C:\invalid\path\that\cannot\be\written.log");

                Logger.LogInfo("Test info");
                Logger.LogWarn("Test warn");
                Logger.LogError("Test error");
                Logger.LogError("Test error with exception", new Exception("test"));
            }
            finally
            {
                var field = typeof(Logger).GetField("LogFilePath", BindingFlags.NonPublic | BindingFlags.Static);
                field?.SetValue(null, originalPath);
            }
        }

        [Fact]
        public void Deduplication_WorksForWarningsAndErrors()
        {
            ClearDedupState();

            Logger.LogWarn("Duplicate warning");
            Logger.LogWarn("Duplicate warning");
            Logger.LogError("Duplicate error");
            Logger.LogError("Duplicate error");

            var writtenSet = typeof(Logger).GetField("WrittenWarningsAndErrors", BindingFlags.NonPublic | BindingFlags.Static);
            var set = writtenSet?.GetValue(null) as System.Collections.Generic.HashSet<string>;

            Assert.NotNull(set);
            Assert.Equal(2, set.Count);
            Assert.Contains("Warn:Duplicate warning", set);
            Assert.Contains("Error:Duplicate error", set);
        }

        [Fact]
        public void Deduplication_DoesNotApplyToInfo()
        {
            ClearDedupState();

            Logger.LogInfo("Info message");
            Logger.LogInfo("Info message");

            var writtenSet = typeof(Logger).GetField("WrittenWarningsAndErrors", BindingFlags.NonPublic | BindingFlags.Static);
            var set = writtenSet?.GetValue(null) as System.Collections.Generic.HashSet<string>;

            Assert.NotNull(set);
            Assert.Equal(0, set.Count);
        }

        [Fact]
        public void DedupKeyLimit_IsEnforced()
        {
            ClearDedupState();

            const int maxKeys = 1024;

            for (int i = 0; i < maxKeys + 100; i++)
            {
                Logger.LogWarn($"Warning {i}");
            }

            var writtenSet = typeof(Logger).GetField("WrittenWarningsAndErrors", BindingFlags.NonPublic | BindingFlags.Static);
            var keyQueue = typeof(Logger).GetField("DedupKeyQueue", BindingFlags.NonPublic | BindingFlags.Static);

            var set = writtenSet?.GetValue(null) as System.Collections.Generic.HashSet<string>;
            var queue = keyQueue?.GetValue(null) as System.Collections.Generic.Queue<string>;

            Assert.NotNull(set);
            Assert.NotNull(queue);
            Assert.True(set.Count <= maxKeys);
            Assert.True(queue.Count <= maxKeys);
        }

        [Fact]
        public void OldestKey_Removed_AllowsRewrite()
        {
            ClearDedupState();

            const int maxKeys = 1024;

            for (int i = 0; i < maxKeys; i++)
            {
                Logger.LogWarn($"Warning {i}");
            }

            Logger.LogWarn("Oldest warning");
            Logger.LogWarn("Oldest warning");

            var writtenSet = typeof(Logger).GetField("WrittenWarningsAndErrors", BindingFlags.NonPublic | BindingFlags.Static);
            var set = writtenSet?.GetValue(null) as System.Collections.Generic.HashSet<string>;

            Assert.NotNull(set);
            Assert.DoesNotContain("Warn:Warning 0", set);
            Assert.Contains("Warn:Oldest warning", set);
        }
    }
}