using System;
using System.Collections.ObjectModel;
using Microsoft.Maui.Controls;
using XiaoZhiAI_MAUI.Services;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Maui.ApplicationModel;
using System.Diagnostics;

namespace XiaoZhiAI_MAUI.Pages
{
    public partial class ChatPage : ContentPage
    {
                private readonly IWebSocketService _webSocketService;
    private readonly IBackgroundService _backgroundService;
    private readonly IAudioService _audioService;
    private readonly ILogService _logService;
    private CancellationTokenSource _cts;
    private const int MaxMessages = 200;
    private readonly HashSet<string> _processedMessages = new(); // 防止重复消息

        public ChatPage()
        {
            InitializeComponent();
                    _webSocketService = IPlatformApplication.Current.Services.GetService<IWebSocketService>();
        _backgroundService = IPlatformApplication.Current.Services.GetService<IBackgroundService>();
        _audioService = IPlatformApplication.Current.Services.GetService<IAudioService>();
        _logService = IPlatformApplication.Current.Services.GetService<ILogService>();
        
        // 初始化LogService
        if (_logService != null)
        {
            _logService.LogInfo("ChatPage已初始化");
        }
            
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
                    
                                    // 音频服务初始化消息不显示在聊天界面，只在测试日志中记录
                _logService?.LogInfo("音频服务已初始化");
                }
            }
            catch (Exception ex)
            {
                // 异常消息不显示在聊天界面，只在测试日志中记录
                _logService?.LogError($"音频服务初始化失败: {ex.Message}");
            }
        }

        private async void StartBackgroundService()
        {
            try
            {
                await _backgroundService.StartAsync();
                // 后台服务消息不显示在聊天界面，只在测试日志中记录
                _logService?.LogInfo("后台服务已启动，应用可在后台保持连接");
            }
            catch (Exception ex)
            {
                // 异常消息不显示在聊天界面，只在测试日志中记录
                _logService?.LogError($"启动后台服务失败: {ex.Message}");
            }
        }

        private void OnBackgroundServiceStatusChanged(object sender, bool isRunning)
        {
            var status = isRunning ? "运行中" : "已停止";
            // 后台服务状态消息不显示在聊天界面，只在测试日志中记录
            _logService?.LogDebug($"后台服务状态: {status}");
        }

        private async void ConnectToServer()
        {
            try
            {
                // 连接消息不显示在聊天界面，只在测试日志中记录
                _logService?.LogInfo("正在连接服务器...");
                await _webSocketService.ConnectAsync(_cts.Token);
            }
            catch (Exception ex)
            {
                // 连接失败消息不显示在聊天界面，只在测试日志中记录
                _logService?.LogError($"连接失败: {ex.Message}");
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
                
                // WebSocket状态消息不显示在聊天界面，只在测试日志中记录
                _logService?.LogDebug($"WebSocket状态: {msg}");
            }
            catch (Exception ex)
            {
                // 状态回调异常不显示在聊天界面，只在测试日志中记录
                _logService?.LogError($"状态回调异常: {ex.Message}");
            }
        }

        private void OnWebSocketMessageReceived(object sender, string message)
        {
            try
            {
                // 改进消息分类逻辑
                if (IsSystemMessage(message))
                {
                    // 系统消息不显示在聊天界面，只在测试日志中记录
                    _logService?.LogDebug($"系统消息: {message}");
                }
                else if (IsJsonMessage(message))
                {
                    // 尝试解析JSON消息
                    ParseJsonMessage(message);
                }
                else
                {
                    // 非JSON的普通文本消息，记录到测试日志中，不显示为聊天气泡
                    // 因为AI的回复应该通过tts类型的JSON消息来处理
                    _logService?.LogDebug($"收到非JSON文本消息: {message}");
                }
            }
            catch (Exception ex)
            {
                // 异常信息不显示在聊天界面，只在测试日志中记录
                _logService?.LogError($"消息处理异常: {ex.Message}");
            }
        }

        private void OnWebSocketBinaryMessageReceived(object sender, byte[] binaryData)
        {
            try
            {
                // 接收到服务器的音频数据，直接播放（已通过Opus解码）
                _logService?.LogInfo($"接收到音频数据: {binaryData.Length} 字节，正在播放");
                _audioService?.PlayAudio(binaryData);
                
                // 不在UI中显示音频数据接收消息，只在Debug日志中记录
            }
            catch (Exception ex)
            {
                _logService?.LogError($"音频播放异常: {ex.Message}");
            }
        }



        private bool IsSystemMessage(string message)
        {
            // 识别系统消息，这些消息不应该在聊天界面显示
            return message.StartsWith("Connection failed") || 
                   message.StartsWith("Server closed") || 
                   message.StartsWith("Receive loop error") ||
                   message.StartsWith("Cannot send detect message") ||
                   message.StartsWith("SessionId captured:") ||  // SessionId消息
                   message.StartsWith("Server HELLO received") ||
                   message.StartsWith("HELLO message:") ||
                   message.StartsWith("Sending detect message:") ||
                   message.StartsWith("OTA check") ||
                   message.StartsWith("Using WebSocketUrl:") ||
                   message.StartsWith("Connecting to") ||
                   message.StartsWith("TCP Connection successful") ||
                   message.StartsWith("Sending HELLO message") ||
                   message.StartsWith("Waiting for server HELLO") ||
                   message.StartsWith("Handshake complete") ||
                   message.StartsWith("Send failed") ||
                   message.StartsWith("Send exception") ||
                   message.StartsWith("Binary send failed") ||
                   message.StartsWith("Binary send exception") ||
                   message.StartsWith("Error handling binary message") ||
                   message.StartsWith("Failed to parse websocket");
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
                        case "hello":
                            // 收到hello响应，自动开始监听（参考Unity逻辑）
                            if (jsonObj.TryGetProperty("session_id", out var sessionIdElement))
                            {
                                var sessionId = sessionIdElement.GetString();
                                if (!string.IsNullOrEmpty(sessionId))
                                {
                                    _logService?.LogInfo($"收到session_id: {sessionId}，开始监听");
                                    // 系统消息不显示在聊天界面，只在测试日志中记录
                                    _logService?.LogInfo("AI助手已就绪，开始监听");
                                    
                                    // 自动开始监听（参考Unity的StartListening逻辑）
                                    _ = Task.Run(async () => 
                                    {
                                        await Task.Delay(500); // 短暂延迟确保连接稳定
                                        await StartListening(sessionId);
                                    });
                                }
                            }
                            break;
                            
                        case "tts":
                            if (jsonObj.TryGetProperty("text", out var textElement))
                            {
                                var text = textElement.GetString();
                                if (!string.IsNullOrEmpty(text))
                                {
                                    // 使用消息内容作为唯一标识，防止重复显示
                                    var messageKey = $"ai_text_{text.GetHashCode()}";
                                    if (!_processedMessages.Contains(messageKey))
                                    {
                                        _processedMessages.Add(messageKey);
                                        UpdateStatusDisplay("💬", "AI回复中");
                                        AddMessageSafe(new ChatMessage
                                        {
                                            Type = ChatMessageType.AI,
                                            Avatar = "🤖", // AI头像
                                            Content = text,
                                            Time = DateTime.Now
                                        });
                                        
                                        // 清理旧的处理记录，避免内存泄漏
                                        if (_processedMessages.Count > 100)
                                        {
                                            _processedMessages.Clear();
                                        }
                                    }
                                }
                            }
                            // 处理TTS状态（参考Unity逻辑）
                            if (jsonObj.TryGetProperty("state", out var stateElement))
                            {
                                var state = stateElement.GetString();
                                
                                if (state == "start" || state == "sentence_start")
                                {
                                    // TTS开始播放，停止监听和录音避免回音（参考Unity逻辑）
                                    _audioService?.SetListenState("stop"); // 停止监听状态
                                    if (_audioService != null && _audioService.IsRecording)
                                    {
                                        _logService?.LogInfo("TTS开始，停止录音避免回音");
                                        _ = Task.Run(async () => await _audioService.StopRecordingAsync());
                                    }
                                    UpdateStatusDisplay("🔊", "AI说话中");
                                    
                                    // AI开始说话状态只在调试日志中显示，不在UI显示
                                    _logService?.LogInfo("AI开始说话");
                                }
                                else if (state == "stop")
                                {
                                    // TTS结束，重置播放缓冲区并重新开始监听（参考Unity逻辑）
                                    _logService?.LogInfo("TTS结束，重新开始监听");
                                    _audioService?.ResetPlayback();
                                    
                                    // 延迟重新开始监听，避免立即捕获到回音
                                    _ = Task.Run(async () => 
                                    {
                                        await Task.Delay(1500); // 1.5秒冷却时间，与Unity一致
                                        if (!string.IsNullOrEmpty(_webSocketService.SessionId))
                                        {
                                            await StartListening(_webSocketService.SessionId);
                                        }
                                    });
                                    
                                    UpdateStatusDisplay("🟢", "准备就绪");
                                    // AI说话结束状态只在调试日志中显示，不在UI显示
                                                                            _logService?.LogInfo("AI说话结束");
                                }
                                else
                                {
                                    // TTS其他状态消息不显示在聊天界面，只在测试日志中记录
                                    _logService?.LogDebug($"TTS状态: {state}");
                                }
                            }
                            break;
                            
                        case "stt":
                            if (jsonObj.TryGetProperty("text", out var sttTextElement))
                            {
                                var text = sttTextElement.GetString();
                                if (!string.IsNullOrEmpty(text))
                                {
                                    // 为用户消息生成防重复key（类型+内容）
                                    var userMessageKey = $"user:{text}";
                                    if (_processedMessages.Contains(userMessageKey))
                                    {
                                        _logService?.LogDebug($"跳过重复的用户消息: {text}");
                                        break;
                                    }
                                    _processedMessages.Add(userMessageKey);
                                    
                                    UpdateStatusDisplay("📝", "AI处理中");
                                    AddMessageSafe(new ChatMessage
                                    {
                                        Type = ChatMessageType.User,
                                        Avatar = "👤", // 用户头像
                                        Content = text, // 移除"[语音识别]"前缀
                                        Time = DateTime.Now
                                    });
                                }
                            }
                            break;
                            
                        default:
                            // 其他JSON消息不显示在聊天界面，只在测试日志中记录
                            _logService?.LogDebug($"收到消息: [{messageType}] {message}");
                            break;
                    }
                }
                else
                {
                    // 无类型的JSON消息不显示在聊天界面，只在测试日志中记录
                    _logService?.LogDebug($"收到无类型消息: {message}");
                }
            }
            catch
            {
                // JSON解析失败，不显示在聊天界面，只在测试日志中记录
                _logService?.LogError($"JSON解析失败: {message}");
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
            // 不再显示System类型消息，直接跳过
            if (msg.Type == ChatMessageType.System)
            {
                return;
            }

            // 时间戳（只为AI和User消息显示）
            var timeLabel = new Label
            {
                Text = (msg.Time == default ? DateTime.Now : msg.Time).ToString("HH:mm"),
                FontSize = 12,
                TextColor = Color.FromArgb("#888"),
                HorizontalOptions = LayoutOptions.Center,
                Margin = new Thickness(0, 8, 0, 0)
            };
            ChatStack.Children.Add(timeLabel);

            if (msg.Type == ChatMessageType.AI)
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
                
                var avatar = new Label
                {
                    Text = msg.Avatar ?? "🤖",
                    FontSize = 24,
                    WidthRequest = 36,
                    HeightRequest = 36,
                    VerticalOptions = LayoutOptions.Start,
                    HorizontalTextAlignment = TextAlignment.Center,
                    VerticalTextAlignment = TextAlignment.Center,
                    Margin = new Thickness(0, 0, 8, 0)
                };
                
                var row = new HorizontalStackLayout
                {
                    Spacing = 0,
                    Padding = new Thickness(10, 4, 60, 4),
                    HorizontalOptions = LayoutOptions.Fill,
                    BackgroundColor = Colors.Transparent // 设置透明背景
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
                
                var avatar = new Label
                {
                    Text = msg.Avatar ?? "👤",
                    FontSize = 24,
                    WidthRequest = 36,
                    HeightRequest = 36,
                    VerticalOptions = LayoutOptions.Start,
                    HorizontalTextAlignment = TextAlignment.Center,
                    VerticalTextAlignment = TextAlignment.Center,
                    Margin = new Thickness(8, 0, 0, 0)
                };
                
                // 使用Grid来确保用户消息靠右显示
                var grid = new Grid
                {
                    Padding = new Thickness(60, 4, 10, 4),
                    HorizontalOptions = LayoutOptions.FillAndExpand,
                    BackgroundColor = Colors.Transparent, // 设置透明背景
                    ColumnDefinitions = 
                    {
                        new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }, // 占位
                        new ColumnDefinition { Width = GridLength.Auto }, // 气泡
                        new ColumnDefinition { Width = GridLength.Auto }  // 头像
                    }
                };

                Grid.SetColumn(bubble, 1);
                Grid.SetColumn(avatar, 2);
                
                grid.Children.Add(bubble);
                grid.Children.Add(avatar);
                ChatStack.Children.Add(grid);
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
                    // 为用户消息生成防重复key（类型+内容）
                    var userMessageKey = $"user:{text}";
                    if (_processedMessages.Contains(userMessageKey))
                    {
                        _logService?.LogDebug($"跳过重复的用户消息: {text}");
                        MessageEntry.Text = string.Empty;
                        return;
                    }
                    _processedMessages.Add(userMessageKey);
                    
                    AddMessageSafe(new ChatMessage
                    {
                        Type = ChatMessageType.User,
                        Avatar = "👤", // 用户头像
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
                // 发送异常消息不显示在聊天界面，只在测试日志中记录
                _logService?.LogError($"发送消息异常: {ex.Message}");
            }
        }
        // 按住录音开始（参考Unity的OnSpaceKeyPress）
        private async void OnRecordPressed(object sender, EventArgs e)
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
                    // 如果已在录音，忽略重复按下
                    return;
                }

                // 开始录音
                UpdateStatusDisplay("🎤", "用户说话中");
                // 录音开始消息不显示在聊天界面，只在测试日志中记录
                _logService?.LogInfo("开始录音，请说话...");

                // 更新按钮外观
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    RecordButton.BackgroundColor = Colors.Red;
                    RecordButton.Text = "🔴";
                    RecordingHint.IsVisible = true;
                });

                await _audioService.StartRecordingAsync();
            }
            catch (Exception ex)
            {
                // 录音失败消息不显示在聊天界面，只在测试日志中记录
                _logService?.LogError($"开始录音失败: {ex.Message}");
            }
        }

        // 松开录音结束并发送（参考Unity的OnSpaceKeyRelease）
        private async void OnRecordReleased(object sender, EventArgs e)
        {
            try
            {
                if (_audioService == null || !_audioService.IsRecording)
                {
                    // 如果没在录音，忽略松开事件
                    return;
                }

                // 停止录音
                UpdateStatusDisplay("📤", "发送中");
                // 录音结束消息不显示在聊天界面，只在测试日志中记录
                _logService?.LogInfo("录音结束，正在处理并发送到服务器...");

                // 恢复按钮外观
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    RecordButton.BackgroundColor = Colors.Green;
                    RecordButton.Text = "🎤";
                    RecordingHint.IsVisible = false;
                });

                await _audioService.StopRecordingAsync();

                // 等待一下让最后的音频数据发送完
                await Task.Delay(500);
                UpdateStatusDisplay("🟢", "准备就绪");
            }
            catch (Exception ex)
            {
                // 停止录音失败消息不显示在聊天界面，只在测试日志中记录
                _logService?.LogError($"停止录音失败: {ex.Message}");
            }
        }

        // 音频事件处理方法
        private async void OnAudioDataReady(object sender, byte[] audioData)
        {
            try
            {
                _logService?.LogDebug($"OnAudioDataReady: 收到音频数据 {audioData?.Length ?? 0} 字节");
                
                if (audioData == null || audioData.Length == 0)
                {
                    _logService?.LogDebug("OnAudioDataReady: 音频数据为空，跳过发送");
                    return;
                }
                
                _logService?.LogDebug($"WebSocket状态: {_webSocketService.Status}");
                
                // 发送音频数据到服务器
                if (_webSocketService.Status == WebSocketStatus.Connected)
                {
                    _logService?.LogDebug($"正在发送音频数据到服务器: {audioData.Length} 字节");
                    bool success = await _webSocketService.SendBinaryAsync(audioData, _cts.Token);
                    
                    if (success)
                    {
                        _logService?.LogDebug($"音频数据发送成功: {audioData.Length} 字节");
                        // 减少UI噪音：不再显示每次音频数据发送的系统消息
                    }
                    else
                    {
                        _logService?.LogError($"音频数据发送失败: {audioData.Length} 字节");
                        // 音频发送失败消息不显示在聊天界面，只在测试日志中记录
                    }
                }
                else
                {
                    _logService?.LogError($"WebSocket未连接，无法发送音频数据。状态: {_webSocketService.Status}");
                    // WebSocket连接状态消息不显示在聊天界面，只在测试日志中记录
                }
            }
            catch (Exception ex)
            {
                _logService?.LogError($"OnAudioDataReady异常: {ex.Message}");
                // 音频数据发送异常消息不显示在聊天界面，只在测试日志中记录
            }
        }

        private void OnRecordingStatusChanged(object sender, bool isRecording)
        {
            // 只更新状态显示，不添加重复的系统消息
            if (isRecording)
            {
                UpdateStatusDisplay("🔴", "AI聆听中");
            }
            else
            {
                UpdateStatusDisplay("🟢", "准备就绪");
            }
        }

        private void OnPlaybackStatusChanged(object sender, bool isPlaying)
        {
            // 只更新状态显示，不添加重复的系统消息
            if (isPlaying)
            {
                UpdateStatusDisplay("🔊", "AI说话中");
            }
            else
            {
                UpdateStatusDisplay("🟢", "准备就绪");
            }
        }

        private void OnVoiceActivityDetected(object sender, bool hasVoice)
        {
            // 只更新状态显示，不添加重复的系统消息
            if (hasVoice)
            {
                UpdateStatusDisplay("🎤", "用户说话中");
            }
            else
            {
                UpdateStatusDisplay("🔴", "AI聆听中");
            }
        }

        private async Task StartListening(string sessionId)
        {
            try
            {
                if (string.IsNullOrEmpty(sessionId))
                {
                    _logService?.LogError("StartListening: sessionId为空");
                    return;
                }
                
                _logService?.LogInfo($"开始监听，sessionId: {sessionId}");
                
                // 发送listen start消息（参考Unity逻辑）
                var listenMsg = new
                {
                    session_id = sessionId,
                    type = "listen",
                    state = "start",
                    mode = "auto"  // 自动模式，与Unity一致
                };
                
                string json = System.Text.Json.JsonSerializer.Serialize(listenMsg);
                bool success = await _webSocketService.SendTextAsync(json, _cts.Token);
                
                if (success)
                {
                    _logService?.LogInfo("监听消息发送成功");
                    // 关键修复：设置AudioService监听状态为start（参考Unity逻辑）
                    _audioService?.SetListenState("start");
                    UpdateStatusDisplay("🔴", "AI聆听中");
                    // 监听状态变化只在测试日志中显示，不显示UI消息
                }
                else
                {
                    _logService?.LogError("监听消息发送失败");
                    // 监听启动失败消息不显示在聊天界面，只在测试日志中记录
                }
            }
            catch (Exception ex)
            {
                _logService?.LogError($"StartListening异常: {ex.Message}");
                // 监听启动异常消息不显示在聊天界面，只在测试日志中记录
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
                    _logService?.LogError($"更新状态显示失败: {ex.Message}");
                }
            });
        }

        // 辅助方法：记录日志到测试页面
        private void LogToTest(string message)
        {
            _logService?.LogMessage(message);
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