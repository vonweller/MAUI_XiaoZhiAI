using Android.Media;
using System.Diagnostics;
using AndroidX.Core.App;
using Android.Content;
using AndroidStream = Android.Media.Stream;

namespace XiaoZhiAI_MAUI.Platforms.Android
{
    public class SimpleAudioTest
    {
        private AudioTrack _audioTrack;
        private AudioManager _audioManager;
        private const int SAMPLE_RATE = 44100;
        private const int BUFFER_SIZE = 4096;

        public async Task<bool> InitializeAsync()
        {
            try
            {
                Debug.WriteLine("SimpleAudioTest: 开始初始化");

                // 获取AudioManager
                var context = Platform.CurrentActivity ?? Microsoft.Maui.ApplicationModel.Platform.CurrentActivity ?? global::Android.App.Application.Context;
                _audioManager = context.GetSystemService(Context.AudioService) as AudioManager;

                // 请求音频焦点
                if (_audioManager != null)
                {
                    var result = _audioManager.RequestAudioFocus(null, AndroidStream.Music, AudioFocus.Gain);
                    Debug.WriteLine($"SimpleAudioTest: 音频焦点请求结果: {result}");
                }

                // 创建AudioTrack
                var minBufferSize = AudioTrack.GetMinBufferSize(SAMPLE_RATE, ChannelOut.Mono, Encoding.Pcm16bit);
                Debug.WriteLine($"SimpleAudioTest: 最小缓冲区大小: {minBufferSize}");

                var bufferSize = Math.Max(minBufferSize, BUFFER_SIZE);
                Debug.WriteLine($"SimpleAudioTest: 使用缓冲区大小: {bufferSize}");

                _audioTrack = new AudioTrack.Builder()
                    .SetAudioAttributes(new AudioAttributes.Builder()
                        .SetUsage(AudioUsageKind.Media)
                        .SetContentType(AudioContentType.Music)
                        .Build())
                    .SetAudioFormat(new AudioFormat.Builder()
                        .SetEncoding(Encoding.Pcm16bit)
                        .SetSampleRate(SAMPLE_RATE)
                        .SetChannelMask(ChannelOut.Mono)
                        .Build())
                    .SetBufferSizeInBytes(bufferSize)
                    .SetTransferMode(AudioTrackMode.Stream)
                    .Build();

                if ((int)_audioTrack.State == 1)
                {
                    Debug.WriteLine("SimpleAudioTest: AudioTrack初始化成功");
                    return true;
                }
                else
                {
                    Debug.WriteLine($"SimpleAudioTest: AudioTrack初始化失败，状态: {_audioTrack.State}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"SimpleAudioTest: 初始化失败: {ex.Message}");
                Debug.WriteLine($"SimpleAudioTest: 详细错误: {ex}");
                return false;
            }
        }

        public async Task<bool> PlayBeepAsync()
        {
            try
            {
                if (_audioTrack == null || (int)_audioTrack.State != 1)
                {
                    Debug.WriteLine("SimpleAudioTest: AudioTrack未初始化");
                    return false;
                }

                Debug.WriteLine("SimpleAudioTest: 开始生成哔哔声");

                // 生成1秒的1000Hz正弦波
                int duration = 1; // 1秒
                int numSamples = SAMPLE_RATE * duration;
                short[] samples = new short[numSamples];
                
                for (int i = 0; i < numSamples; i++)
                {
                    double time = (double)i / SAMPLE_RATE;
                    double amplitude = 0.3; // 30%音量
                    double frequency = 1000; // 1000Hz
                    samples[i] = (short)(amplitude * short.MaxValue * Math.Sin(2 * Math.PI * frequency * time));
                }

                Debug.WriteLine($"SimpleAudioTest: 生成了 {samples.Length} 个音频采样");

                // 转换为字节数组
                byte[] audioData = new byte[samples.Length * 2];
                for (int i = 0; i < samples.Length; i++)
                {
                    audioData[i * 2] = (byte)(samples[i] & 0xFF);
                    audioData[i * 2 + 1] = (byte)((samples[i] >> 8) & 0xFF);
                }

                Debug.WriteLine($"SimpleAudioTest: 转换为 {audioData.Length} 字节");

                // 开始播放
                _audioTrack.Play();
                Debug.WriteLine("SimpleAudioTest: AudioTrack.Play() 调用完成");

                // 写入音频数据
                int totalWritten = 0;
                int chunkSize = 4096;
                
                for (int offset = 0; offset < audioData.Length; offset += chunkSize)
                {
                    int writeSize = Math.Min(chunkSize, audioData.Length - offset);
                    int written = _audioTrack.Write(audioData, offset, writeSize);
                    totalWritten += written;
                    
                    Debug.WriteLine($"SimpleAudioTest: 写入进度 {totalWritten}/{audioData.Length} 字节 (本次写入: {written})");
                    
                    if (written < 0)
                    {
                        Debug.WriteLine($"SimpleAudioTest: 写入错误: {written}");
                        break;
                    }
                    
                    await Task.Delay(50); // 小延迟
                }

                // 等待播放完成
                Debug.WriteLine("SimpleAudioTest: 等待播放完成");
                await Task.Delay(duration * 1000 + 500);

                _audioTrack.Stop();
                Debug.WriteLine("SimpleAudioTest: 播放完成并停止");

                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"SimpleAudioTest: 播放失败: {ex.Message}");
                Debug.WriteLine($"SimpleAudioTest: 详细错误: {ex}");
                return false;
            }
        }

        public async Task<bool> PlayToneGeneratorAsync()
        {
            try
            {
                Debug.WriteLine("SimpleAudioTest: 使用ToneGenerator播放音调");
                
                using var toneGenerator = new ToneGenerator(AndroidStream.Music, Volume.Max); // 最大音量
                toneGenerator.StartTone(Tone.PropBeep, 1000); // 播放1秒
                
                await Task.Delay(1200); // 等待播放完成
                toneGenerator.StopTone();
                
                Debug.WriteLine("SimpleAudioTest: ToneGenerator播放完成");
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"SimpleAudioTest: ToneGenerator播放失败: {ex.Message}");
                return false;
            }
        }

