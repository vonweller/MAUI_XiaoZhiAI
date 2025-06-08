using Microsoft.Extensions.Logging;
using XiaoZhiAI_MAUI.Services;
using CommunityToolkit.Maui;

namespace XiaoZhiAI_MAUI
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

#if DEBUG
    		builder.Logging.AddDebug();
#endif
            builder.Services.AddSingleton<IWebSocketService, WebSocketService>();
            builder.Services.AddSingleton<IBackgroundService, Services.BackgroundService>();
            builder.Services.AddSingleton<IAudioService, AudioService>();
            builder.Services.AddSingleton<Pages.SettingsPage>();
            builder.Services.AddSingleton<Pages.ChatPage>();
            builder.Services.AddSingleton<Pages.AudioTestPage>();

#if ANDROID
            builder.Services.AddSingleton<IPlatformAudioService, Platforms.Android.AndroidAudioService>();
#elif WINDOWS
            builder.Services.AddSingleton<IPlatformAudioService, Platforms.Windows.WindowsAudioService>();
#elif IOS
            builder.Services.AddSingleton<IPlatformAudioService, Platforms.iOS.iOSAudioService>();
#endif

            return builder.Build();
        }
    }
}
