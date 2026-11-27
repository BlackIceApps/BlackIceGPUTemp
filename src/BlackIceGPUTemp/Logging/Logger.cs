using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace BlackIceGPUTemp.Logging
{
    public enum LogLevel
    {
        Info,
        Warn,
        Error
    }

    internal static class Logger
    {
        private const int MaxDedupKeys = 1024;

        private static readonly string? LogFilePath;
        private static readonly object FileLock = new();
        private static readonly HashSet<string> WrittenWarningsAndErrors = new();
        private static readonly Queue<string> DedupKeyQueue = new();
        private static readonly object DedupLock = new();

        static Logger()
        {
            try
            {
                string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string logDirectory = Path.Combine(localAppData, "BlackIceGPUTemp");
                Directory.CreateDirectory(logDirectory);
                LogFilePath = Path.Combine(logDirectory, "BlackIceGPUTemp.log");
            }
            catch
            {
                LogFilePath = null;
            }
        }

        public static void LogInfo(string message)
        {
            WriteEntry(LogLevel.Info, message);
        }

        public static void LogWarn(string message)
        {
            WriteEntry(LogLevel.Warn, message);
        }

        public static void LogError(string message)
        {
            WriteEntry(LogLevel.Error, message);
        }

        public static void LogError(string message, Exception exception)
        {
            WriteEntry(LogLevel.Error, $"{message}: {exception}");
        }

        private static void WriteEntry(LogLevel level, string message)
        {
            if (string.IsNullOrEmpty(LogFilePath))
            {
                return;
            }

            bool shouldDeduplicate = level == LogLevel.Warn || level == LogLevel.Error;

            if (shouldDeduplicate)
            {
                string dedupKey = $"{level}:{message}";

                lock (DedupLock)
                {
                    if (WrittenWarningsAndErrors.Contains(dedupKey))
                    {
                        return;
                    }

                    try
                    {
                        string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                        string entry = $"{timestamp} [{level}] {message}{Environment.NewLine}";

                        lock (FileLock)
                        {
                            File.AppendAllText(LogFilePath, entry, Encoding.UTF8);
                        }

                        WrittenWarningsAndErrors.Add(dedupKey);
                        DedupKeyQueue.Enqueue(dedupKey);

                        if (WrittenWarningsAndErrors.Count > MaxDedupKeys)
                        {
                            string oldestKey = DedupKeyQueue.Dequeue();
                            WrittenWarningsAndErrors.Remove(oldestKey);
                        }
                    }
                    catch (Exception ex)
                    {
                        SafeTraceError($"Logger write failed: {ex}");
                    }
                }
            }
            else
            {
                try
                {
                    string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                    string entry = $"{timestamp} [{level}] {message}{Environment.NewLine}";

                    lock (FileLock)
                    {
                        File.AppendAllText(LogFilePath, entry, Encoding.UTF8);
                    }
                }
                catch (Exception ex)
                {
                    SafeTraceError($"Logger write failed: {ex}");
                }
            }
        }

        private static void SafeTraceError(string message)
        {
            try
            {
                Trace.TraceError(message);
            }
            catch
            {
            }
        }
    }
}