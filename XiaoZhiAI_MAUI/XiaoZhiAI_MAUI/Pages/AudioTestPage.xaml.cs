using Microsoft.Maui.Controls;
using System;
using System.Threading.Tasks;
using XiaoZhiAI_MAUI.Services;
using System.Text;
using System.Diagnostics;

namespace XiaoZhiAI_MAUI.Pages;

public partial class AudioTestPage : ContentPage
{
    private readonly IAudioService _audioService;
    private readonly IPlatformAudioService _platformAudio;
    private readonly ILogService _logService;
    private bool _isRecording = false;
    private readonly StringBuilder _logBuilder = new();
    private int _audioDataCount = 0;
    private int _encodedDataCount = 0;

    public AudioTestPage()
    {
        InitializeComponent();
        
        // 获取服务
        _audioService = IPlatformApplication.Current.Services.GetService<IAudioService>();
        _platformAudio = IPlatformApplication.Current.Services.GetService<IPlatformAudioService>();
        _logService = IPlatformApplication.Current.Services.GetService<ILogService>();
        
        // 订阅音频事件
        if (_audioService != null)
        {
            _audioService.AudioDataReady += OnAudioDataReady;
            _audioService.RecordingStatusChanged += OnRecordingStatusChanged;
            _audioService.PlaybackStatusChanged += OnPlaybackStatusChanged;
            _audioService.VoiceActivityDetected += OnVoiceActivityDetected;
            _audioService.AudioDataReceived += OnAudioDataReceived;
        }
        
        // 订阅全局日志服务
        if (_logService != null)
        {
            _logService.LogMessageReceived += OnGlobalLogMessageReceived;
            LogMessage("✅ 全局日志服务已订阅");
        }
        else
        {
            LogMessage("❌ 全局日志服务未找到");
        }
        
        LogMessage("音频测试页面已初始化");
        
        CheckServiceStatus();
        RefreshDeviceInfo();
    }

    private async void CheckServiceStatus()
    {
        try
        {
            if (_audioService == null)
            {
                UpdateServiceStatus("❌ 音频服务未找到");
                return;
            }

            UpdateServiceStatus("🔄 正在初始化音频服务...");
            await _audioService.InitializeAsync();
            UpdateServiceStatus("✅ 音频服务已就绪");
            
            LogMessage($"音频服务类型: {_audioService.GetType().Name}");
            LogMessage($"平台音频服务类型: {_platformAudio?.GetType().Name ?? "未找到"}");
        }
        catch (Exception ex)
        {
            UpdateServiceStatus($"❌ 初始化失败: {ex.Message}");
            LogMessage($"详细错误: {ex}");
        }
    }

    private async void OnRecordClicked(object sender, EventArgs e)
    {
        try
        {
            if (_isRecording)
            {
                // 停止录音
                LogMessage("用户点击停止录音");
                await _audioService.StopRecordingAsync();
            }
            else
            {
                // 开始录音
                LogMessage("用户点击开始录音");
                _audioDataCount = 0;
                _encodedDataCount = 0;
                await _audioService.StartRecordingAsync();
            }
        }
        catch (Exception ex)
        {
            LogMessage($"录音操作失败: {ex.Message}");
            UpdateRecordStatus("❌ 录音操作失败");
        }
    }

