using System;
using System.IO;
using System.Threading.Tasks;
using Plugin.Maui.Audio;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.ApplicationModel;

namespace XiaoZhiSharpMAUI.Services
{
    public class MauiAudioService : IMauiAudioService
    {
        private readonly IAudioManager _audioManager;
        private readonly ILogger<MauiAudioService> _logger;
        
        private IAudioRecorder? _audioRecorder;
        private IAudioPlayer? _audioPlayer;
        private bool _disposed;

        public bool IsRecording => _audioRecorder?.IsRecording ?? false;
        public bool IsPlaying => _audioPlayer?.IsPlaying ?? false;

        public event EventHandler<byte[]>? RecordDataAvailable;

        public MauiAudioService(IAudioManager audioManager, ILogger<MauiAudioService> logger)
        {
            _audioManager = audioManager ?? throw new ArgumentNullException(nameof(audioManager));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task StartRecordingAsync()
        {
            try
            {
                if (IsRecording)
                {
                    _logger.LogWarning("Recording is already in progress");
                    return;
                }

                // 使用MAUI内置的权限系统检查麦克风权限
                var microphoneStatus = await Permissions.CheckStatusAsync<Permissions.Microphone>();
                if (microphoneStatus != PermissionStatus.Granted)
                {
                    microphoneStatus = await Permissions.RequestAsync<Permissions.Microphone>();
                    if (microphoneStatus != PermissionStatus.Granted)
                    {
                        throw new UnauthorizedAccessException("麦克风权限被拒绝");
                    }
                }

                _audioRecorder = _audioManager.CreateRecorder();
                await _audioRecorder.StartAsync();
                
                _logger.LogInformation("Recording started successfully");

                // 启动数据收集任务
                _ = Task.Run(async () => await CollectRecordingDataAsync());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start recording");
                throw;
            }
        }

        public async Task StopRecordingAsync()
        {
            try
            {
                if (_audioRecorder != null && IsRecording)
                {
                    var recordingResult = await _audioRecorder.StopAsync();
                    _logger.LogInformation("Recording stopped successfully");
                    
                    // 处理录音结果，转换为字节数组并触发事件
                    if (recordingResult != null)
                    {
                        await ProcessRecordingResult(recordingResult);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to stop recording");
                throw;
            }
        }

        public async Task PlayAudioAsync(byte[] audioData)
        {
            try
            {
                using var stream = new MemoryStream(audioData);
                await PlayAudioStreamAsync(stream);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to play audio data");
                throw;
            }
        }

        public async Task PlayAudioStreamAsync(Stream audioStream)
        {
            try
            {
                if (IsPlaying)
                {
                    await StopPlayingAsync();
                }

                _audioPlayer = _audioManager.CreatePlayer(audioStream);
                _audioPlayer.PlaybackEnded += OnPlaybackEnded;
                
                _audioPlayer.Play();
                _logger.LogInformation("Audio playback started");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to play audio stream");
                throw;
            }
        }

        public async Task StopPlayingAsync()
        {
            try
            {
                if (_audioPlayer != null && IsPlaying)
                {
                    _audioPlayer.Stop();
                    _logger.LogInformation("Audio playback stopped");
                }
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to stop audio playback");
                throw;
            }
        }

        public void SetVolume(double volume)
        {
            try
            {
                if (_audioPlayer != null)
                {
                    _audioPlayer.Volume = Math.Clamp(volume, 0.0, 1.0);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to set volume");
                throw;
            }
        }

        private async Task CollectRecordingDataAsync()
        {
            while (IsRecording && _audioRecorder != null)
            {
                try
                {
                    // Plugin.Maui.Audio 的录音数据处理方式
                    // 当前版本可能不支持实时数据流，这里暂时模拟
                    await Task.Delay(100);
                    
                    // TODO: 实现实际的录音数据收集
                    // 这取决于 Plugin.Maui.Audio 的具体实现
                    // 目前该插件可能不支持实时音频流
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error collecting recording data");
                    break;
                }
            }
        }

        private async Task ProcessRecordingResult(IAudioSource recordingResult)
        {
            try
            {
                // 将录音结果转换为字节数组
                using var stream = recordingResult.GetAudioStream();
                using var memoryStream = new MemoryStream();
                await stream.CopyToAsync(memoryStream);
                var audioData = memoryStream.ToArray();
                
                // 触发录音数据可用事件
                RecordDataAvailable?.Invoke(this, audioData);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing recording result");
            }
        }

        private void OnPlaybackEnded(object? sender, EventArgs e)
        {
            _logger.LogInformation("Audio playback completed");
            _audioPlayer?.Dispose();
            _audioPlayer = null;
        }

        public void Dispose()
        {
            if (_disposed) return;

            try
            {
                // 停止录音和播放
                if (_audioRecorder?.IsRecording == true)
                {
                    _audioRecorder.StopAsync().GetAwaiter().GetResult();
                }
                
                if (_audioPlayer?.IsPlaying == true)
                {
                    _audioPlayer.Stop();
                }

                // 清理资源
                _audioRecorder = null;
                _audioPlayer?.Dispose();
                _audioPlayer = null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disposing audio service");
            }
            finally
            {
                _disposed = true;
                _logger.LogInformation("MauiAudioService disposed");
            }
        }
    }
} 