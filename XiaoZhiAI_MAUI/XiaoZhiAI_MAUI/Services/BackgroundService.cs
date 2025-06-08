using System.Timers;

namespace XiaoZhiAI_MAUI.Services
{
    public class BackgroundService : IBackgroundService
    {
        private readonly IWebSocketService _webSocketService;
        private System.Timers.Timer _keepAliveTimer;
        private bool _isRunning;
        private CancellationTokenSource _cancellationTokenSource;

        public bool IsRunning => _isRunning;
        public event EventHandler<bool> StatusChanged;

        public BackgroundService()
        {
            _webSocketService = IPlatformApplication.Current.Services.GetService<IWebSocketService>();
        }

        public async Task StartAsync()
        {
            if (_isRunning) return;

            try
            {
                _isRunning = true;
                _cancellationTokenSource = new CancellationTokenSource();
                
                // 启动前台服务（平台特定）
                await StartPlatformServiceAsync();
                
                // 启动心跳检测
                StartKeepAliveTimer();
                
                // 确保WebSocket连接
                if (_webSocketService.Status != WebSocketStatus.Connected)
                {
                    await _webSocketService.ConnectAsync(_cancellationTokenSource.Token);
                }
                
                StatusChanged?.Invoke(this, true);
                
                System.Diagnostics.Debug.WriteLine("后台服务已启动");
            }
            catch (Exception ex)
            {
                _isRunning = false;
                System.Diagnostics.Debug.WriteLine($"启动后台服务失败: {ex.Message}");
                throw;
            }
        }

        public async Task StopAsync()
        {
            if (!_isRunning) return;

            try
            {
                _isRunning = false;
                
                // 停止心跳检测
                _keepAliveTimer?.Stop();
                _keepAliveTimer?.Dispose();
                _keepAliveTimer = null;
                
                // 取消操作
                _cancellationTokenSource?.Cancel();
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = null;
                
                // 停止平台特定服务
                await StopPlatformServiceAsync();
                
                StatusChanged?.Invoke(this, false);
                
                System.Diagnostics.Debug.WriteLine("后台服务已停止");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"停止后台服务失败: {ex.Message}");
            }
        }

        private void StartKeepAliveTimer()
        {
            _keepAliveTimer = new System.Timers.Timer(30000); // 30秒检查一次
            _keepAliveTimer.Elapsed += OnKeepAliveTimer;
            _keepAliveTimer.AutoReset = true;
            _keepAliveTimer.Start();
        }

        private async void OnKeepAliveTimer(object sender, ElapsedEventArgs e)
        {
            if (!_isRunning || _cancellationTokenSource?.Token.IsCancellationRequested == true)
                return;

            try
            {
                // 检查WebSocket连接状态
                if (_webSocketService.Status != WebSocketStatus.Connected)
                {
                    System.Diagnostics.Debug.WriteLine("WebSocket连接断开，尝试重连...");
                    await _webSocketService.ConnectAsync(_cancellationTokenSource.Token);
                }
                
                // 发送心跳包保持连接
                await SendHeartbeatAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"后台心跳检查异常: {ex.Message}");
            }
        }

        private async Task SendHeartbeatAsync()
        {
            try
            {
                var heartbeat = new
                {
                    type = "heartbeat",
                    timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                };
                
                var json = System.Text.Json.JsonSerializer.Serialize(heartbeat);
                await _webSocketService.SendTextAsync(json, _cancellationTokenSource?.Token ?? CancellationToken.None);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"发送心跳包失败: {ex.Message}");
            }
        }

        private async Task StartPlatformServiceAsync()
        {
#if ANDROID
            await StartAndroidForegroundServiceAsync();
#elif WINDOWS
            await StartWindowsServiceAsync();
#endif
        }

        private async Task StopPlatformServiceAsync()
        {
#if ANDROID
            await StopAndroidForegroundServiceAsync();
#elif WINDOWS
            await StopWindowsServiceAsync();
#endif
        }

#if ANDROID
        private async Task StartAndroidForegroundServiceAsync()
        {
            // Android前台服务将在平台特定代码中实现
            var activity = Platform.CurrentActivity ?? Android.App.Application.Context;
            var intent = new Android.Content.Intent(activity, typeof(Platforms.Android.XiaoZhiAIForegroundService));
            intent.SetAction("START_FOREGROUND_SERVICE");
            
            if (Android.OS.Build.VERSION.SdkInt >= Android.OS.BuildVersionCodes.O)
            {
                activity.StartForegroundService(intent);
            }
            else
            {
                activity.StartService(intent);
            }
            
            await Task.CompletedTask;
        }

        private async Task StopAndroidForegroundServiceAsync()
        {
            var activity = Platform.CurrentActivity ?? Android.App.Application.Context;
            var intent = new Android.Content.Intent(activity, typeof(Platforms.Android.XiaoZhiAIForegroundService));
            intent.SetAction("STOP_FOREGROUND_SERVICE");
            activity.StopService(intent);
            
            await Task.CompletedTask;
        }
#endif

#if WINDOWS
        private async Task StartWindowsServiceAsync()
        {
            // Windows平台保持窗口在后台运行
            System.Diagnostics.Debug.WriteLine("Windows后台服务已启动");
            await Task.CompletedTask;
        }

        private async Task StopWindowsServiceAsync()
        {
            System.Diagnostics.Debug.WriteLine("Windows后台服务已停止");
            await Task.CompletedTask;
        }
#endif
    }
} 