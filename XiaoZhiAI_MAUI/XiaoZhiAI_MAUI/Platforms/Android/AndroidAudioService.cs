using Android;
using Android.Content;
using Android.Media;
using AndroidX.Core.App;
using AndroidX.Core.Content;
using Microsoft.Maui.Controls;
using System.Diagnostics;
using XiaoZhiAI_MAUI.Services;
using AudioSource = Android.Media.AudioSource;

namespace XiaoZhiAI_MAUI.Platforms.Android
{
    public class AndroidAudioService : IPlatformAudioService
    {
        private AudioTrack? _audioTrack;
        private bool _isPlaying = false;
        
        public event EventHandler<float[]> AudioDataReceived;

        public async Task InitializeAsync()
        {
            try
            {
                Debug.WriteLine("=== 初始化Android音频服务 ===");
                
                // 检查权限
                await CheckPermissions();
                
                // 获取Context
                var context = Platform.CurrentActivity ?? Microsoft.Maui.ApplicationModel.Platform.CurrentActivity;
                if (context == null)
                {
                    Debug.WriteLine("❌ 无法获取Context");
                    return;
                }
                
                // 只初始化播放组件，录音组件在需要时创建
                await InitializeAudioTrack();
                
                Debug.WriteLine("✅ Android音频服务初始化完成 - 等待用户手动触发录音");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ 音频服务初始化失败: {ex.Message}");
                throw;
            }
        }

        private async Task CheckPermissions()
        {
            var context = Platform.CurrentActivity ?? Microsoft.Maui.ApplicationModel.Platform.CurrentActivity;
            if (context == null)
            {
                Debug.WriteLine("❌ 无法获取Context");
                return;
            }

            var recordPermission = ContextCompat.CheckSelfPermission(context, Manifest.Permission.RecordAudio);
            Debug.WriteLine($"🔍 录音权限状态: {recordPermission}");
            
            if (recordPermission != AndroidX.Core.Content.PermissionChecker.PermissionGranted)
            {
                Debug.WriteLine("📋 请求录音权限");
                ActivityCompat.RequestPermissions(Platform.CurrentActivity, 
                    new string[] { Manifest.Permission.RecordAudio }, 100);
                await Task.Delay(1000);
            }
        }

        // 实现真正的录音功能（不自动播放录音内容）
        private bool _isRecording = false;
        private CancellationTokenSource? _recordingCancellation;

        // IPlatformAudioService接口实现 - 实时音频流（参考Unity）
        public async Task StartRecordingAsync()
        {
            if (_isRecording)
            {
                Debug.WriteLine("⚠️ 已在录音中，忽略重复启动");
                return;
            }

            try
            {
                Debug.WriteLine("🎤 Android开始实时录音...");
                
                _recordingCancellation?.Cancel();
                _recordingCancellation = new CancellationTokenSource();
                _isRecording = true;

                // 启动实时音频捕获任务（类似Unity的SendAudioCoroutine）
                _ = Task.Run(() => RealTimeAudioCapture(_recordingCancellation.Token));
                
                Debug.WriteLine("✅ Android实时录音已启动");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ Android录音启动失败: {ex.Message}");
                _isRecording = false;
            }
        }

        public async Task StopRecordingAsync()
        {
            if (!_isRecording)
            {
                Debug.WriteLine("⚠️ 当前未在录音，忽略停止请求");
                return;
            }

            try
            {
                Debug.WriteLine("🛑 Android停止实时录音...");
                
                _isRecording = false;
                _recordingCancellation?.Cancel();
                
                Debug.WriteLine("✅ Android实时录音已停止 - 不播放录音内容");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ Android停止录音失败: {ex.Message}");
            }
        }

