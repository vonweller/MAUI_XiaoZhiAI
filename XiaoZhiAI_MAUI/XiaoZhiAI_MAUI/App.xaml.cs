using XiaoZhiAI_MAUI.Services;

namespace XiaoZhiAI_MAUI
{
    public partial class App : Application
    {
        private AppLifecycleService _lifecycleService;

        public App()
        {
            InitializeComponent();
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            var window = new Window(new AppShell());
            
            // 初始化生命周期服务
            InitializeLifecycleService();
            
            return window;
        }

        private void InitializeLifecycleService()
        {
            try
            {
                var backgroundService = IPlatformApplication.Current.Services.GetService<IBackgroundService>();
                var webSocketService = IPlatformApplication.Current.Services.GetService<IWebSocketService>();
                
                if (backgroundService != null && webSocketService != null)
                {
                    _lifecycleService = new AppLifecycleService(backgroundService, webSocketService);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"初始化生命周期服务失败: {ex.Message}");
            }
        }

        protected override void OnSleep()
        {
            base.OnSleep();
            _lifecycleService?.OnAppSleep();
        }

        protected override void OnResume()
        {
            base.OnResume();
            _lifecycleService?.OnAppResume();
        }

        protected override void OnStart()
        {
            base.OnStart();
            System.Diagnostics.Debug.WriteLine("应用启动");
        }
    }
}