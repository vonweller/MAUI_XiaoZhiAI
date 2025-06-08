namespace XiaoZhiAI_MAUI.Services
{
    public interface IBackgroundService
    {
        /// <summary>
        /// 启动后台服务
        /// </summary>
        Task StartAsync();

        /// <summary>
        /// 停止后台服务
        /// </summary>
        Task StopAsync();

        /// <summary>
        /// 检查后台服务是否正在运行
        /// </summary>
        bool IsRunning { get; }

        /// <summary>
        /// 后台服务状态变化事件
        /// </summary>
        event EventHandler<bool> StatusChanged;
    }
} 