        // 实时音频捕获（参考Unity的SendAudioCoroutine）
        private async Task RealTimeAudioCapture(CancellationToken cancellationToken)
        {
            AudioRecord? audioRecord = null;
            try
            {
                Debug.WriteLine("🎤 开始初始化AudioRecord进行实时录音...");

                // 音频参数（与AudioService保持一致）
                const int SAMPLE_RATE = 16000; // 16kHz录音采样率
                var CHANNEL_CONFIG = ChannelIn.Mono;
                var AUDIO_FORMAT = Encoding.Pcm16bit;
                
                // 计算缓冲区大小（60ms帧 = 960采样）
                int frameSize = 960; // 60ms at 16kHz
                int bufferSize = AudioRecord.GetMinBufferSize(SAMPLE_RATE, CHANNEL_CONFIG, AUDIO_FORMAT);
                bufferSize = Math.Max(bufferSize, frameSize * 4); // 确保至少能容纳几帧

                Debug.WriteLine($"🎤 AudioRecord参数: 采样率={SAMPLE_RATE}, 缓冲区={bufferSize}");

                // 创建AudioRecord
                audioRecord = new AudioRecord(
                    AudioSource.Mic,
                    SAMPLE_RATE,
                    CHANNEL_CONFIG,
                    AUDIO_FORMAT,
                    bufferSize);

                if ((int)audioRecord.State != 1)
                {
                    Debug.WriteLine($"❌ AudioRecord初始化失败，状态: {audioRecord.State}");
                    return;
                }

                Debug.WriteLine("✅ AudioRecord初始化成功，开始录音");
                audioRecord.StartRecording();

                // 音频数据缓冲区
                var buffer = new short[frameSize]; // 960采样的缓冲区
                var floatBuffer = new float[frameSize];

                while (!cancellationToken.IsCancellationRequested && _isRecording)
                {
                    try
                    {
                        // 读取音频数据
                        int readSamples = audioRecord.Read(buffer, 0, buffer.Length);
                        
                        if (readSamples > 0)
                        {
                            // 转换为float数组（-1.0到1.0）
                            for (int i = 0; i < readSamples; i++)
                            {
                                floatBuffer[i] = buffer[i] / 32768.0f;
                            }

                            // 如果读取的样本数不够一帧，则补零
                            if (readSamples < frameSize)
                            {
                                for (int i = readSamples; i < frameSize; i++)
                                {
                                    floatBuffer[i] = 0.0f;
                                }
                            }

                            // 触发音频数据事件（发送给AudioService处理）
                            AudioDataReceived?.Invoke(this, floatBuffer);
                        }
                        else
                        {
                            Debug.WriteLine($"⚠️ AudioRecord读取失败，返回值: {readSamples}");
                        }

                        // 短暂延迟避免过度消耗CPU
                        await Task.Delay(10, cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"❌ 音频捕获循环异常: {ex.Message}");
                        break;
                    }
                }

                Debug.WriteLine("🛑 实时音频捕获结束");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ 实时音频捕获失败: {ex.Message}");
            }
            finally
            {
                try
                {
                    audioRecord?.Stop();
                    audioRecord?.Release();
                    audioRecord?.Dispose();
                    Debug.WriteLine("✅ AudioRecord资源已释放");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"⚠️ AudioRecord释放失败: {ex.Message}");
                }
            }
        }

        public async Task PlayAudioAsync(float[] audioData)
        {
            try
            {
                if (audioData == null || audioData.Length == 0)
                {
                    Debug.WriteLine("⚠️ PlayAudioAsync接收到空的音频数据");
                    return;
                }

                Debug.WriteLine($"🔊 PlayAudioAsync: 播放 {audioData.Length} 个音频采样");

                // 初始化AudioTrack用于播放PCM数据
                if (_audioTrack == null)
                {
                    await InitializeAudioTrack();
                }

                if (_audioTrack == null || (int)_audioTrack.State != 1)
                {
                    Debug.WriteLine("❌ AudioTrack未正确初始化");
                    return;
                }

                // 转换float数组为byte数组 (16位PCM)
                var byteData = new byte[audioData.Length * 2];
                for (int i = 0; i < audioData.Length; i++)
                {
                    // 将float(-1.0到1.0)转换为16位整数(-32768到32767)
                    var sample = (short)(Math.Max(-1.0f, Math.Min(1.0f, audioData[i])) * short.MaxValue);
                    byteData[i * 2] = (byte)(sample & 0xFF);
                    byteData[i * 2 + 1] = (byte)((sample >> 8) & 0xFF);
                }

                // 开始播放
                if (_audioTrack.PlayState != PlayState.Playing)
                {
                    _audioTrack.Play();
                    Debug.WriteLine("🔊 AudioTrack开始播放");
                }

                // 写入音频数据
                int written = _audioTrack.Write(byteData, 0, byteData.Length);
                Debug.WriteLine($"✅ 写入AudioTrack: {written}/{byteData.Length} 字节");

                // 等待播放完成（估算播放时间）
                double durationMs = (audioData.Length / 24000.0) * 1000; // 24kHz采样率
                await Task.Delay((int)Math.Max(50, durationMs));

            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ PlayAudioAsync失败: {ex.Message}");
            }
        }

        private async Task InitializeAudioTrack()
        {
            try
            {
                int bufferSize = AudioTrack.GetMinBufferSize(
                    24000, // 24kHz采样率，与AudioService中的PLAY_SAMPLE_RATE一致
                    ChannelOut.Mono,
                    Encoding.Pcm16bit);

                Debug.WriteLine($"🔊 初始化AudioTrack，缓冲区大小: {bufferSize}");

                _audioTrack = new AudioTrack(
                    global::Android.Media.Stream.Music,
                    24000, // 24kHz
                    ChannelOut.Mono,
                    Encoding.Pcm16bit,
                    bufferSize * 2,
                    AudioTrackMode.Stream);

                if ((int)_audioTrack.State == 1)
                {
                    Debug.WriteLine("✅ AudioTrack初始化成功");
                }
                else
                {
                    Debug.WriteLine($"❌ AudioTrack初始化失败，状态: {_audioTrack.State}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ AudioTrack初始化异常: {ex.Message}");
            }
        }

        public void Dispose()
        {
            try
            {
                Debug.WriteLine("🗑️ 释放AndroidAudioService资源");
                
                // 停止录音
                _isRecording = false;
                _recordingCancellation?.Cancel();
                _recordingCancellation?.Dispose();
                
                // 释放播放资源
                if (_audioTrack != null)
                {
                    if (_audioTrack.PlayState == PlayState.Playing)
                    {
                        _audioTrack.Stop();
                    }
                    _audioTrack.Release();
                    _audioTrack = null;
                }
                
                Debug.WriteLine("✅ AndroidAudioService资源释放完成");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ 释放资源失败: {ex.Message}");
            }
        }
    }
} 