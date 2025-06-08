using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;
using Font = Microsoft.Maui.Font;

namespace XiaoZhiAI_MAUI
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();

            // 移除MainPage路由，因为已经不需要了
            Routing.RegisterRoute(nameof(Pages.SettingsPage), typeof(Pages.SettingsPage));
            Routing.RegisterRoute(nameof(Pages.ChatPage), typeof(Pages.ChatPage));
            
            // 确保Tab导航在所有平台都可见
            Shell.SetTabBarIsVisible(this, true);
            
#if WINDOWS
            // Windows平台特定设置，确保底部导航可见
            this.SetDynamicResource(Shell.TabBarBackgroundColorProperty, "Gray100");
#endif
        }
    }
}
