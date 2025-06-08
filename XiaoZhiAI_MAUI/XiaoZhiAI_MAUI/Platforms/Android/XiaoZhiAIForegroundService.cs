using Android.App;
using Android.Content;
using Android.OS;
using AndroidX.Core.App;

namespace XiaoZhiAI_MAUI.Platforms.Android
{
    [Service(ForegroundServiceType = global::Android.Content.PM.ForegroundService.TypeMicrophone)]
    public class XiaoZhiAIForegroundService : Service
    {
        private const int NOTIFICATION_ID = 1001;
        private const string CHANNEL_ID = "XiaoZhiAI_Service_Channel";
        private const string CHANNEL_NAME = "小智AI后台服务";

        public override IBinder OnBind(Intent intent)
        {
            return null;
        }

        public override StartCommandResult OnStartCommand(Intent intent, StartCommandFlags flags, int startId)
        {
            try
            {
                if (intent?.Action == "START_FOREGROUND_SERVICE")
                {
                    StartForegroundService();
                }
                else if (intent?.Action == "STOP_FOREGROUND_SERVICE")
                {
                    StopForegroundService();
                }

                return StartCommandResult.Sticky;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"前台服务启动失败: {ex.Message}");
                return StartCommandResult.NotSticky;
            }
        }

        private void StartForegroundService()
        {
            CreateNotificationChannel();
            
            var notification = CreateNotification();
            StartForeground(NOTIFICATION_ID, notification);
            
            System.Diagnostics.Debug.WriteLine("Android前台服务已启动");
        }

        private void StopForegroundService()
        {
            StopForeground(true);
            StopSelf();
            
            System.Diagnostics.Debug.WriteLine("Android前台服务已停止");
        }

        private void CreateNotificationChannel()
        {
            if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
            {
                var serviceChannel = new NotificationChannel(
                    CHANNEL_ID,
                    CHANNEL_NAME,
                    NotificationImportance.Low)
                {
                    Description = "小智AI语音助手后台运行通知"
                };

                var manager = GetSystemService(NotificationService) as NotificationManager;
                manager?.CreateNotificationChannel(serviceChannel);
            }
        }

        private Notification CreateNotification()
        {
            var intent = new Intent(this, typeof(MainActivity));
            intent.SetFlags(ActivityFlags.NewTask | ActivityFlags.ClearTask);
            
            var pendingIntent = PendingIntent.GetActivity(
                this, 
                0, 
                intent, 
                PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);

            var builder = new NotificationCompat.Builder(this, CHANNEL_ID)
                .SetContentTitle("小智AI助手")
                .SetContentText("正在后台运行，保持语音连接...")
                .SetSmallIcon(global::Android.Resource.Drawable.IcDialogInfo) // 使用系统图标
                .SetContentIntent(pendingIntent)
                .SetOngoing(true)
                .SetAutoCancel(false)
                .SetPriority(NotificationCompat.PriorityLow)
                .SetCategory(NotificationCompat.CategoryService);

            return builder.Build();
        }



        public override void OnDestroy()
        {
            base.OnDestroy();
            System.Diagnostics.Debug.WriteLine("前台服务已销毁");
        }
    }
} 