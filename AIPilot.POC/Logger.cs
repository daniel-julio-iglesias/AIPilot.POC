// [UPDATED: Nullable enabled] _filePath nullable, guard usage
using System;
using System.IO;
using System.Text;

namespace AIPilot.POC
{
    public static class Logger
    {
        private static readonly object _lock = new object();
        private static string? _filePath;
        private static bool _initialized;

        public static void Init()
        {
            if (_initialized) return;
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var logDir = Path.Combine(baseDir, "logs");
            Directory.CreateDirectory(logDir);
            var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            _filePath = Path.Combine(logDir, $"ai_pilot_poc_{stamp}.log");
            _initialized = true;
            Info("Logger initialized");
        }

        public static void Info(string msg) => Write("INFO", msg);
        public static void Warn(string msg) => Write("WARN", msg);
        public static void Error(string msg) => Write("ERROR", msg);

        private static void Write(string level, string msg)
        {
            lock (_lock)
            {
                if (!_initialized) Init();
                if (string.IsNullOrEmpty(_filePath)) return; // extremely defensive
                var line = $"{DateTime.UtcNow:O} [{level}] {msg}{Environment.NewLine}";
                File.AppendAllText(_filePath, line, Encoding.UTF8);
            }
        }
    }
}
