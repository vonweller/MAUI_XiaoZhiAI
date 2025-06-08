using System;
using System.Diagnostics;

namespace XiaoZhiAI_MAUI.Services
{
    public class LogService : ILogService
    {
        public event EventHandler<string> LogMessageReceived;

        public void LogMessage(string message)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            var logEntry = $"[{timestamp}] {message}";
            
            Debug.WriteLine(logEntry);
            LogMessageReceived?.Invoke(this, logEntry);
        }

        public void LogDebug(string message)
        {
            LogMessage($"🔧 {message}");
        }

        public void LogError(string message)
        {
            LogMessage($"❌ {message}");
        }

        public void LogInfo(string message)
        {
            LogMessage($"ℹ️ {message}");
        }
    }
} 