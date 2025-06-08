using System;

namespace XiaoZhiAI_MAUI.Services
{
    public interface ILogService
    {
        event EventHandler<string> LogMessageReceived;
        void LogMessage(string message);
        void LogDebug(string message);
        void LogError(string message);
        void LogInfo(string message);
    }
} 