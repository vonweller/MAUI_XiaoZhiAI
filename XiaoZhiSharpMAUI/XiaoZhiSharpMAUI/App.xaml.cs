namespace XiaoZhiSharpMAUI
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            var mainPage = Handler?.MauiContext?.Services.GetService<MainPage>();
            if (mainPage == null)
            {
                // Fallback: create MainPage manually if DI fails
                var xiaoZhiAgent = Handler?.MauiContext?.Services.GetService<XiaoZhiSharp.XiaoZhiAgent>();
                var logger = Handler?.MauiContext?.Services.GetService<Microsoft.Extensions.Logging.ILogger<MainPage>>();
                var mauiAudioService = Handler?.MauiContext?.Services.GetService<XiaoZhiSharpMAUI.Services.IMauiAudioService>();
                
                if (xiaoZhiAgent != null && logger != null)
                {
                    mainPage = new MainPage(xiaoZhiAgent, logger, mauiAudioService);
                }
                else
                {
                    // Ultimate fallback
                    mainPage = new MainPage(
                        new XiaoZhiSharp.XiaoZhiAgent("https://api.tenclass.net/xiaozhi/ota/", "wss://api.tenclass.net/xiaozhi/v1/"),
                        Microsoft.Extensions.Logging.Abstractions.NullLogger<MainPage>.Instance,
                        null // IMauiAudioService can be null
                    );
                }
            }

            return new Window(mainPage) { Title = "小智AI助手" };
        }
    }
}
