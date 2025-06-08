using Microsoft.Maui.Storage;
using XiaoZhiAI_MAUI.Services;

namespace XiaoZhiAI_MAUI.Pages
{
    public partial class SettingsPage : ContentPage
    {
        private readonly IBackgroundService _backgroundService;

        public SettingsPage()
        {
            InitializeComponent();
            
            _backgroundService = IPlatformApplication.Current.Services.GetService<IBackgroundService>();
            _backgroundService.StatusChanged += OnBackgroundServiceStatusChanged;
            
            // 设置默认值（从WebSocketService获取的默认配置）
            UrlEntry.Text = Preferences.Get("ServerUrl", "wss://api.tenclass.net/xiaozhi/v1/");
            MacEntry.Text = Preferences.Get("MacAddress", XiaoZhiAI_MAUI.Utils.DeviceInfoHelper.GetDeviceMacAddress());
            OtaEntry.Text = Preferences.Get("OtaUrl", "https://api.tenclass.net/xiaozhi/ota/");
            
            // 设置后台服务状态
            UpdateBackgroundServiceStatus();
        }

        private void UpdateBackgroundServiceStatus()
        {
            BackgroundSwitch.IsToggled = _backgroundService.IsRunning;
            BackgroundStatusLabel.Text = _backgroundService.IsRunning ? "运行中" : "已停止";
            BackgroundStatusLabel.TextColor = _backgroundService.IsRunning ? Colors.Green : Colors.Gray;
        }

        private void OnBackgroundServiceStatusChanged(object sender, bool isRunning)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                UpdateBackgroundServiceStatus();
            });
        }

        private async void OnBackgroundSwitchToggled(object sender, ToggledEventArgs e)
        {
            try
            {
                if (e.Value)
                {
                    await _backgroundService.StartAsync();
                }
                else
                {
                    await _backgroundService.StopAsync();
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("错误", $"后台服务操作失败：{ex.Message}", "确定");
                BackgroundSwitch.IsToggled = _backgroundService.IsRunning;
            }
        }
        private async void OnSaveClicked(object sender, EventArgs e)
        {
            try
            {
                // 验证URL格式
                if (!string.IsNullOrEmpty(UrlEntry.Text) && !UrlEntry.Text.StartsWith("wss://") && !UrlEntry.Text.StartsWith("ws://"))
                {
                    await DisplayAlert("错误", "服务器URL必须以wss://或ws://开头", "确定");
                    return;
                }
                
                if (!string.IsNullOrEmpty(OtaEntry.Text) && !OtaEntry.Text.StartsWith("https://") && !OtaEntry.Text.StartsWith("http://"))
                {
                    await DisplayAlert("错误", "OTA地址必须以https://或http://开头", "确定");
                    return;
                }

                // 保存设置
                Preferences.Set("ServerUrl", UrlEntry.Text?.Trim() ?? "");
                Preferences.Set("MacAddress", MacEntry.Text?.Trim() ?? "");
                Preferences.Set("OtaUrl", OtaEntry.Text?.Trim() ?? "");
                
                await DisplayAlert("✅ 保存成功", 
                    "设置已保存成功！\n\n📱 请切换到聊天页面，设置将在下次连接时生效。", 
                    "确定");
            }
            catch (Exception ex)
            {
                await DisplayAlert("错误", $"保存设置时出错：{ex.Message}", "确定");
            }
        }
    }
} 