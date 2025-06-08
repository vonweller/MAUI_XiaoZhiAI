using Android;
using Android.Content;
using Android.Media;
using AndroidX.Core.App;
using AndroidX.Core.Content;
using Microsoft.Maui.Controls;
using System.Diagnostics;
using XiaoZhiAI_MAUI.Services;

namespace XiaoZhiAI_MAUI.Platforms.Android
{
    public class AndroidAudioService : IPlatformAudioService
    {
        private SimpleAudioRecorder? _simpleRecorder;
        private AudioTrack? _audioTrack;
        private bool _isPlaying = false;
        private string? _lastRecordingPath;
        
        public event EventHandler<float[]> AudioDataReceived;

        public async Task InitializeAsync()
        {
            try
            {
                Debug.WriteLine("=== 初始化新版Android音频服务 (使用MediaRecorder) ===");
                
                // 检查权限
                await CheckPermissions();
                
                // 获取Context
                var context = Platform.CurrentActivity ?? Microsoft.Maui.ApplicationModel.Platform.CurrentActivity;
                if (context == null)
                {
                    Debug.WriteLine("❌ 无法获取Context");
                    return;
                }
                
                // 初始化SimpleAudioRecorder
                _simpleRecorder = new SimpleAudioRecorder(context);
                
                Debug.WriteLine("✅ Android音频服务初始化完成");
                
                // 开始录制→播放测试
                await StartRecordPlaybackTest();
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

        // 核心功能：录制→播放测试 (使用MediaRecorder)
        public async Task StartRecordPlaybackTest()
        {
            try
            {
                Debug.WriteLine("=== 开始MediaRecorder录制→播放测试 ===");
                Debug.WriteLine("🎤 即将开始3秒录音，请对着麦克风说话...");
                
                await Task.Delay(1000); // 等待1秒准备
                
                // 开始录音
                await StartRecording();
                
                // 录音3秒
                await Task.Delay(3000);
                
                // 停止录音
                await StopRecording();
                
                // 等待1秒
                await Task.Delay(1000);
                
                Debug.WriteLine("🔊 录音完成，即将播放刚才录制的声音...");
                
                // 播放录制的声音
                await PlayRecordedAudio();
                
                Debug.WriteLine("✅ === MediaRecorder录制→播放测试完成 ===");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ 录制播放测试失败: {ex.Message}");
            }
        }

        private async Task StartRecording()
        {
            try
            {
                if (_simpleRecorder == null)
                {
                    Debug.WriteLine("❌ SimpleAudioRecorder未初始化");
                    return;
                }

                Debug.WriteLine("🎤 开始MediaRecorder录音...");
                var success = await _simpleRecorder.StartRecordingAsync();
                
                if (success)
                {
                    Debug.WriteLine("✅ MediaRecorder录音开始成功");
                    
                    // 模拟AudioDataReceived事件（保持兼容性）
                    _ = Task.Run(async () =>
                    {
                        while (_simpleRecorder?.IsRecording == true)
                        {
                            // 发送模拟数据保持兼容性
                            var fakeData = new float[1024];
                            for (int i = 0; i < fakeData.Length; i++)
                            {
                                fakeData[i] = (float)(Random.Shared.NextDouble() * 0.1); // 小幅度随机数据
                            }
                            AudioDataReceived?.Invoke(this, fakeData);
                            await Task.Delay(50);
                        }
                    });
                }
                else
                {
                    Debug.WriteLine("❌ MediaRecorder录音开始失败");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ 开始录音异常: {ex.Message}");
            }
        }

        private async Task StopRecording()
        {
            try
            {
                if (_simpleRecorder == null)
                {
                    Debug.WriteLine("❌ SimpleAudioRecorder未初始化");
                    return;
                }

                Debug.WriteLine("⏹️ 停止MediaRecorder录音...");
                _lastRecordingPath = await _simpleRecorder.StopRecordingAsync();
                
                if (!string.IsNullOrEmpty(_lastRecordingPath))
                {
                    var fileInfo = new System.IO.FileInfo(_lastRecordingPath);
                    Debug.WriteLine($"✅ 录音完成: {_lastRecordingPath}");
                    Debug.WriteLine($"📁 文件大小: {fileInfo.Length} bytes");
                }
                else
                {
                    Debug.WriteLine("❌ 录音失败，没有生成文件");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ 停止录音异常: {ex.Message}");
            }
        }

        private async Task PlayRecordedAudio()
        {
            try
            {
                if (string.IsNullOrEmpty(_lastRecordingPath))
                {
                    Debug.WriteLine("❌ 没有录音文件可播放");
                    return;
                }

                if (_simpleRecorder == null)
                {
                    Debug.WriteLine("❌ SimpleAudioRecorder未初始化");
                    return;
                }

                Debug.WriteLine($"🔊 开始播放录音: {_lastRecordingPath}");
                var success = await _simpleRecorder.PlayRecordingAsync(_lastRecordingPath);
                
                if (success)
                {
                    Debug.WriteLine("✅ 录音播放完成");
                }
                else
                {
                    Debug.WriteLine("❌ 录音播放失败");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ 播放录音异常: {ex.Message}");
            }
        }

        // IPlatformAudioService接口实现
        public async Task StartRecordingAsync()
        {
            try
            {
                await StartRecording();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ StartRecordingAsync失败: {ex.Message}");
            }
        }

        public async Task StopRecordingAsync()
        {
            try
            {
                await StopRecording();
                
                // 自动播放录制的音频
                if (!string.IsNullOrEmpty(_lastRecordingPath))
                {
                    Debug.WriteLine("🔊 录音停止后自动播放录制的音频...");
                    await Task.Delay(500); // 等待500ms确保录音完全停止
                    await PlayRecordedAudio();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ StopRecordingAsync失败: {ex.Message}");
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

                if (_audioTrack == null || _audioTrack.State != AudioTrackState.Initialized)
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

                if (_audioTrack.State == AudioTrackState.Initialized)
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
                
                _simpleRecorder?.Dispose();
                _simpleRecorder = null;
                
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