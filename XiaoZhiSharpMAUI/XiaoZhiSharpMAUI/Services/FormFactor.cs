using XiaoZhiSharpMAUI.Shared.Services;

namespace XiaoZhiSharpMAUI.Services
{
    public class FormFactor : IFormFactor
    {
        public string GetFormFactor()
        {
            var idiom = DeviceInfo.Idiom;
            
            if (idiom == DeviceIdiom.Phone)
                return "Phone";
            else if (idiom == DeviceIdiom.Tablet)
                return "Tablet";
            else if (idiom == DeviceIdiom.Desktop)
                return "Desktop";
            else if (idiom == DeviceIdiom.TV)
                return "TV";
            else if (idiom == DeviceIdiom.Watch)
                return "Watch";
            else
                return "Unknown";
        }

        public string GetPlatform()
        {
            return DeviceInfo.Platform.ToString();
        }

        public string GetVersion()
        {
            return DeviceInfo.VersionString;
        }

        public bool IsPhysicalDevice()
        {
            return DeviceInfo.DeviceType == DeviceType.Physical;
        }
    }
}
