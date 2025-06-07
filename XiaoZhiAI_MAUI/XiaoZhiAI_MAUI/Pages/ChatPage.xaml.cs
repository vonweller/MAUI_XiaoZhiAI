using System;
using System.Collections.ObjectModel;
using Microsoft.Maui.Controls;
using XiaoZhiAI_MAUI.Services;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Maui.ApplicationModel;

namespace XiaoZhiAI_MAUI.Pages
{
    public partial class ChatPage : ContentPage
    {
        private readonly IWebSocketService _webSocketService;
        private CancellationTokenSource _cts;
        private const int MaxMessages = 200;

        public ChatPage()
        {
            InitializeComponent();
            _webSocketService = IPlatformApplication.Current.Services.GetService<IWebSocketService>();
            _webSocketService.StatusChanged += OnWebSocketStatusChanged;
            _webSocketService.MessageReceived += OnWebSocketMessageReceived;
            // 启动后自动连接
            _cts = new CancellationTokenSource();
            ConnectToServer();
        }

        private async void ConnectToServer()
        {
            try
            {
                AddMessageSafe(new ChatMessage
                {
                    Type = ChatMessageType.System,
                    Content = "正在连接服务器..."
                });
                await _webSocketService.ConnectAsync(_cts.Token);
            }
            catch (Exception ex)
            {
                AddMessageSafe(new ChatMessage
                {
                    Type = ChatMessageType.System,
                    Content = $"连接失败: {ex.Message}"
                });
            }
        }

        private void OnWebSocketStatusChanged(object sender, WebSocketStatus status)
        {
            try
            {
                string msg = status switch
                {
                    WebSocketStatus.Connected => "已连接服务器",
                    WebSocketStatus.Connecting => "正在连接...",
                    WebSocketStatus.Disconnected => "已断开连接",
                    WebSocketStatus.Disconnecting => "正在断开...",
                    WebSocketStatus.Error => "连接出错",
                    _ => status.ToString()
                };
                AddMessageSafe(new ChatMessage
                {
                    Type = ChatMessageType.System,
                    Content = msg
                });
            }
            catch (Exception ex)
            {
                AddMessageSafe(new ChatMessage
                {
                    Type = ChatMessageType.System,
                    Content = $"状态回调异常: {ex.Message}"
                });
            }
        }

        private void OnWebSocketMessageReceived(object sender, string message)
        {
            try
            {
                // 这里可根据消息内容判断AI/系统/用户消息
                if (message.StartsWith("OTA") || message.StartsWith("Connection") || message.StartsWith("Send failed") || message.StartsWith("Handshake") || message.StartsWith("Using WebSocketUrl") || message.StartsWith("HELLO") || message.StartsWith("SessionId") || message.StartsWith("TCP Connection") || message.StartsWith("Performing OTA check") || message.StartsWith("Failed to parse") || message.StartsWith("Server closed") || message.StartsWith("Receive loop error") || message.StartsWith("Connection failed") || message.StartsWith("Inner Exception") || message.StartsWith("OTA check threw") || message.StartsWith("OTA check failed") || message.StartsWith("OTA check successful") || message.StartsWith("Waiting for server HELLO response"))
                {
                    AddMessageSafe(new ChatMessage
                    {
                        Type = ChatMessageType.System,
                        Content = message
                    });
                }
                else
                {
                    AddMessageSafe(new ChatMessage
                    {
                        Type = ChatMessageType.AI,
                        Avatar = "avatar_ai.png",
                        Content = message,
                        Time = DateTime.Now
                    });
                }
            }
            catch (Exception ex)
            {
                AddMessageSafe(new ChatMessage
                {
                    Type = ChatMessageType.System,
                    Content = $"消息回调异常: {ex.Message}"
                });
            }
        }

