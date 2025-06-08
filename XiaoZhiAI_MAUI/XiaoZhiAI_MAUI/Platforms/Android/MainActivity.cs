using Android.App;
using Android.Content.PM;
using Android.OS;
using AndroidX.Core.App;
using AndroidX.Core.Content;
using System.Collections.Generic;

namespace XiaoZhiAI_MAUI
{
    [Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
    public class MainActivity : MauiAppCompatActivity
    {
        private const int PERMISSION_REQUEST_CODE = 1001;

        protected override void OnCreate(Bundle savedInstanceState)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("MainActivity OnCreate 开始");
                
                base.OnCreate(savedInstanceState);
                
                System.Diagnostics.Debug.WriteLine("MainActivity OnCreate 基类调用完成");
                
                // 检查并请求权限
                CheckAndRequestPermissions();
                
                System.Diagnostics.Debug.WriteLine("MainActivity OnCreate 完成");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"MainActivity OnCreate 异常: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"异常详情: {ex}");
                // 重新抛出异常，以便于调试
                throw;
            }
        }

        private void CheckAndRequestPermissions()
        {
            var permissions = new[]
            {
                Android.Manifest.Permission.RecordAudio,
                Android.Manifest.Permission.ModifyAudioSettings,
                Android.Manifest.Permission.ForegroundService,
                Android.Manifest.Permission.PostNotifications
            };

            var permissionsToRequest = new List<string>();

            foreach (var permission in permissions)
            {
                if (ContextCompat.CheckSelfPermission(this, permission) != Permission.Granted)
                {
                    permissionsToRequest.Add(permission);
                }
            }

            if (permissionsToRequest.Count > 0)
            {
                ActivityCompat.RequestPermissions(this, permissionsToRequest.ToArray(), PERMISSION_REQUEST_CODE);
            }
        }

        public override void OnRequestPermissionsResult(int requestCode, string[] permissions, Permission[] grantResults)
        {
            base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
            
            if (requestCode == PERMISSION_REQUEST_CODE)
            {
                for (int i = 0; i < permissions.Length; i++)
                {
                    System.Diagnostics.Debug.WriteLine($"权限 {permissions[i]}: {grantResults[i]}");
                }
            }
        }
    }
}
