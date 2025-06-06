using XiaoZhiSharp;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Net.WebSockets;
using System.Net.NetworkInformation;
using Microsoft.Maui.Networking;
using XiaoZhiSharpMAUI.Services;
using OpusSharp.Core;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace XiaoZhiSharpMAUI
{
    public partial class MainPage : ContentPage
    {
        private readonly XiaoZhiAgent _xiaoZhiAgent;
        private readonly ILogger<MainPage> _logger;
        private readonly IMauiAudioService? _mauiAudioService;
        private bool _isRecording = false;
        private bool _isConnected = false;
        private bool IsAndroidPlatform => DeviceInfo.Platform == DevicePlatform.Android;
        private ClientWebSocket? _androidWebSocket;
        private CancellationTokenSource? _androidWebSocketCts;
        private string? _androidSessionId;
        
        // 性能优化：减少UI更新频率
        private DateTime _lastScrollTime = DateTime.MinValue;
        private readonly SemaphoreSlim _audioProcessingSemaphore = new SemaphoreSlim(1, 1);
        private int _audioProcessedCount = 0;
        private OpusDecoder? _opusDecoder;
        
        // 🔧 音频缓冲机制 - 类似PC版本
        private readonly ConcurrentQueue<short[]> _audioBufferQueue = new ConcurrentQueue<short[]>();
        private readonly SemaphoreSlim _audioPlaybackSemaphore = new SemaphoreSlim(1, 1);
        private bool _isAudioPlaying = false;
        private CancellationTokenSource? _audioPlaybackCts;
        
        // 🔧 高级音频流管理
        private readonly List<short> _continuousAudioBuffer = new List<short>();
        private readonly object _audioBufferLock = new object();
        private DateTime _lastAudioReceived = DateTime.MinValue;
        private const int MIN_BUFFER_SIZE = 8; // 最少8个包才开始播放
        private const int OPTIMAL_BUFFER_SIZE = 12; // 理想缓冲大小
        private const int MAX_BATCH_DURATION_MS = 800; // 单次播放最长800ms

        // 🔧 重新设计：完整语音段播放机制
        private readonly List<short> _speechBuffer = new List<short>();
        private readonly object _speechBufferLock = new object();
        private DateTime _lastAudioPacketTime = DateTime.MinValue;
        private bool _isSpeechActive = false;
        private readonly SemaphoreSlim _speechPlaybackSemaphore = new SemaphoreSlim(1, 1);
        private CancellationTokenSource? _speechTimeoutCts;
        
        // 语音检测参数
        private const int SPEECH_TIMEOUT_MS = 400; // 设置一个安全的超时，之后再优化
        private const int MIN_SPEECH_LENGTH_MS = 200; // 最短语音长度200ms
        private const int MAX_SPEECH_LENGTH_MS = 10000; // 最长语音10秒

        // 语音检测参数 - 流式播放优化
        private const int STREAM_START_THRESHOLD_MS = 240; // 缓冲240ms后立即开始播放
        private const int STREAM_END_TIMEOUT_MS = 400;     // 400ms无新数据则认为语音结束

        public MainPage(XiaoZhiAgent xiaoZhiAgent, ILogger<MainPage> logger, IMauiAudioService? mauiAudioService)
        {
            InitializeComponent();
            _xiaoZhiAgent = xiaoZhiAgent;
            _logger = logger;
            _mauiAudioService = mauiAudioService;
            
            InitializeXiaoZhiAgent();
        }

        private void InitializeXiaoZhiAgent()
        {
            try
            {
                UpdateStatus("正在初始化小智AI服务...");
                UpdateConnectionStatus("初始化中");
                
                AddSystemMessage("🔄 开始初始化小智AI服务");
                
                // 检查平台特定的配置
                if (IsAndroidPlatform)
                {
                    AddSystemMessage("📱 检测到Android平台，使用专用WebSocket连接");
                    _ = Task.Run(InitializeAndroidWebSocket);
                    return;
                }
                
                // 非Android平台使用原有的XiaoZhiAgent
                AddSystemMessage("🖥️ 使用XiaoZhiAgent服务");
                
                // 订阅消息事件
                _xiaoZhiAgent.OnMessageEvent += OnXiaoZhiMessage;
                _xiaoZhiAgent.OnAudioEvent += OnXiaoZhiAudio;
                
                AddSystemMessage("✅ 事件订阅完成");
                
                // 启动小智服务
                AddSystemMessage("🚀 启动XiaoZhiAgent服务...");
                _xiaoZhiAgent.Start();
                
                _isConnected = true;
                UpdateStatus("小智AI助手已启动");
                UpdateConnectionStatus("已连接");
                AddSystemMessage("🎉 XiaoZhiAgent服务启动成功");
                _logger.LogInformation("XiaoZhi Agent started successfully");
            }
            catch (Exception ex)
            {
                _isConnected = false;
                _logger.LogError(ex, "Failed to initialize XiaoZhi Agent");
                UpdateStatus("初始化失败，请点击重新连接");
                UpdateConnectionStatus("连接失败");
                
                // 显示详细错误信息
                AddSystemMessage($"❌ XiaoZhiAgent初始化失败");
                AddSystemMessage($"错误类型: {ex.GetType().Name}");
                AddSystemMessage($"错误信息: {ex.Message}");
                
                if (ex.InnerException != null)
                {
                    AddSystemMessage($"内部错误: {ex.InnerException.Message}");
                }
                
                AddSystemMessage("💡 建议: 点击'🔄 重新连接'按钮重试");
            }
        }
        
        private async Task InitializeAndroidWebSocket()
        {
            try
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    AddSystemMessage("🔌 初始化Android专用WebSocket连接...");
                });
                
                _androidWebSocketCts = new CancellationTokenSource();
                _androidWebSocket = new ClientWebSocket();
                
                // 设置请求头
                var token = "test-token";
                var deviceId = XiaoZhiSharp.Utils.SystemInfo.GetMacAddress();
                
                _androidWebSocket.Options.SetRequestHeader("Authorization", $"Bearer {token}");
                _androidWebSocket.Options.SetRequestHeader("Protocol-Version", "1");
                _androidWebSocket.Options.SetRequestHeader("Device-Id", deviceId);
                _androidWebSocket.Options.SetRequestHeader("Client-Id", Guid.NewGuid().ToString());
                
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    AddSystemMessage($"🔑 Token: {token}");
                    AddSystemMessage($"📱 Device-Id: {deviceId}");
                });
                
                await _androidWebSocket.ConnectAsync(new Uri("wss://api.tenclass.net/xiaozhi/v1/"), _androidWebSocketCts.Token);
                
                if (_androidWebSocket.State == WebSocketState.Open)
                {
                    // 🔧 关键修复：初始化共享的Opus解码器
                    const int sampleRate = 24000;
                    const int channels = 1;
                    _opusDecoder = new OpusDecoder(sampleRate, channels);
                    
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        _isConnected = true;
                        UpdateStatus("Android WebSocket已连接");
                        UpdateConnectionStatus("WebSocket已连接");
                        AddSystemMessage("✅ Android WebSocket连接成功");
                        AddSystemMessage("🎧 Opus解码器已初始化 (共享实例)");
                        AddSystemMessage("📝 您可以发送文字消息进行对话");
                        AddSystemMessage("🎤 录音功能使用MAUI音频服务");
                    });
                    
                    // 发送初始化Hello消息
                    var helloMessage = XiaoZhiSharp.Protocols.WebSocketProtocol.Hello("");
                    var helloBuffer = System.Text.Encoding.UTF8.GetBytes(helloMessage);
                    await _androidWebSocket.SendAsync(new ArraySegment<byte>(helloBuffer), WebSocketMessageType.Text, true, _androidWebSocketCts.Token);
                    
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        AddSystemMessage("📤 已发送初始化握手消息");
                        AddSystemMessage("⏳ 等待服务器响应...");
                    });
                    
                    // 启动消息接收循环
                    _ = Task.Run(AndroidWebSocketReceiveLoop);
                }
                else
                {
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        AddSystemMessage("❌ Android WebSocket连接失败");
                        UpdateStatus("连接失败");
                        UpdateConnectionStatus("连接失败");
                    });
                }
            }
            catch (Exception ex)
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    AddSystemMessage($"❌ Android WebSocket初始化失败: {ex.Message}");
                    UpdateStatus("连接失败");
                    UpdateConnectionStatus("连接失败");
                });
                _logger.LogError(ex, "Failed to initialize Android WebSocket");
            }
        }
        
        private async Task AndroidWebSocketReceiveLoop()
        {
            var buffer = new byte[1024 * 4];
            
            try
            {
                while (_androidWebSocket?.State == WebSocketState.Open && !_androidWebSocketCts?.Token.IsCancellationRequested == true)
                {
                    var result = await _androidWebSocket.ReceiveAsync(new ArraySegment<byte>(buffer), _androidWebSocketCts.Token);
                    
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        MainThread.BeginInvokeOnMainThread(() =>
                        {
                            AddSystemMessage("�� WebSocket连接已关闭");
                            _isConnected = false;
                            UpdateConnectionStatus("连接已断开");
                        });
                        break;
                    }
                    
                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        var messageText = System.Text.Encoding.UTF8.GetString(buffer, 0, result.Count);
                        
                        MainThread.BeginInvokeOnMainThread(() =>
                        {
                            try
                            {
                                AddSystemMessage($"📥 收到消息: {messageText}");
                                
                                // 解析session_id - 与PC版本一致的处理方式
                                if (messageText.Contains("session_id"))
                                {
                                    try
                                    {
                                        dynamic? json = JsonConvert.DeserializeObject<dynamic>(messageText);
                                        if (json?.session_id != null)
                                        {
                                            _androidSessionId = (string)json.session_id;
                                            AddSystemMessage($"🔑 获得Session ID: {_androidSessionId}");
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        _logger.LogWarning(ex, "Failed to parse session_id from message");
                                    }
                                }
                                
                                // 解析消息内容 - 按照PC版本的逻辑
                                dynamic? msg = JsonConvert.DeserializeObject<dynamic>(messageText);
                                if (msg != null)
                                {
                                    string msgType = msg.type?.ToString() ?? "";
                                    
                                    switch (msgType)
                                    {
                                        case "hello":
                                            AddSystemMessage("✅ 收到Hello响应");
                                            if (msg.session_id != null)
                                            {
                                                _androidSessionId = (string)msg.session_id;
                                                AddSystemMessage($"🔑 会话ID: {_androidSessionId}");
                                            }
                                            break;
                                            
                                        case "tts":
                                            string state = msg.state?.ToString() ?? "";
                                            if (state == "sentence_start" && msg.text != null)
                                            {
                                                // AI 回复消息
                                                string aiText = msg.text.ToString();
                                                AddAIMessage(aiText);
                                                UpdateStatus("收到AI回复");
                                                AddSystemMessage($"🤖 AI回复: {aiText}");
                                            }
                                            break;
                                            
                                        case "stt":
                                            if (msg.text != null)
                                            {
                                                // 语音转文字
                                                string sttText = msg.text.ToString();
                                                AddUserMessage($"🎤 {sttText}", true);
                                                UpdateStatus("语音识别完成");
                                                AddSystemMessage($"🎤 语音识别: {sttText}");
                                            }
                                            break;
                                            
                                        case "listen":
                                            string listenState = msg.state?.ToString() ?? "";
                                            AddSystemMessage($"👂 监听状态: {listenState}");
                                            break;
                                            
                                        default:
                                            AddSystemMessage($"📝 收到{msgType}类型消息");
                                            break;
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                AddSystemMessage($"📥 收到原始消息: {messageText}");
                                _logger.LogWarning(ex, "Failed to parse received message");
                            }
                        });
                    }
                    else if (result.MessageType == WebSocketMessageType.Binary)
                    {
                        // 🔧 性能优化：防止并发音频处理
                        _ = Task.Run(async () =>
                        {
                            // 使用信号量确保音频处理串行化
                            if (!await _audioProcessingSemaphore.WaitAsync(100))
                            {
                                // 如果无法获取锁，说明有其他音频正在处理，跳过
                                return;
                            }
                            
                            try
                            {
                                // 获取音频数据
                                byte[] audioData = new byte[result.Count];
                                Array.Copy(buffer, 0, audioData, 0, result.Count);
                                
                                // 简化UI更新：只显示关键信息
                                MainThread.BeginInvokeOnMainThread(() =>
                                {
                                    _audioProcessedCount++;
                                    // 每5个音频包显示一次状态，避免UI过度更新
                                    if (_audioProcessedCount % 5 == 1)
                                    {
                                        AddSystemMessage($"🔊 音频处理中... ({_audioProcessedCount})");
                                    }
                                });
                                
                                // 在后台线程处理音频（避免死锁）
                                if (_mauiAudioService != null && IsAndroidPlatform)
                                {
                                    await PlayAudioDataAsync(audioData);
                                }
                                else
                                {
                                    MainThread.BeginInvokeOnMainThread(() =>
                                    {
                                        AddSystemMessage("⚠️ 音频服务不可用");
                                    });
                                }
                            }
                            catch (Exception ex)
                            {
                                MainThread.BeginInvokeOnMainThread(() =>
                                {
                                    AddSystemMessage($"❌ 音频处理错误: {ex.Message}");
                                });
                                _logger.LogError(ex, "Audio processing error");
                            }
                            finally
                            {
                                _audioProcessingSemaphore.Release();
                            }
                        });
                    }
                    
                    await Task.Delay(60); // 与PC版本一致的延迟
                }
            }
            catch (OperationCanceledException)
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    AddSystemMessage("🔌 WebSocket接收循环已取消");
                });
            }
            catch (Exception ex)
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    AddSystemMessage($"❌ WebSocket接收错误: {ex.Message}");
                    _isConnected = false;
                    UpdateConnectionStatus("连接出错");
                });
                _logger.LogError(ex, "Android WebSocket receive loop error");
            }
        }

        private async void OnRecordButtonClicked(object sender, EventArgs e)
        {
            if (!_isConnected)
            {
                await DisplayAlert("错误", "请先连接到小智AI服务", "确定");
                return;
            }

            try
            {
                if (!_isRecording)
                {
                    // 开始录音
                    if (IsAndroidPlatform)
                    {
                        await StartAndroidRecording();
                    }
                    else
                    {
                        await _xiaoZhiAgent.StartRecording("manual");
                    }
                    
                    _isRecording = true;
                    
                    RecordButton.Text = "🔴 停止录音";
                    RecordButton.BackgroundColor = Colors.Red;
                    RecordingStatusLabel.Text = "正在录音...";
                    RecordingStatusLabel.TextColor = Colors.Red;
                    
                    UpdateStatus("开始录音...");
                    _logger.LogInformation("Recording started");
                }
                else
                {
                    // 停止录音
                    if (IsAndroidPlatform)
                    {
                        await StopAndroidRecording();
                    }
                    else
                    {
                        await _xiaoZhiAgent.StopRecording();
                    }
                    
                    _isRecording = false;
                    
                    RecordButton.Text = "🎤 开始录音";
                    RecordButton.BackgroundColor = Colors.Green;
                    RecordingStatusLabel.Text = "准备录音";
                    RecordingStatusLabel.TextColor = Colors.Gray;
                    
                    UpdateStatus("录音结束，处理中...");
                    _logger.LogInformation("Recording stopped");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during recording operation");
                await DisplayAlert("错误", $"录音操作失败：{ex.Message}", "确定");
                
                // 重置录音状态
                _isRecording = false;
                RecordButton.Text = "🎤 开始录音";
                RecordButton.BackgroundColor = Colors.Green;
                RecordingStatusLabel.Text = "准备录音";
                RecordingStatusLabel.TextColor = Colors.Gray;
            }
        }
        
        private async Task StartAndroidRecording()
        {
            try
            {
                AddSystemMessage("🎤 Android录音功能开发中...");
                AddSystemMessage("💡 当前版本请使用文字输入");
                
                // TODO: 实现Android录音功能
                // 这里可以集成MAUI音频服务
                await Task.Delay(100); // 临时占位
            }
            catch (Exception ex)
            {
                AddSystemMessage($"❌ Android录音启动失败: {ex.Message}");
                throw;
            }
        }
        
        private async Task StopAndroidRecording()
        {
            try
            {
                AddSystemMessage("🛑 停止Android录音");
                
                // TODO: 实现Android录音停止功能
                await Task.Delay(100); // 临时占位
            }
            catch (Exception ex)
            {
                AddSystemMessage($"❌ Android录音停止失败: {ex.Message}");
                throw;
            }
        }

        private async void OnSendButtonClicked(object sender, EventArgs e)
        {
            await SendTextMessage();
        }

        private async void OnMessageEntryCompleted(object sender, EventArgs e)
        {
            await SendTextMessage();
        }

        private async Task SendTextMessage()
        {
            if (!_isConnected)
            {
                await DisplayAlert("错误", "请先连接到小智AI服务", "确定");
                return;
            }

            var message = MessageEntry.Text?.Trim();
            if (string.IsNullOrEmpty(message))
                return;

            try
            {
                // 显示用户消息
                AddUserMessage(message);
                
                // 清空输入框
                MessageEntry.Text = string.Empty;
                
                if (IsAndroidPlatform)
                {
                    // Android平台使用专用WebSocket连接
                    await SendMessageViaAndroidWebSocket(message);
                }
                else
                {
                    // 其他平台使用XiaoZhiAgent
                    await _xiaoZhiAgent.SendMessage(message);
                    UpdateStatus("消息已发送，等待回复...");
                    _logger.LogInformation("Text message sent via XiaoZhiAgent: {Message}", message);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending text message");
                await DisplayAlert("错误", $"发送消息失败：{ex.Message}", "确定");
            }
        }
        
        private async Task SendMessageViaAndroidWebSocket(string message)
        {
            try
            {
                if (_androidWebSocket?.State != WebSocketState.Open)
                {
                    AddSystemMessage("❌ WebSocket连接不可用，尝试重新连接");
                    await InitializeAndroidWebSocket();
                    return;
                }
                
                // 使用正确的协议格式 - 与PC版本一致的Listen_Detect格式
                var messageJson = XiaoZhiSharp.Protocols.WebSocketProtocol.Listen_Detect(message);
                var messageBuffer = System.Text.Encoding.UTF8.GetBytes(messageJson);
                
                await _androidWebSocket.SendAsync(
                    new ArraySegment<byte>(messageBuffer), 
                    WebSocketMessageType.Text, 
                    true, 
                    _androidWebSocketCts?.Token ?? CancellationToken.None);
                
                AddSystemMessage($"📤 已发送消息: {message}");
                AddSystemMessage($"📋 协议格式: Listen_Detect");
                UpdateStatus("消息已发送，等待回复...");
                
                _logger.LogInformation("Text message sent via Android WebSocket: {Message}", message);
            }
            catch (Exception ex)
            {
                AddSystemMessage($"❌ Android WebSocket发送失败: {ex.Message}");
                _logger.LogError(ex, "Failed to send message via Android WebSocket");
                throw;
            }
        }

        private void OnXiaoZhiMessage(string message)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                try
                {
                    dynamic? msg = JsonConvert.DeserializeObject<dynamic>(message);
                    if (msg != null)
                    {
                        if (msg.type == "tts" && msg.state == "sentence_start")
                        {
                            // AI 回复消息
                            AddAIMessage(msg.text.ToString());
                            UpdateStatus("收到AI回复");
                        }
                        else if (msg.type == "stt")
                        {
                            // 语音转文字
                            AddUserMessage($"🎤 {msg.text}", true);
                            UpdateStatus("语音识别完成");
                        }
                        else if (msg.type == "iot")
                        {
                            // IoT设备控制消息
                            AddSystemMessage($"IoT: {msg}");
                        }
                    }
                    
                    _logger.LogInformation("Received message: {Message}", message);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing received message");
                    AddSystemMessage($"消息处理错误：{ex.Message}");
                }
            });
        }

        private void OnXiaoZhiAudio(byte[] audio)
        {
            // 🔧 性能优化：减少UI更新频率
            MainThread.BeginInvokeOnMainThread(() =>
            {
                UpdateStatus("语音播放中...");
                // 只记录到日志，不显示系统消息避免UI卡顿
            });
            _logger.LogDebug("Received audio data: {Length} bytes", audio.Length);
        }

        private void AddUserMessage(string text, bool isVoice = false)
        {
            var frame = new Frame
            {
                BackgroundColor = Colors.LightBlue,
                Padding = 10,
                CornerRadius = 10,
                HorizontalOptions = LayoutOptions.End,
                WidthRequest = 250,
                Margin = new Thickness(50, 5, 0, 5)
            };

            var stackLayout = new StackLayout();
            
            var headerLabel = new Label
            {
                Text = isVoice ? "👤 您 (语音)" : "👤 您",
                FontSize = 12,
                FontAttributes = FontAttributes.Bold,
                TextColor = Colors.DarkBlue
            };
            
            var messageLabel = new Label
            {
                Text = text,
                FontSize = 14,
                TextColor = Colors.Black
            };

            var timeLabel = new Label
            {
                Text = DateTime.Now.ToString("HH:mm"),
                FontSize = 10,
                TextColor = Colors.Gray,
                HorizontalOptions = LayoutOptions.End
            };

            stackLayout.Children.Add(headerLabel);
            stackLayout.Children.Add(messageLabel);
            stackLayout.Children.Add(timeLabel);
            frame.Content = stackLayout;

            MessagesContainer.Children.Add(frame);
            ScrollToBottom();
        }

        private void AddAIMessage(string text)
        {
            var frame = new Frame
            {
                BackgroundColor = Colors.LightGreen,
                Padding = 10,
                CornerRadius = 10,
                HorizontalOptions = LayoutOptions.Start,
                WidthRequest = 250,
                Margin = new Thickness(0, 5, 50, 5)
            };

            var stackLayout = new StackLayout();
            
            var headerLabel = new Label
            {
                Text = "🤖 小智AI",
                FontSize = 12,
                FontAttributes = FontAttributes.Bold,
                TextColor = Colors.DarkGreen
            };
            
            var messageLabel = new Label
            {
                Text = text,
                FontSize = 14,
                TextColor = Colors.Black
            };

            var timeLabel = new Label
            {
                Text = DateTime.Now.ToString("HH:mm"),
                FontSize = 10,
                TextColor = Colors.Gray,
                HorizontalOptions = LayoutOptions.End
            };

            stackLayout.Children.Add(headerLabel);
            stackLayout.Children.Add(messageLabel);
            stackLayout.Children.Add(timeLabel);
            frame.Content = stackLayout;

            MessagesContainer.Children.Add(frame);
            ScrollToBottom();
        }

        private void AddSystemMessage(string text)
        {
            var frame = new Frame
            {
                BackgroundColor = Colors.LightYellow,
                Padding = 10,
                CornerRadius = 10,
                HorizontalOptions = LayoutOptions.Center,
                Margin = new Thickness(20, 5, 20, 5)
            };

            var stackLayout = new StackLayout();
            
            var headerLabel = new Label
            {
                Text = "⚙️ 系统",
                FontSize = 12,
                FontAttributes = FontAttributes.Bold,
                TextColor = Colors.Orange
            };
            
            var messageLabel = new Label
            {
                Text = text,
                FontSize = 12,
                TextColor = Colors.Black
            };

            var timeLabel = new Label
            {
                Text = DateTime.Now.ToString("HH:mm"),
                FontSize = 10,
                TextColor = Colors.Gray,
                HorizontalOptions = LayoutOptions.End
            };

            stackLayout.Children.Add(headerLabel);
            stackLayout.Children.Add(messageLabel);
            stackLayout.Children.Add(timeLabel);
            frame.Content = stackLayout;

            MessagesContainer.Children.Add(frame);
            ScrollToBottom();
        }

        private void ScrollToBottom()
        {
            // 🔧 性能优化：限制滚动频率，避免UI卡顿
            var now = DateTime.Now;
            if ((now - _lastScrollTime).TotalMilliseconds < 500) // 最多每500ms滚动一次
            {
                return; // 跳过过于频繁的滚动请求
            }
            
            _lastScrollTime = now;
            
            // 使用简化的滚动逻辑
            MainThread.BeginInvokeOnMainThread(() =>
            {
                try
                {
                    // 直接滚动到底部，不使用异步等待
                    _ = MessageScrollView.ScrollToAsync(0, MessagesContainer.Height, false); // 使用非动画滚动
                }
                catch (Exception ex)
                {
                    // 滚动失败不影响主要功能，只记录日志
                    _logger.LogWarning(ex, "Failed to scroll to bottom");
                }
            });
        }

        private void UpdateStatus(string status)
        {
            if (StatusLabel != null)
            {
                StatusLabel.Text = status;
            }
        }

        private void UpdateConnectionStatus(string status)
        {
            if (ConnectionLabel != null)
            {
                ConnectionLabel.Text = $"连接状态：{status}";
            }
        }

        private async void OnNetworkDiagnosticClicked(object sender, EventArgs e)
        {
            try
            {
                NetworkDiagnosticButton.IsEnabled = false;
                NetworkDiagnosticButton.Text = "🔄 诊断中...";
                
                UpdateStatus("开始网络诊断...");
                AddSystemMessage("🔍 开始Android网络诊断");
                
                var diagnosticResults = await RunNetworkDiagnostic();
                
                // 显示诊断结果
                AddSystemMessage("📊 网络诊断完成");
                foreach (var result in diagnosticResults)
                {
                    AddSystemMessage(result);
                }
                
                UpdateStatus("网络诊断完成");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Network diagnostic failed");
                AddSystemMessage($"❌ 网络诊断失败: {ex.Message}");
                UpdateStatus("网络诊断失败");
            }
            finally
            {
                NetworkDiagnosticButton.IsEnabled = true;
                NetworkDiagnosticButton.Text = "🔍 网络诊断";
            }
        }
        
        private async void OnWebSocketTestClicked(object sender, EventArgs e)
        {
            try
            {
                WebSocketTestButton.IsEnabled = false;
                WebSocketTestButton.Text = "🔄 测试中...";
                
                UpdateStatus("开始WebSocket连接测试...");
                AddSystemMessage("🔌 开始WebSocket连接测试");
                
                var testResults = await RunWebSocketTest();
                
                // 显示测试结果
                AddSystemMessage("📡 WebSocket测试完成");
                foreach (var result in testResults)
                {
                    AddSystemMessage(result);
                }
                
                // 🔧 添加音频测试
                if (IsAndroidPlatform && _mauiAudioService != null)
                {
                    AddSystemMessage("🔔 开始音频播放测试...");
                    await TestAudioPlayback();
                }
                
                UpdateStatus("连接测试完成");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "WebSocket test failed");
                AddSystemMessage($"❌ WebSocket测试失败: {ex.Message}");
                UpdateStatus("WebSocket测试失败");
            }
            finally
            {
                WebSocketTestButton.IsEnabled = true;
                WebSocketTestButton.Text = "🔌 连接测试";
            }
        }
        
        private async Task TestAudioPlayback()
        {
            try
            {
                if (_mauiAudioService == null)
                {
                    AddSystemMessage("❌ 音频服务未初始化");
                    return;
                }
                
                // 检查音频设备状态
                await CheckAudioDeviceStatus();
                
                AddSystemMessage("🔔 测试1: 播放标准测试音频");
                var testAudio = CreateTestAudio();
                await _mauiAudioService.PlayAudioAsync(testAudio);
                AddSystemMessage("✅ 标准测试音频播放完成");
                
                await Task.Delay(2000); // 等待播放完成
                
                AddSystemMessage("🔔 测试2: 播放不同频率音频");
                var highToneAudio = CreateCustomTestAudio(1000.0, 500); // 1000Hz, 0.5秒
                await _mauiAudioService.PlayAudioAsync(highToneAudio);
                AddSystemMessage("✅ 高音测试完成");
                
                await Task.Delay(1000);
                
                AddSystemMessage("🔔 测试3: 播放低音");
                var lowToneAudio = CreateCustomTestAudio(220.0, 500); // 220Hz, 0.5秒
                await _mauiAudioService.PlayAudioAsync(lowToneAudio);
                AddSystemMessage("✅ 低音测试完成");
                
                // 测试音量设置
                AddSystemMessage("🔔 测试4: 设置音量并播放");
                _mauiAudioService.SetVolume(1.0); // 最大音量
                var loudAudio = CreateCustomTestAudio(660.0, 1000); // E5音符, 1秒
                await _mauiAudioService.PlayAudioAsync(loudAudio);
                AddSystemMessage("✅ 音量测试完成");
                
                AddSystemMessage("🎉 音频播放测试全部完成！");
                
            }
            catch (Exception ex)
            {
                AddSystemMessage($"❌ 音频测试失败: {ex.Message}");
                _logger.LogError(ex, "Audio test failed");
            }
        }
        
        private async Task CheckAudioDeviceStatus()
        {
            try
            {
                AddSystemMessage("🔍 检查音频设备状态...");
                
                // 检查设备信息
                AddSystemMessage($"📱 设备型号: {DeviceInfo.Model}");
                AddSystemMessage($"📱 系统版本: {DeviceInfo.VersionString}");
                AddSystemMessage($"📱 平台: {DeviceInfo.Platform}");
                
                // 检查音频权限（如果需要的话）
                var audioPermissionStatus = await Permissions.CheckStatusAsync<Permissions.Microphone>();
                AddSystemMessage($"🎤 麦克风权限: {audioPermissionStatus}");
                
                // 测试音频服务状态
                if (_mauiAudioService != null)
                {
                    AddSystemMessage($"🔊 音频服务已初始化");
                    AddSystemMessage($"📻 是否正在播放: {_mauiAudioService.IsPlaying}");
                    AddSystemMessage($"🎤 是否正在录音: {_mauiAudioService.IsRecording}");
                }
                else
                {
                    AddSystemMessage("❌ 音频服务未初始化");
                }
                
                AddSystemMessage("✅ 设备状态检查完成");
                
            }
            catch (Exception ex)
            {
                AddSystemMessage($"❌ 设备检查失败: {ex.Message}");
                _logger.LogError(ex, "Device check failed");
            }
        }
        
        private byte[] CreateCustomTestAudio(double frequency, int durationMs)
        {
            const int sampleRate = 44100;
            const int channels = 1;
            
            int samples = (sampleRate * durationMs) / 1000;
            int dataSize = samples * channels * 2;
            
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);
            
            // WAV头部
            writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
            writer.Write(36 + dataSize);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));
            writer.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
            writer.Write(16);
            writer.Write((short)1);
            writer.Write((short)channels);
            writer.Write(sampleRate);
            writer.Write(sampleRate * channels * 2);
            writer.Write((short)(channels * 2));
            writer.Write((short)16);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));
            writer.Write(dataSize);
            
            // 生成指定频率的音频数据
            for (int i = 0; i < samples; i++)
            {
                double time = i / (double)sampleRate;
                double amplitude = Math.Sin(2 * Math.PI * frequency * time);
                short sample = (short)(amplitude * short.MaxValue * 0.5);
                writer.Write(sample);
            }
            
            return stream.ToArray();
        }
        
        private async Task<List<string>> RunNetworkDiagnostic()
        {
            var results = new List<string>();
            
            try
            {
                // 1. 检查网络访问
                var networkAccess = Connectivity.NetworkAccess;
                results.Add($"🌐 网络访问: {networkAccess}");
                
                // 2. 检查连接配置
                var profiles = Connectivity.ConnectionProfiles;
                results.Add($"📶 连接类型: {string.Join(", ", profiles)}");
                
                // 3. 检查网络接口
                var interfaces = NetworkInterface.GetAllNetworkInterfaces();
                var activeInterfaces = interfaces.Where(i => i.OperationalStatus == OperationalStatus.Up).ToList();
                results.Add($"🔗 活动接口: {activeInterfaces.Count} 个");
                
                // 4. 测试DNS解析
                try
                {
                    var addresses = await System.Net.Dns.GetHostAddressesAsync("api.tenclass.net");
                    results.Add($"🔍 DNS解析: 成功 ({addresses.Length} 个地址)");
                }
                catch (Exception ex)
                {
                    results.Add($"🔍 DNS解析: 失败 - {ex.Message}");
                }
                
                // 5. 测试HTTP连接
                try
                {
                    using var httpClient = new HttpClient();
                    httpClient.Timeout = TimeSpan.FromSeconds(10);
                    var response = await httpClient.GetAsync("https://api.tenclass.net/");
                    results.Add($"🌍 HTTPS连接: 成功 ({response.StatusCode})");
                }
                catch (Exception ex)
                {
                    results.Add($"🌍 HTTPS连接: 失败 - {ex.Message}");
                }
                
                // 6. 设备信息
                results.Add($"📱 设备平台: {DeviceInfo.Platform}");
                results.Add($"📱 设备型号: {DeviceInfo.Model}");
                results.Add($"📱 系统版本: {DeviceInfo.VersionString}");
                
            }
            catch (Exception ex)
            {
                results.Add($"❌ 诊断过程错误: {ex.Message}");
            }
            
            return results;
        }
        
        private async Task<List<string>> RunWebSocketTest()
        {
            var results = new List<string>();
            
            try
            {
                // 1. 测试WebSocket Echo服务器
                try
                {
                    using var echoWebSocket = new ClientWebSocket();
                    var cts = new CancellationTokenSource(10000);
                    
                    await echoWebSocket.ConnectAsync(new Uri("wss://echo.websocket.org/"), cts.Token);
                    
                    if (echoWebSocket.State == WebSocketState.Open)
                    {
                        results.Add("✅ Echo服务器: 连接成功");
                        
                        // 发送测试消息
                        var testMessage = "Hello Test";
                        var buffer = System.Text.Encoding.UTF8.GetBytes(testMessage);
                        await echoWebSocket.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, cts.Token);
                        
                        // 接收回显
                        var receiveBuffer = new byte[1024];
                        var result = await echoWebSocket.ReceiveAsync(new ArraySegment<byte>(receiveBuffer), cts.Token);
                        var receivedMessage = System.Text.Encoding.UTF8.GetString(receiveBuffer, 0, result.Count);
                        
                        if (receivedMessage == testMessage)
                        {
                            results.Add("✅ Echo测试: 消息收发正常");
                        }
                        else
                        {
                            results.Add("⚠️ Echo测试: 消息内容不匹配");
                        }
                        
                        await echoWebSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Test complete", CancellationToken.None);
                    }
                    else
                    {
                        results.Add($"❌ Echo服务器: 连接失败 ({echoWebSocket.State})");
                    }
                }
                catch (Exception ex)
                {
                    results.Add($"❌ Echo服务器: {ex.Message}");
                }
                
                // 2. 测试API WebSocket服务器
                try
                {
                    using var apiWebSocket = new ClientWebSocket();
                    var cts = new CancellationTokenSource(10000);
                    
                    // 设置请求头
                    var token = "test-token";
                    var deviceId = XiaoZhiSharp.Utils.SystemInfo.GetMacAddress();
                    
                    apiWebSocket.Options.SetRequestHeader("Authorization", $"Bearer {token}");
                    apiWebSocket.Options.SetRequestHeader("Protocol-Version", "1");
                    apiWebSocket.Options.SetRequestHeader("Device-Id", deviceId);
                    apiWebSocket.Options.SetRequestHeader("Client-Id", Guid.NewGuid().ToString());
                    
                    results.Add($"🔑 Token: {token}");
                    results.Add($"📱 Device-Id: {deviceId}");
                    
                    await apiWebSocket.ConnectAsync(new Uri("wss://api.tenclass.net/xiaozhi/v1/"), cts.Token);
                    
                    if (apiWebSocket.State == WebSocketState.Open)
                    {
                        results.Add("✅ API服务器: 连接成功！");
                        results.Add("🎉 WebSocket连接完全正常");
                        
                        await apiWebSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Test complete", CancellationToken.None);
                    }
                    else
                    {
                        results.Add($"❌ API服务器: 连接失败 ({apiWebSocket.State})");
                    }
                }
                catch (Exception ex)
                {
                    results.Add($"❌ API服务器: {ex.Message}");
                    
                    // 分析错误原因
                    if (ex.Message.Contains("401"))
                    {
                        results.Add("💡 可能原因: Token无效或已过期");
                    }
                    else if (ex.Message.Contains("403"))
                    {
                        results.Add("💡 可能原因: 设备未授权");
                    }
                    else if (ex.Message.Contains("timeout"))
                    {
                        results.Add("💡 可能原因: 网络连接超时");
                    }
                    else if (ex.Message.Contains("SSL") || ex.Message.Contains("TLS"))
                    {
                        results.Add("💡 可能原因: SSL证书验证失败");
                    }
                }
                
            }
            catch (Exception ex)
            {
                results.Add($"❌ 测试过程错误: {ex.Message}");
            }
            
            return results;
        }

        private async void OnReconnectClicked(object sender, EventArgs e)
        {
            try
            {
                ReconnectButton.IsEnabled = false;
                ReconnectButton.Text = "🔄 连接中...";
                
                AddSystemMessage("🔄 开始重新连接服务");
                
                // 先停止现有服务
                try
                {
                    if (IsAndroidPlatform)
                    {
                        // 清理Android WebSocket连接
                        if (_androidWebSocket != null)
                        {
                            _androidWebSocketCts?.Cancel();
                            if (_androidWebSocket.State == WebSocketState.Open)
                            {
                                await _androidWebSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Reconnecting", CancellationToken.None);
                            }
                            _androidWebSocket.Dispose();
                            _androidWebSocket = null;
                            
                            // 关键：销毁旧的解码器
                            _opusDecoder?.Dispose();
                            _opusDecoder = null;
                            
                            AddSystemMessage("🛑 Android WebSocket已断开");
                        }
                    }
                    else
                    {
                        // 清理XiaoZhiAgent连接
                        if (_xiaoZhiAgent != null)
                        {
                            _xiaoZhiAgent.OnMessageEvent -= OnXiaoZhiMessage;
                            _xiaoZhiAgent.OnAudioEvent -= OnXiaoZhiAudio;
                            _xiaoZhiAgent.Stop();
                            AddSystemMessage("🛑 XiaoZhiAgent服务已停止");
                        }
                    }
                }
                catch (Exception stopEx)
                {
                    AddSystemMessage($"⚠️ 停止旧服务时出错: {stopEx.Message}");
                }
                
                // 等待一秒钟
                await Task.Delay(1000);
                
                // 重置连接状态
                _isConnected = false;
                UpdateConnectionStatus("重新连接中");
                
                // 重新初始化
                InitializeXiaoZhiAgent();
                
                // 等待连接结果
                await Task.Delay(3000);
                
                if (_isConnected)
                {
                    AddSystemMessage("🎉 重新连接成功！");
                }
                else
                {
                    AddSystemMessage("❌ 重新连接失败，请检查错误信息");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to reconnect");
                AddSystemMessage($"❌ 重连过程出错: {ex.Message}");
            }
            finally
            {
                ReconnectButton.IsEnabled = true;
                ReconnectButton.Text = "🔄 重新连接";
            }
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            
            // 清理资源
            try
            {
                // 🔧 停止音频缓冲播放
                _audioPlaybackCts?.Cancel();
                _isAudioPlaying = false;
                
                // 🔧 清理连续音频缓冲区
                lock (_audioBufferLock)
                {
                    _continuousAudioBuffer.Clear();
                }
                
                if (IsAndroidPlatform)
                {
                    // 清理Android WebSocket资源
                    _androidWebSocketCts?.Cancel();
                    _androidWebSocket?.Dispose();
                    
                    // 关键：销毁解码器
                    _opusDecoder?.Dispose();
                    _opusDecoder = null;
                }
                else
                {
                    // 清理XiaoZhiAgent资源
                    if (_xiaoZhiAgent != null)
                    {
                        _xiaoZhiAgent.OnMessageEvent -= OnXiaoZhiMessage;
                        _xiaoZhiAgent.OnAudioEvent -= OnXiaoZhiAudio;
                        _xiaoZhiAgent.Stop();
                    }
                }
                
                // 清理性能优化资源
                _audioProcessingSemaphore?.Dispose();
                _audioPlaybackSemaphore?.Dispose();
                
                // 清理旧的音频缓冲队列（如果还存在）
                while (_audioBufferQueue.TryDequeue(out _)) { }
                
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during cleanup");
            }
        }

        private async Task PlayAudioDataAsync(byte[] audioData)
        {
            try
            {
                if (_mauiAudioService == null) return;

                // 关键修复：在一句话开始时，重置解码器
                lock (_speechBufferLock)
                {
                    if (!_isSpeechActive)
                    {
                        _opusDecoder?.Dispose();
                        _opusDecoder = new OpusDecoder(24000, 1);
                        MainThread.BeginInvokeOnMainThread(() => AddSystemMessage("🎤 新语音开始 (解码器已重置)..."));
                        _isSpeechActive = true;
                    }
                }

                var pcmData = DecodeOpusToShortArray(audioData);
                if (pcmData == null || pcmData.Length == 0) return;

                // 收集音频包到语音缓冲区
                lock (_speechBufferLock)
                {
                    _speechBuffer.AddRange(pcmData);
                    _lastAudioPacketTime = DateTime.Now;
                }

                // 启动或重置语音超时检测
                _speechTimeoutCts?.Cancel();
                _speechTimeoutCts = new CancellationTokenSource();
                _ = Task.Run(() => MonitorSpeechTimeout(_speechTimeoutCts.Token));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in PlayAudioDataAsync");
            }
        }

        private async Task MonitorSpeechTimeout(CancellationToken cancellationToken)
        {
            try
            {
                await Task.Delay(SPEECH_TIMEOUT_MS, cancellationToken);
                await PlayCompleteSpeechAsync();
            }
            catch (OperationCanceledException) { /* Normal cancellation */ }
        }

        private async Task PlayCompleteSpeechAsync()
        {
            if (!await _speechPlaybackSemaphore.WaitAsync(100)) return;

            short[] speechData;
            try
            {
                lock (_speechBufferLock)
                {
                    if (_speechBuffer.Count == 0) return;
                    speechData = _speechBuffer.ToArray();
                    _speechBuffer.Clear();
                    _isSpeechActive = false; // 允许下一句话重置解码器
                }

                double durationMs = speechData.Length / 24.0;
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    AddSystemMessage($"🎵 开始播放完整语音: {durationMs:F0}ms");
                    UpdateStatus("正在播放AI语音...");
                });

                var wavData = CreateWavFromPcm(speechData, speechData.Length, 24000, 1);
                await _mauiAudioService.PlayAudioAsync(wavData);

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    AddSystemMessage($"✅ 语音播放完成");
                    UpdateStatus("语音播放完成");
                });
            }
            catch (Exception ex)
            {
                MainThread.BeginInvokeOnMainThread(() => AddSystemMessage($"❌ 完整语音播放失败: {ex.Message}"));
                _logger.LogError(ex, "Error playing complete speech");
            }
            finally
            {
                _speechPlaybackSemaphore.Release();
            }
        }
        
        private short[]? DecodeOpusToShortArray(byte[] opusData)
        {
            if (_opusDecoder == null)
            {
                MainThread.BeginInvokeOnMainThread(() => AddSystemMessage("❌ 解码器未初始化"));
                return null;
            }
            
            try
            {
                // 🔧 与PC版本完全一致的处理方式
                const int FrameSize = 1440; // 60ms @ 24kHz
                
                // 关键修复：使用共享的解码器实例
                short[] pcmData = new short[FrameSize * 10];
                int decodedSamples = _opusDecoder.Decode(opusData, opusData.Length, pcmData, FrameSize * 10, false);
                
                if (decodedSamples > 0)
                {
                    // 只返回有效的采样数据
                    short[] validPcmData = new short[decodedSamples];
                    Array.Copy(pcmData, 0, validPcmData, 0, decodedSamples);
                    return validPcmData;
                }
                
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error decoding Opus to short array");
                MainThread.BeginInvokeOnMainThread(() => AddSystemMessage($"❌ Opus解码失败: {ex.Message}"));
                return null;
            }
        }
        
        private byte[]? ExtractOpusPayload(byte[] rtpData)
        {
            try
            {
                // 简化的RTP头解析 (最少12字节)
                if (rtpData.Length <= 12)
                    return null;
                
                // RTP头格式:
                // 0: V(2) P(1) X(1) CC(4)
                // 1: M(1) PT(7)
                // 2-3: Sequence Number
                // 4-7: Timestamp
                // 8-11: SSRC
                // 12+: 载荷数据
                
                byte firstByte = rtpData[0];
                int version = (firstByte >> 6) & 0x03;
                int csrcCount = firstByte & 0x0F;
                
                if (version != 2) // RTP版本应该是2
                    return null;
                
                int headerLength = 12 + (csrcCount * 4);
                if (rtpData.Length <= headerLength)
                    return null;
                
                // 提取载荷
                byte[] payload = new byte[rtpData.Length - headerLength];
                Array.Copy(rtpData, headerLength, payload, 0, payload.Length);
                
                return payload;
            }
            catch
            {
                return null;
            }
        }
        
        private byte[] CreateSilentWav(int durationMs)
        {
            // 创建一个静音的WAV文件用于测试
            const int sampleRate = 24000;
            const int channels = 1;
            const int bitsPerSample = 16;
            
            int samples = (sampleRate * durationMs) / 1000;
            int dataSize = samples * channels * (bitsPerSample / 8);
            
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);
            
            // WAV文件头
            writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
            writer.Write(36 + dataSize);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));
            writer.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
            writer.Write(16); // fmt chunk size
            writer.Write((short)1); // PCM format
            writer.Write((short)channels);
            writer.Write(sampleRate);
            writer.Write(sampleRate * channels * (bitsPerSample / 8));
            writer.Write((short)(channels * (bitsPerSample / 8)));
            writer.Write((short)bitsPerSample);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));
            writer.Write(dataSize);
            
            // 静音数据 (全零)
            for (int i = 0; i < samples; i++)
            {
                writer.Write((short)0);
            }
            
            return stream.ToArray();
        }
        
        private byte[] CreateTestAudio()
        {
            // 创建一个标准的WAV格式测试音频，确保在Android设备上能播放
            const int sampleRate = 44100;     // 使用标准采样率
            const int channels = 1;           // 单声道
            const int durationMs = 1000;      // 1秒，足够听清
            const double frequency = 880.0;   // A5音符，比较清晰
            
            int samples = (sampleRate * durationMs) / 1000;
            int dataSize = samples * channels * 2; // 16位PCM
            
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);
            
            // 严格按照WAV格式标准写入头部
            writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));        // ChunkID
            writer.Write(36 + dataSize);                                        // ChunkSize
            writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));        // Format
            
            // fmt子块
            writer.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));        // Subchunk1ID
            writer.Write(16);                                                   // Subchunk1Size (PCM = 16)
            writer.Write((short)1);                                            // AudioFormat (PCM = 1)
            writer.Write((short)channels);                                     // NumChannels
            writer.Write(sampleRate);                                          // SampleRate
            writer.Write(sampleRate * channels * 2);                          // ByteRate
            writer.Write((short)(channels * 2));                              // BlockAlign
            writer.Write((short)16);                                          // BitsPerSample
            
            // data子块
            writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));        // Subchunk2ID
            writer.Write(dataSize);                                            // Subchunk2Size
            
            // 生成更明显的音频数据
            for (int i = 0; i < samples; i++)
            {
                double time = i / (double)sampleRate;
                // 使用包络让声音更自然，渐强渐弱
                double envelope = 1.0;
                if (time < 0.1) // 前0.1秒渐强
                    envelope = time / 0.1;
                else if (time > 0.9) // 后0.1秒渐弱
                    envelope = (1.0 - time) / 0.1;
                
                double amplitude = Math.Sin(2 * Math.PI * frequency * time) * envelope;
                short sample = (short)(amplitude * short.MaxValue * 0.6); // 60%音量，更明显
                writer.Write(sample);
            }
            
            var result = stream.ToArray();
            
            // 添加调试信息
            MainThread.BeginInvokeOnMainThread(() =>
            {
                AddSystemMessage($"🔔 生成测试音频: {result.Length}字节, {frequency}Hz, {durationMs}ms");
            });
            
            return result;
        }
        
        private byte[] CreateWavFromPcm(short[] pcmData, int samples, int sampleRate, int channels)
        {
            // 🔧 简化WAV生成：减少UI更新提高性能
            const int bitsPerSample = 16;
            int dataSize = samples * channels * (bitsPerSample / 8);
            
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);
            
            // 严格按照WAV格式标准写入头部
            writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));        // ChunkID
            writer.Write(36 + dataSize);                                        // ChunkSize
            writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));        // Format
            
            // fmt子块
            writer.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));        // Subchunk1ID
            writer.Write(16);                                                   // Subchunk1Size (PCM = 16)
            writer.Write((short)1);                                            // AudioFormat (PCM = 1)
            writer.Write((short)channels);                                     // NumChannels
            writer.Write(sampleRate);                                          // SampleRate
            writer.Write(sampleRate * channels * (bitsPerSample / 8));        // ByteRate
            writer.Write((short)(channels * (bitsPerSample / 8)));            // BlockAlign
            writer.Write((short)bitsPerSample);                               // BitsPerSample
            
            // data子块
            writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));        // Subchunk2ID
            writer.Write(dataSize);                                            // Subchunk2Size
            
            // 写入PCM数据
            for (int i = 0; i < samples; i++)
            {
                writer.Write(pcmData[i]);
            }
            
            return stream.ToArray();
        }
    }
}