        private void AddMessageSafe(ChatMessage msg)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                try
                {
                    // 限制气泡总数
                    while (ChatStack.Children.Count > MaxMessages * 2) // 每条消息2个控件（时间+气泡）
                    {
                        ChatStack.Children.RemoveAt(0);
                    }
                    AddMessage(msg);
                }
                catch (Exception ex)
                {
                    // 避免UI异常导致死循环
                }
            });
        }

        private void AddMessage(ChatMessage msg)
        {
            // 时间戳
            if (msg.Type != ChatMessageType.System)
            {
                var timeLabel = new Label
                {
                    Text = (msg.Time == default ? DateTime.Now : msg.Time).ToString("HH:mm"),
                    FontSize = 12,
                    TextColor = Color.FromArgb("#888"),
                    HorizontalOptions = LayoutOptions.Center,
                    Margin = new Thickness(0, 8, 0, 0)
                };
                ChatStack.Children.Add(timeLabel);
            }

            if (msg.Type == ChatMessageType.System)
            {
                var sysLabel = new Label
                {
                    Text = "[系统] " + msg.Content,
                    FontSize = 13,
                    TextColor = Color.FromArgb("#888"),
                    BackgroundColor = Color.FromArgb("#F0F0F0"),
                    HorizontalOptions = LayoutOptions.Center,
                    Padding = new Thickness(10, 4),
                    Margin = new Thickness(0, 4, 0, 0),
                    LineBreakMode = LineBreakMode.WordWrap
                };
                ChatStack.Children.Add(sysLabel);
            }
            else if (msg.Type == ChatMessageType.AI)
            {
                var bubble = new Frame
                {
                    BackgroundColor = Color.FromArgb("#A5F3A1"),
                    CornerRadius = 12,
                    Padding = 10,
                    HasShadow = false,
                    Content = new Label { Text = msg.Content, FontSize = 15, TextColor = Color.FromArgb("#222") },
                    Margin = new Thickness(0)
                };
                var avatar = new Image
                {
                    Source = msg.Avatar ?? "avatar_ai.png",
                    WidthRequest = 36,
                    HeightRequest = 36,
                    VerticalOptions = LayoutOptions.Start,
                    Margin = new Thickness(0,0,0,0)
                };
                var row = new HorizontalStackLayout
                {
                    Spacing = 6,
                    Padding = new Thickness(0,0,0,0),
                    HorizontalOptions = LayoutOptions.Start
                };
                row.Children.Add(avatar);
                row.Children.Add(bubble);
                ChatStack.Children.Add(row);
            }
            else if (msg.Type == ChatMessageType.User)
            {
                var bubble = new Frame
                {
                    BackgroundColor = Color.FromArgb("#BFE3F9"),
                    CornerRadius = 12,
                    Padding = 10,
                    HasShadow = false,
                    Content = new Label { Text = msg.Content, FontSize = 15, TextColor = Color.FromArgb("#222") },
                    Margin = new Thickness(0)
                };
                var avatar = new Image
                {
                    Source = msg.Avatar ?? "avatar_user.png",
                    WidthRequest = 36,
                    HeightRequest = 36,
                    VerticalOptions = LayoutOptions.Start,
                    Margin = new Thickness(0,0,0,0)
                };
                var row = new HorizontalStackLayout
                {
                    Spacing = 6,
                    Padding = new Thickness(0,0,0,0),
                    HorizontalOptions = LayoutOptions.End
                };
                row.Children.Add(bubble);
                row.Children.Add(avatar);
                ChatStack.Children.Add(row);
            }

            // 自动滚动到底部
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                try
                {
                    await Task.Delay(100);
                    if (ChatStack.Parent is ScrollView sv)
                        await sv.ScrollToAsync(ChatStack, ScrollToPosition.End, true);
                }
                catch { }
            });
        }

        private void OnSendClicked(object sender, EventArgs e)
        {
            try
            {
                var text = MessageEntry.Text?.Trim();
                if (!string.IsNullOrEmpty(text))
                {
                    AddMessageSafe(new ChatMessage
                    {
                        Type = ChatMessageType.User,
                        Avatar = "avatar_user.png",
                        Content = text,
                        Time = DateTime.Now
                    });
                    MessageEntry.Text = string.Empty;
                    // 发送到服务器
                    _ = _webSocketService.SendTextAsync(text, CancellationToken.None);
                }
            }
            catch (Exception ex)
            {
                AddMessageSafe(new ChatMessage
                {
                    Type = ChatMessageType.System,
                    Content = $"发送消息异常: {ex.Message}"
                });
            }
        }
        private void OnRecordClicked(object sender, EventArgs e)
        {
            // 录音逻辑
        }
    }

    public enum ChatMessageType { System, AI, User }
    public class ChatMessage
    {
        public ChatMessageType Type { get; set; }
        public string Avatar { get; set; }
        public string Content { get; set; }
        public DateTime Time { get; set; }
    }
} 