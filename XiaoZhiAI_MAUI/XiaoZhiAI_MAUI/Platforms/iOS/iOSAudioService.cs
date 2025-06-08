using AVFoundation;
using AudioToolbox;
using Foundation;
using System.Diagnostics;
using XiaoZhiAI_MAUI.Services;

namespace XiaoZhiAI_MAUI.Platforms.iOS
{
    public class iOSAudioService : IPlatformAudioService
    {
        private AVAudioEngine _audioEngine;
        private AVAudioPlayerNode _playerNode;
        private AVAudioInputNode _inputNode;
        private AVAudioFormat _recordFormat;
        private AVAudioFormat _playFormat;
        private bool _isRecording = false;

        public event EventHandler<float[]> AudioDataReceived;

        public async Task InitializeAsync()
        {
            Debug.WriteLine("初始化iOS音频服务");

            try
            {
                // 请求录音权限
                var tcs = new TaskCompletionSource<bool>();
                AVAudioSession.SharedInstance().RequestRecordPermission((granted) =>
                {
                    tcs.SetResult(granted);
                });
                var status = await tcs.Task;
                
                if (!status)
                {
                    throw new UnauthorizedAccessException("录音权限被拒绝");
                }

                // 配置音频会话
                var audioSession = AVAudioSession.SharedInstance();
                var error = audioSession.SetCategory(AVAudioSessionCategory.PlayAndRecord, AVAudioSessionCategoryOptions.DefaultToSpeaker);
                if (error != null)
                {
                    throw new Exception($"设置音频会话类别失败: {error.LocalizedDescription}");
                }

                error = audioSession.SetActive(true);
                if (error != null)
                {
                    throw new Exception($"激活音频会话失败: {error.LocalizedDescription}");
                }

                // 初始化音频引擎
                _audioEngine = new AVAudioEngine();
                _inputNode = _audioEngine.InputNode;
                _playerNode = new AVAudioPlayerNode();

                // 设置录音格式 (16kHz, 单声道, 32位浮点)
                _recordFormat = new AVAudioFormat(16000, 1);
                
                // 设置播放格式 (24kHz, 单声道, 32位浮点)
                _playFormat = new AVAudioFormat(24000, 1);

                // 连接播放节点
                _audioEngine.AttachNode(_playerNode);
                _audioEngine.Connect(_playerNode, _audioEngine.MainMixerNode, _playFormat);

                Debug.WriteLine("iOS音频服务初始化完成");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"iOS音频服务初始化失败: {ex.Message}");
                throw;
            }
        }

        public async Task StartRecordingAsync()
        {
            if (_isRecording) return;

            try
            {
                Debug.WriteLine("iOS开始录音");

                // 安装录音tap
                _inputNode.InstallTapOnBus(0, 1024, _recordFormat, (buffer, when) =>
                {
                    ProcessAudioBuffer(buffer);
                });

                // 启动音频引擎
                _audioEngine.StartAndReturnError(out var error);
                if (error != null)
                {
                    throw new Exception($"启动音频引擎失败: {error.LocalizedDescription}");
                }

                _isRecording = true;
                Debug.WriteLine("iOS录音已开始");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"iOS开始录音失败: {ex.Message}");
                throw;
            }
        }

        public async Task StopRecordingAsync()
        {
            if (!_isRecording) return;

            try
            {
                Debug.WriteLine("iOS停止录音");

                _isRecording = false;
                _inputNode.RemoveTapOnBus(0);
                _audioEngine.Stop();

                Debug.WriteLine("iOS录音已停止");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"iOS停止录音失败: {ex.Message}");
            }
        }

        public async Task PlayAudioAsync(float[] audioData)
        {
            if (_playerNode == null || audioData == null || audioData.Length == 0) return;

            try
            {
                // 创建音频缓冲区
                var buffer = new AVAudioPcmBuffer(_playFormat, (uint)audioData.Length);
                if (buffer == null) return;

                // 复制音频数据
                unsafe
                {
                    var channelDataPtr = buffer.FloatChannelData;
                    var channelData = (float*)((void**)channelDataPtr)[0];
                    for (int i = 0; i < audioData.Length; i++)
                    {
                        channelData[i] = audioData[i];
                    }
                }
                buffer.FrameLength = (uint)audioData.Length;

                // 播放音频
                _playerNode.ScheduleBuffer(buffer, null);
                
                if (!_playerNode.Playing)
                {
                    _playerNode.Play();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"iOS播放音频失败: {ex.Message}");
            }
        }

        private void ProcessAudioBuffer(AVAudioPcmBuffer buffer)
        {
            if (!_isRecording || buffer == null) return;

            try
            {
                var frameLength = (int)buffer.FrameLength;
                if (frameLength == 0) return;

                var audioData = new float[frameLength];

                unsafe
                {
                    var channelDataPtr = buffer.FloatChannelData;
                    var channelData = (float*)((void**)channelDataPtr)[0];
                    for (int i = 0; i < frameLength; i++)
                    {
                        audioData[i] = channelData[i];
                    }
                }

                AudioDataReceived?.Invoke(this, audioData);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"处理音频缓冲区失败: {ex.Message}");
            }
        }

        public void Dispose()
        {
            Debug.WriteLine("释放iOS音频服务资源");

            _isRecording = false;
            
            _inputNode?.RemoveTapOnBus(0);
            _audioEngine?.Stop();
            _playerNode?.Stop();

            _playerNode?.Dispose();
            _audioEngine?.Dispose();
            
            var audioSession = AVAudioSession.SharedInstance();
            audioSession.SetActive(false);
        }
    }
} 