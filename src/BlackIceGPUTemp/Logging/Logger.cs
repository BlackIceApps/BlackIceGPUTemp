using System;
using System.IO;
using System.Text;
using System.Threading;

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
        private static readonly string LogFilePath;
        private static readonly object FileLock = new();

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
                LogFilePath = null!;
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

            try
            {
                string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                string entry = $"{timestamp} [{level}] {message}{Environment.NewLine}";

                lock (FileLock)
                {
                    File.AppendAllText(LogFilePath, entry, Encoding.UTF8);
                }
            }
            catch
            {
            }
        }
    }
}