    private async void OnPlayTestClicked(object sender, EventArgs e)
    {
        try
        {
            LogMessage("开始播放测试音调");
            UpdatePlayStatus("🔊 正在播放测试音调...");
            
            // 生成440Hz测试音调 (1秒)
            var sampleRate = 24000;
            var duration = 1.0; // 1秒
            var frequency = 440; // A音
            var samples = (int)(sampleRate * duration);
            
            var testData = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                testData[i] = (float)(0.3 * Math.Sin(2 * Math.PI * frequency * i / sampleRate));
            }
            
            if (_platformAudio != null)
            {
                await _platformAudio.PlayAudioAsync(testData);
                UpdatePlayStatus("✅ 测试音调播放完成");
                LogMessage($"播放了 {samples} 采样的测试音调");
            }
            else
            {
                UpdatePlayStatus("❌ 平台音频服务不可用");
            }
        }
        catch (Exception ex)
        {
            UpdatePlayStatus($"❌ 播放失败: {ex.Message}");
            LogMessage($"播放测试音调失败: {ex}");
        }
    }

    private async void OnOpusTestClicked(object sender, EventArgs e)
    {
        try
        {
            LogMessage("开始Opus编解码测试");
            UpdateOpusStatus("🔄 正在测试Opus编解码...");
            
            // 创建测试音频数据 (16kHz, 960采样 = 60ms)
            var frameSize = 960;
            var testData = new float[frameSize];
            for (int i = 0; i < frameSize; i++)
            {
                testData[i] = (float)(0.1 * Math.Sin(2 * Math.PI * 440 * i / 16000));
            }
            
            // 测试编码
            var codec = new OpusCodecNative(16000, 1, frameSize);
            var encodedData = codec.Encode(testData);
            
            if (encodedData != null && encodedData.Length > 0)
            {
                LogMessage($"编码成功: {testData.Length} 采样 → {encodedData.Length} 字节");
                
                // 测试解码
                var decodedData = codec.Decode(encodedData);
                if (decodedData != null && decodedData.Length > 0)
                {
                    LogMessage($"解码成功: {encodedData.Length} 字节 → {decodedData.Length} 采样");
                    UpdateOpusStatus("✅ Opus编解码测试成功");
                }
                else
                {
                    UpdateOpusStatus("❌ Opus解码失败");
                }
            }
            else
            {
                UpdateOpusStatus("❌ Opus编码失败");
            }
            
            codec.Dispose();
        }
        catch (Exception ex)
        {
            UpdateOpusStatus($"❌ Opus测试失败: {ex.Message}");
            LogMessage($"Opus测试详细错误: {ex}");
        }
    }

    private async void OnInitServiceClicked(object sender, EventArgs e)
    {
        try
        {
            LogMessage("用户请求重新初始化音频服务");
            UpdateServiceStatus("🔄 正在重新初始化...");
            
            if (_audioService != null)
            {
                await _audioService.InitializeAsync();
                UpdateServiceStatus("✅ 重新初始化完成");
                LogMessage("音频服务重新初始化成功");
            }
            else
            {
                UpdateServiceStatus("❌ 音频服务不可用");
            }
        }
        catch (Exception ex)
        {
            UpdateServiceStatus($"❌ 重新初始化失败: {ex.Message}");
            LogMessage($"重新初始化失败: {ex}");
        }
    }

    private void OnClearLogClicked(object sender, EventArgs e)
    {
        _logBuilder.Clear();
        MainThread.BeginInvokeOnMainThread(() =>
        {
            LogLabel.Text = "日志已清除";
        });
    }

    private void OnRefreshDeviceInfoClicked(object sender, EventArgs e)
    {
        LogMessage("用户请求刷新设备信息");
        RefreshDeviceInfo();
    }

    private void OnClearMacCacheClicked(object sender, EventArgs e)
    {
        try
        {
            LogMessage("用户请求清除MAC地址缓存");
            XiaoZhiAI_MAUI.Utils.DeviceInfoHelper.ClearCachedMacAddress();
            LogMessage("MAC地址缓存已清除");
            RefreshDeviceInfo();
        }
        catch (Exception ex)
        {
            LogMessage($"清除MAC地址缓存失败: {ex.Message}");
        }
    }

    private void RefreshDeviceInfo()
    {
        try
        {
            var deviceInfo = Microsoft.Maui.Devices.DeviceInfo.Current;
            var macAddress = XiaoZhiAI_MAUI.Utils.DeviceInfoHelper.GetDeviceMacAddress();
            var clientId = XiaoZhiAI_MAUI.Utils.DeviceInfoHelper.GetClientId();
            
            var info = $"设备名称: {deviceInfo.Name}\n" +
                      $"平台: {deviceInfo.Platform}\n" +
                      $"制造商: {deviceInfo.Manufacturer}\n" +
                      $"型号: {deviceInfo.Model}\n" +
                      $"版本: {deviceInfo.VersionString}\n" +
                      $"MAC地址: {macAddress}\n" +
                      $"客户端ID: {clientId}";
            
            MainThread.BeginInvokeOnMainThread(() =>
            {
                DeviceInfoLabel.Text = info;
            });
            
            LogMessage($"设备MAC地址: {macAddress}");
            LogMessage($"客户端ID: {clientId}");
        }
        catch (Exception ex)
        {
            LogMessage($"获取设备信息失败: {ex.Message}");
            MainThread.BeginInvokeOnMainThread(() =>
            {
                DeviceInfoLabel.Text = $"设备信息获取失败: {ex.Message}";
            });
        }
    }

    // 音频事件处理
    private void OnAudioDataReady(object sender, byte[] audioData)
    {
        _encodedDataCount++;
        LogMessage($"编码数据就绪 #{_encodedDataCount}: {audioData.Length} 字节");
    }

    private void OnRecordingStatusChanged(object sender, bool isRecording)
    {
        _isRecording = isRecording;
        var status = isRecording ? "🔴 正在录音..." : "⏹️ 录音已停止";
        UpdateRecordStatus(status);
        LogMessage($"录音状态变更: {(isRecording ? "开始" : "停止")}");
        
        MainThread.BeginInvokeOnMainThread(() =>
        {
            RecordButton.Text = isRecording ? "停止录音" : "开始录音";
            RecordButton.BackgroundColor = isRecording ? Colors.Red : Colors.Green;
        });
    }

    private void OnPlaybackStatusChanged(object sender, bool isPlaying)
    {
        var status = isPlaying ? "🔊 正在播放..." : "⏹️ 播放已停止";
        UpdatePlayStatus(status);
        LogMessage($"播放状态变更: {(isPlaying ? "开始" : "停止")}");
    }

    private void OnVoiceActivityDetected(object sender, bool hasVoice)
    {
        var status = hasVoice ? "🎤 检测到语音" : "🔇 语音结束";
        LogMessage($"VAD: {status}");
    }

    private void OnAudioDataReceived(object sender, float[] audioData)
    {
        _audioDataCount++;
        if (_audioDataCount % 10 == 0) // 每10帧记录一次，避免日志过多
        {
            LogMessage($"接收音频数据 #{_audioDataCount}: {audioData.Length} 采样");
        }
        
        MainThread.BeginInvokeOnMainThread(() =>
        {
            AudioDataLabel.Text = $"音频数据: 已接收 {_audioDataCount} 帧";
        });
    }

    private void OnGlobalLogMessageReceived(object sender, string logMessage)
    {
        // 接收来自其他页面的日志消息
        var globalEntry = $"[全局] {logMessage}";
        _logBuilder.AppendLine(globalEntry);
        
        // 同时输出到调试日志
        Debug.WriteLine($"[AudioTestPage] 收到全局日志: {globalEntry}");
        
        MainThread.BeginInvokeOnMainThread(() =>
        {
            try
            {
                if (LogLabel != null)
                {
                    LogLabel.Text = _logBuilder.ToString();
                    Debug.WriteLine($"[AudioTestPage] UI已更新，日志总长度: {_logBuilder.Length}");
                }
                else
                {
                    Debug.WriteLine("❌ [AudioTestPage] LogLabel为null");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ [AudioTestPage] UI更新失败: {ex.Message}");
            }
        });
    }

    // UI更新方法
    private void UpdateServiceStatus(string status)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            ServiceStatusLabel.Text = $"服务状态: {status}";
        });
    }

    private void UpdateRecordStatus(string status)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            RecordStatusLabel.Text = $"录音状态: {status}";
        });
    }

    private void UpdatePlayStatus(string status)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            PlayStatusLabel.Text = $"播放状态: {status}";
        });
    }

    private void UpdateOpusStatus(string status)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            OpusStatusLabel.Text = $"Opus状态: {status}";
        });
    }

    private void LogMessage(string message)
    {
        var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
        var logEntry = $"[{timestamp}] {message}";
        
        _logBuilder.AppendLine(logEntry);
        Debug.WriteLine($"[AudioTestPage] {logEntry}");
        
        MainThread.BeginInvokeOnMainThread(() =>
        {
            LogLabel.Text = _logBuilder.ToString();
        });
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        
        // 清理事件订阅
        if (_audioService != null)
        {
            _audioService.AudioDataReady -= OnAudioDataReady;
            _audioService.RecordingStatusChanged -= OnRecordingStatusChanged;
            _audioService.PlaybackStatusChanged -= OnPlaybackStatusChanged;
            _audioService.VoiceActivityDetected -= OnVoiceActivityDetected;
            _audioService.AudioDataReceived -= OnAudioDataReceived;
        }
        
        if (_logService != null)
        {
            _logService.LogMessageReceived -= OnGlobalLogMessageReceived;
        }
    }
}