        public string GetAudioInfo()
        {
            try
            {
                var info = "音频设备信息:\n";
                
                if (_audioManager != null)
                {
                    info += $"• 音频模式: {_audioManager.Mode}\n";
                    info += $"• 音量 (音乐): {_audioManager.GetStreamVolume(AndroidStream.Music)}/{_audioManager.GetStreamMaxVolume(AndroidStream.Music)}\n";
                    info += $"• 音量 (系统): {_audioManager.GetStreamVolume(AndroidStream.System)}/{_audioManager.GetStreamMaxVolume(AndroidStream.System)}\n";
                    info += $"• 是否静音: {_audioManager.IsStreamMute(AndroidStream.Music)}\n";
                }

                if (_audioTrack != null)
                {
                    info += $"• AudioTrack状态: {_audioTrack.State}\n";
                    info += $"• AudioTrack播放状态: {_audioTrack.PlayState}\n";
                    info += $"• 采样率: {_audioTrack.SampleRate}\n";
                    info += $"• 声道配置: {_audioTrack.ChannelConfiguration}\n";
                }

                var minBufferSize = AudioTrack.GetMinBufferSize(SAMPLE_RATE, ChannelOut.Mono, Encoding.Pcm16bit);
                info += $"• 最小缓冲区: {minBufferSize} 字节\n";

                return info;
            }
            catch (Exception ex)
            {
                return $"获取音频信息失败: {ex.Message}";
            }
        }

        public async Task<bool> TestRecordPlaybackAsync()
        {
            try
            {
                Debug.WriteLine("SimpleAudioTest: 开始音频系统诊断");
                
                // 获取当前的音频音量以进行诊断
                if (_audioManager != null)
                {
                    var musicVolume = _audioManager.GetStreamVolume(AndroidStream.Music);
                    var maxMusicVolume = _audioManager.GetStreamMaxVolume(AndroidStream.Music);
                    Debug.WriteLine($"SimpleAudioTest: 当前音乐音量: {musicVolume}/{maxMusicVolume}");
                    
                    // 如果音量太低，尝试提高
                    if (musicVolume < maxMusicVolume / 2)
                    {
                        Debug.WriteLine("SimpleAudioTest: 音量较低，尝试提高音量");
                    }
                }

                // 不播放测试音，只进行诊断
                Debug.WriteLine("SimpleAudioTest: 音频系统诊断完成（未播放测试音）");
                
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"SimpleAudioTest: 音频系统诊断失败: {ex.Message}");
                return false;
            }
        }

        public void Dispose()
        {
            try
            {
                _audioTrack?.Stop();
                _audioTrack?.Release();
                _audioTrack?.Dispose();
                _audioTrack = null;

                if (_audioManager != null)
                {
                    _audioManager.AbandonAudioFocus(null);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"SimpleAudioTest: 清理资源失败: {ex.Message}");
            }
        }
    }
} 