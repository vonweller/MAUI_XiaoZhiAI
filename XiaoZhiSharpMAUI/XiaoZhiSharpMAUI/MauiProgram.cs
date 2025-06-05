using Microsoft.Extensions.Logging;
using XiaoZhiSharpMAUI.Services;
using XiaoZhiSharpMAUI.Shared.Services;
using XiaoZhiSharp;
using CommunityToolkit.Maui;
using Plugin.Maui.Audio;

namespace XiaoZhiSharpMAUI
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .UseMauiCommunityToolkit()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });

            // Add device-specific services used by the XiaoZhiSharpMAUI.Shared project
            builder.Services.AddSingleton<IFormFactor, FormFactor>();

            // Add MAUI Audio service - 使用正确的配置方式
            builder.Services.AddSingleton(AudioManager.Current);
            builder.Services.AddSingleton<IMauiAudioService, MauiAudioService>();

            // Add XiaoZhiSharp services
            builder.Services.AddSingleton<XiaoZhiAgent>(serviceProvider =>
            {
                var otaUrl = "https://api.tenclass.net/xiaozhi/ota/";
                var wsUrl = "wss://api.tenclass.net/xiaozhi/v1/";
                return new XiaoZhiAgent(otaUrl, wsUrl);
            });

            // Register MainPage for dependency injection
            builder.Services.AddTransient<MainPage>();

            // Add MAUI Blazor WebView
            builder.Services.AddMauiBlazorWebView();

            // Configure logging
#if DEBUG
            builder.Services.AddBlazorWebViewDeveloperTools();
            builder.Logging.AddDebug();
            builder.Logging.SetMinimumLevel(LogLevel.Debug);
#else
            builder.Logging.SetMinimumLevel(LogLevel.Information);
#endif

            return builder.Build();
        }
    }
}
