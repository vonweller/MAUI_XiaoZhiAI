using Microsoft.Maui.Storage;
namespace XiaoZhiAI_MAUI.Pages
{
    public partial class SettingsPage : ContentPage
    {
        public SettingsPage()
        {
            InitializeComponent();
            UrlEntry.Text = Preferences.Get("ServerUrl", "");
            MacEntry.Text = Preferences.Get("MacAddress", "");
            OtaEntry.Text = Preferences.Get("OtaUrl", "");
        }
        private void OnSaveClicked(object sender, EventArgs e)
        {
            Preferences.Set("ServerUrl", UrlEntry.Text);
            Preferences.Set("MacAddress", MacEntry.Text);
            Preferences.Set("OtaUrl", OtaEntry.Text);
            DisplayAlert("提示", "设置已保存", "OK");
        }
    }
} 