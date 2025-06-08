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
    private readonly IBackgroundService _backgroundService;
    private readonly IAudioService _audioService;
        private CancellationTokenSource _cts;
        private const int MaxMessages = 200;

        public ChatPage()
        {
            InitializeComponent();
            _webSocketService = IPlatformApplication.Current.Services.GetService<IWebSocketService>();
            _backgroundService = IPlatformApplication.Current.Services.GetService<IBackgroundService>();
            _audioService = IPlatformApplication.Current.Services.GetService<IAudioService>();
            
            _webSocketService.StatusChanged += OnWebSocketStatusChanged;
            _webSocketService.MessageReceived += OnWebSocketMessageReceived;
            _webSocketService.BinaryMessageReceived += OnWebSocketBinaryMessageReceived;
            _backgroundService.StatusChanged += OnBackgroundServiceStatusChanged;
            
            // 启动后自动连接
            _cts = new CancellationTokenSource();
            ConnectToServer();
            
            // 启动后台服务
            StartBackgroundService();
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            // 页面出现后再初始化音频服务
            await Task.Delay(1000); // 延迟1秒确保页面完全加载
            InitializeAudioService();
        }

        private async void InitializeAudioService()
        {
            try
            {
                if (_audioService != null)
                {
                    await _audioService.InitializeAsync();
                    
                    // 订阅音频事件
                    _audioService.AudioDataReady += OnAudioDataReady;
                    _audioService.RecordingStatusChanged += OnRecordingStatusChanged;
                    _audioService.PlaybackStatusChanged += OnPlaybackStatusChanged;
                    _audioService.VoiceActivityDetected += OnVoiceActivityDetected;
                    
                    AddMessageSafe(new ChatMessage
                    {
                        Type = ChatMessageType.System,
                        Content = "音频服务已初始化"
                    });
                }
            }
            catch (Exception ex)
            {
                AddMessageSafe(new ChatMessage
                {
                    Type = ChatMessageType.System,
                    Content = $"音频服务初始化失败: {ex.Message}"
                });
            }
        }

        private async void StartBackgroundService()
        {
            try
            {
                await _backgroundService.StartAsync();
                AddMessageSafe(new ChatMessage
                {
                    Type = ChatMessageType.System,
                    Content = "后台服务已启动，应用可在后台保持连接"
                });
            }
            catch (Exception ex)
            {
                AddMessageSafe(new ChatMessage
                {
                    Type = ChatMessageType.System,
                    Content = $"启动后台服务失败: {ex.Message}"
                });
            }
        }

        private void OnBackgroundServiceStatusChanged(object sender, bool isRunning)
        {
            var status = isRunning ? "运行中" : "已停止";
            AddMessageSafe(new ChatMessage
            {
                Type = ChatMessageType.System,
                Content = $"后台服务状态: {status}"
            });
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
                
                // 更新状态显示
                var (icon, statusText) = status switch
                {
                    WebSocketStatus.Connected => ("🟢", "准备就绪"),
                    WebSocketStatus.Connecting => ("🔄", "连接中"),
                    WebSocketStatus.Disconnected => ("🔴", "离线"),
                    WebSocketStatus.Disconnecting => ("⏸️", "断开中"),
                    WebSocketStatus.Error => ("❌", "连接错误"),
                    _ => ("⚪", "未知状态")
                };
                UpdateStatusDisplay(icon, statusText);
                
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
                // 改进消息分类逻辑
                if (IsSystemMessage(message))
                {
                    AddMessageSafe(new ChatMessage
                    {
                        Type = ChatMessageType.System,
                        Content = message
                    });
                }
                else if (IsJsonMessage(message))
                {
                    // 尝试解析JSON消息
                    ParseJsonMessage(message);
                }
                else
                {
                    // 普通文本消息，当作AI回复
                    UpdateStatusDisplay("💬", "AI回复中");
                    AddMessageSafe(new ChatMessage
                    {
                        Type = ChatMessageType.AI,
                        Avatar = "avatar_ai.png",
                        Content = message,
                        Time = DateTime.Now
                    });
                    
                    // 延迟恢复准备状态
                    _ = Task.Run(async () =>
                    {
                        await Task.Delay(2000);
                        UpdateStatusDisplay("🟢", "准备就绪");
                    });
                }
            }
            catch (Exception ex)
            {
                AddMessageSafe(new ChatMessage
                {
                    Type = ChatMessageType.System,
                    Content = $"消息处理异常: {ex.Message}"
                });
            }
        }

        private void OnWebSocketBinaryMessageReceived(object sender, byte[] binaryData)
        {
            try
            {
                // 接收到服务器的音频数据，直接播放（已通过Opus解码）
                _audioService?.PlayAudio(binaryData);
                
                AddMessageSafe(new ChatMessage
                {
                    Type = ChatMessageType.System,
                    Content = $"接收到音频数据: {binaryData.Length} 字节，正在播放"
                });
            }
            catch (Exception ex)
            {
                AddMessageSafe(new ChatMessage
                {
                    Type = ChatMessageType.System,
                    Content = $"音频播放异常: {ex.Message}"
                });
            }
        }



        private bool IsSystemMessage(string message)
        {
            return message.StartsWith("OTA") || 
                   message.StartsWith("Connection") || 
                   message.StartsWith("Send failed") || 
                   message.StartsWith("Handshake") || 
                   message.StartsWith("Using WebSocketUrl") || 
                   message.StartsWith("HELLO") || 
                   message.StartsWith("SessionId") || 
                   message.StartsWith("TCP Connection") || 
                   message.StartsWith("Performing OTA check") || 
                   message.StartsWith("Failed to parse") || 
                   message.StartsWith("Server closed") || 
                   message.StartsWith("Receive loop error") || 
                   message.StartsWith("Connection failed") || 
                   message.StartsWith("Inner Exception") || 
                   message.StartsWith("OTA check") || 
                   message.StartsWith("Waiting for server HELLO") ||
                   message.StartsWith("Sending detect message") ||
                   message.StartsWith("Cannot send detect message");
        }

        private bool IsJsonMessage(string message)
        {
            return message.TrimStart().StartsWith("{") && message.TrimEnd().EndsWith("}");
        }

        private void ParseJsonMessage(string message)
        {
            try
            {
                var jsonObj = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(message);
                
                if (jsonObj.TryGetProperty("type", out var typeElement))
                {
                    var messageType = typeElement.GetString();
                    
                    switch (messageType)
                    {
                        case "tts":
                            if (jsonObj.TryGetProperty("text", out var textElement))
                            {
                                var text = textElement.GetString();
                                if (!string.IsNullOrEmpty(text))
                                {
                                    UpdateStatusDisplay("💬", "AI回复中");
                                    AddMessageSafe(new ChatMessage
                                    {
                                        Type = ChatMessageType.AI,
                                        Avatar = "avatar_ai.png",
                                        Content = text,
                                        Time = DateTime.Now
                                    });
                                }
                            }
                            // 其他tts状态信息作为系统消息
                            if (jsonObj.TryGetProperty("state", out var stateElement))
                            {
                                var state = stateElement.GetString();
                                
                                // 处理TTS状态
                                if (state == "end")
                                {
                                    // TTS结束，重置播放缓冲区
                                    _audioService?.ResetPlayback();
                                    UpdateStatusDisplay("🟢", "准备就绪");
                                }
                                else if (state == "start")
                                {
                                    UpdateStatusDisplay("🔊", "AI说话中");
                                }
                                
                                AddMessageSafe(new ChatMessage
                                {
                                    Type = ChatMessageType.System,
                                    Content = $"TTS状态: {state}"
                                });
                            }
                            break;
                            
                        case "stt":
                            if (jsonObj.TryGetProperty("text", out var sttTextElement))
                            {
                                var text = sttTextElement.GetString();
                                if (!string.IsNullOrEmpty(text))
                                {
                                    UpdateStatusDisplay("📝", "AI处理中");
                                    AddMessageSafe(new ChatMessage
                                    {
                                        Type = ChatMessageType.User,
                                        Avatar = "avatar_user.png",
                                        Content = $"[语音识别] {text}",
                                        Time = DateTime.Now
                                    });
                                }
                            }
                            break;
                            
                        default:
                            // 其他JSON消息作为系统消息
                            AddMessageSafe(new ChatMessage
                            {
                                Type = ChatMessageType.System,
                                Content = $"[{messageType}] {message}"
                            });
                            break;
                    }
                }
                else
                {
                    // 无类型的JSON消息
                    AddMessageSafe(new ChatMessage
                    {
                        Type = ChatMessageType.System,
                        Content = message
                    });
                }
            }
            catch
            {
                // JSON解析失败，当作系统消息
                AddMessageSafe(new ChatMessage
                {
                    Type = ChatMessageType.System,
                    Content = message
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
                var sysFrame = new Frame
                {
                    BackgroundColor = Color.FromArgb("#E8E8E8"),
                    CornerRadius = 8,
                    Padding = new Thickness(12, 6),
                    HasShadow = false,
                    HorizontalOptions = LayoutOptions.Center,
                    Margin = new Thickness(40, 4, 40, 4),
                    Content = new Label 
                    { 
                        Text = msg.Content,
                        FontSize = 12,
                        TextColor = Color.FromArgb("#666"),
                        HorizontalTextAlignment = TextAlignment.Center,
                        LineBreakMode = LineBreakMode.WordWrap
                    }
                };
                ChatStack.Children.Add(sysFrame);
            }
            else if (msg.Type == ChatMessageType.AI)
            {
                var contentLabel = new Label 
                { 
                    Text = msg.Content, 
                    FontSize = 15, 
                    TextColor = Color.FromArgb("#222"),
                    LineBreakMode = LineBreakMode.WordWrap
                };
                
                var bubble = new Frame
                {
                    BackgroundColor = Color.FromArgb("#A5F3A1"),
                    CornerRadius = 12,
                    Padding = 12,
                    HasShadow = false,
                    Content = contentLabel,
                    Margin = new Thickness(0),
                    MaximumWidthRequest = 280,
                    HorizontalOptions = LayoutOptions.Start
                };
                
                var avatar = new Image
                {
                    Source = msg.Avatar ?? "avatar_ai.png",
                    WidthRequest = 36,
                    HeightRequest = 36,
                    VerticalOptions = LayoutOptions.Start,
                    Margin = new Thickness(0, 0, 8, 0)
                };
                
                var row = new HorizontalStackLayout
                {
                    Spacing = 0,
                    Padding = new Thickness(10, 4, 60, 4),
                    HorizontalOptions = LayoutOptions.Fill
                };
                row.Children.Add(avatar);
                row.Children.Add(bubble);
                ChatStack.Children.Add(row);
            }
            else if (msg.Type == ChatMessageType.User)
            {
                var contentLabel = new Label 
                { 
                    Text = msg.Content, 
                    FontSize = 15, 
                    TextColor = Color.FromArgb("#222"),
                    LineBreakMode = LineBreakMode.WordWrap
                };
                
                var bubble = new Frame
                {
                    BackgroundColor = Color.FromArgb("#95C8F7"),
                    CornerRadius = 12,
                    Padding = 12,
                    HasShadow = false,
                    Content = contentLabel,
                    Margin = new Thickness(0),
                    MaximumWidthRequest = 280,
                    HorizontalOptions = LayoutOptions.End
                };
                
                var avatar = new Image
                {
                    Source = msg.Avatar ?? "avatar_user.png",
                    WidthRequest = 36,
                    HeightRequest = 36,
                    VerticalOptions = LayoutOptions.Start,
                    Margin = new Thickness(8, 0, 0, 0)
                };
                
                var row = new HorizontalStackLayout
                {
                    Spacing = 0,
                    Padding = new Thickness(60, 4, 10, 4),
                    HorizontalOptions = LayoutOptions.Fill
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
                    // 更新状态并发送到服务器
                    UpdateStatusDisplay("📤", "发送中");
                    _ = _webSocketService.SendDetectMessageAsync(text, CancellationToken.None);
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
        private async void OnRecordClicked(object sender, EventArgs e)
        {
            try
            {
                if (_audioService == null)
                {
                    await DisplayAlert("错误", "音频服务未初始化", "确定");
                    return;
                }

                if (_audioService.IsRecording)
                {
                    // 停止录音
                    await _audioService.StopRecordingAsync();
                }
                else
                {
                    // 开始录音
                    await _audioService.StartRecordingAsync();
                }
            }
            catch (Exception ex)
            {
                AddMessageSafe(new ChatMessage
                {
                    Type = ChatMessageType.System,
                    Content = $"录音操作失败: {ex.Message}"
                });
            }
        }

        // 音频事件处理方法
        private async void OnAudioDataReady(object sender, byte[] audioData)
        {
            try
            {
                // 发送音频数据到服务器
                if (_webSocketService.Status == WebSocketStatus.Connected)
                {
                    await _webSocketService.SendBinaryAsync(audioData, _cts.Token);
                }
            }
            catch (Exception ex)
            {
                AddMessageSafe(new ChatMessage
                {
                    Type = ChatMessageType.System,
                    Content = $"发送音频数据失败: {ex.Message}"
                });
            }
        }

        private void OnRecordingStatusChanged(object sender, bool isRecording)
        {
            if (isRecording)
            {
                UpdateStatusDisplay("🔴", "AI聆听中");
                AddMessageSafe(new ChatMessage
                {
                    Type = ChatMessageType.System,
                    Content = "开始录音 - AI正在聆听..."
                });
            }
            else
            {
                UpdateStatusDisplay("🟢", "准备就绪");
                AddMessageSafe(new ChatMessage
                {
                    Type = ChatMessageType.System,
                    Content = "停止录音"
                });
            }
        }

        private void OnPlaybackStatusChanged(object sender, bool isPlaying)
        {
            if (isPlaying)
            {
                UpdateStatusDisplay("🔊", "AI说话中");
                AddMessageSafe(new ChatMessage
                {
                    Type = ChatMessageType.System,
                    Content = "AI开始说话..."
                });
            }
            else
            {
                UpdateStatusDisplay("🟢", "准备就绪");
                AddMessageSafe(new ChatMessage
                {
                    Type = ChatMessageType.System,
                    Content = "AI说话结束"
                });
            }
        }

        private void OnVoiceActivityDetected(object sender, bool hasVoice)
        {
            if (hasVoice)
            {
                UpdateStatusDisplay("🎤", "用户说话中");
                AddMessageSafe(new ChatMessage
                {
                    Type = ChatMessageType.System,
                    Content = "检测到用户语音..."
                });
            }
            else
            {
                UpdateStatusDisplay("🔴", "AI聆听中");
                AddMessageSafe(new ChatMessage
                {
                    Type = ChatMessageType.System,
                    Content = "用户语音结束，AI处理中..."
                });
            }
        }

        private void UpdateStatusDisplay(string icon, string text)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                try
                {
                    if (StatusIcon != null) StatusIcon.Text = icon;
                    if (StatusText != null) StatusText.Text = text;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"更新状态显示失败: {ex.Message}");
                }
            });
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