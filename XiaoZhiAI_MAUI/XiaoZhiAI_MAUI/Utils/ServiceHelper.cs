namespace XiaoZhiAI_MAUI.Utils
{
    public static class ServiceHelper
    {
        public static TService GetService<TService>()
        {
            if (Application.Current?.Handler?.MauiContext?.Services is not null)
            {
                return (TService)Application.Current.Handler.MauiContext.Services.GetService(typeof(TService));
            }
            
            return default(TService);
        }
    }
} 