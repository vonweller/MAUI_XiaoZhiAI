using XiaoZhiSharp;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace XiaoZhiSharpMAUI
{
    public partial class MainPage : ContentPage
    {
        private readonly XiaoZhiAgent _xiaoZhiAgent;
        private readonly ILogger<MainPage> _logger;
        private bool _isRecording = false;
        private bool _isConnected = false;

        public MainPage(XiaoZhiAgent xiaoZhiAgent, ILogger<MainPage> logger)
        {
            InitializeComponent();
            _xiaoZhiAgent = xiaoZhiAgent;
            _logger = logger;
            
            InitializeXiaoZhiAgent();
        }

        private void InitializeXiaoZhiAgent()
        {
            try
            {
                // 订阅消息事件
                _xiaoZhiAgent.OnMessageEvent += OnXiaoZhiMessage;
                _xiaoZhiAgent.OnAudioEvent += OnXiaoZhiAudio;
                
                // 启动小智服务
                _xiaoZhiAgent.Start();
                
                _isConnected = true;
                UpdateStatus("小智AI助手已启动");
                UpdateConnectionStatus("已连接");
                _logger.LogInformation("XiaoZhi Agent started successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize XiaoZhi Agent");
                UpdateStatus("初始化失败，请重试");
                UpdateConnectionStatus("连接失败");
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
                    await _xiaoZhiAgent.StartRecording("manual");
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
                    await _xiaoZhiAgent.StopRecording();
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
                
                // 发送消息
                await _xiaoZhiAgent.SendMessage(message);
                
                UpdateStatus("消息已发送，等待回复...");
                _logger.LogInformation("Text message sent: {Message}", message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending text message");
                await DisplayAlert("错误", $"发送消息失败：{ex.Message}", "确定");
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
            MainThread.BeginInvokeOnMainThread(() =>
            {
                UpdateStatus("播放语音中...");
                _logger.LogDebug("Received audio data: {Length} bytes", audio.Length);
            });
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
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                await Task.Delay(100); // 等待UI更新
                await MessageScrollView.ScrollToAsync(0, MessagesContainer.Height, true);
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

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            
            // 清理资源
            if (_xiaoZhiAgent != null)
            {
                _xiaoZhiAgent.OnMessageEvent -= OnXiaoZhiMessage;
                _xiaoZhiAgent.OnAudioEvent -= OnXiaoZhiAudio;
                _xiaoZhiAgent.Stop();
            }
        }
    }
}
