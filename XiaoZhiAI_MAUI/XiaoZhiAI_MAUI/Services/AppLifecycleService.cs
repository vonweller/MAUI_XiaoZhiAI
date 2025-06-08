namespace XiaoZhiAI_MAUI.Services
{
    public class AppLifecycleService
    {
        private readonly IBackgroundService _backgroundService;
        private readonly IWebSocketService _webSocketService;

        public AppLifecycleService(IBackgroundService backgroundService, IWebSocketService webSocketService)
        {
            _backgroundService = backgroundService;
            _webSocketService = webSocketService;
        }

        public async Task OnAppSleep()
        {
            System.Diagnostics.Debug.WriteLine("应用进入后台");
            
            // 确保后台服务正在运行
            if (!_backgroundService.IsRunning)
            {
                await _backgroundService.StartAsync();
            }
        }

        public async Task OnAppResume()
        {
            System.Diagnostics.Debug.WriteLine("应用恢复前台");
            
            // 检查WebSocket连接状态
            if (_webSocketService.Status != WebSocketStatus.Connected)
            {
                try
                {
                    await _webSocketService.ConnectAsync(CancellationToken.None);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"恢复WebSocket连接失败: {ex.Message}");
                }
            }
        }

        public async Task OnAppStopping()
        {
            System.Diagnostics.Debug.WriteLine("应用正在退出");
            
            // 可选：停止后台服务（如果用户选择完全退出）
            // await _backgroundService.StopAsync();
        }
    }
} 