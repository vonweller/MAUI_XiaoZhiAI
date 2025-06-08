using Android.Content;
using Android.Media;
using Android.OS;
using Java.IO;
using System;
using System.Threading.Tasks;
using SystemIO = System.IO;
using SystemDiagnostics = System.Diagnostics;
using SystemEnvironment = System.Environment;

namespace XiaoZhiAI_MAUI.Platforms.Android
{
    /// <summary>
    /// 简单可靠的Android录音器 - 使用MediaRecorder
    /// </summary>
    public class SimpleAudioRecorder : IDisposable
    {
        private MediaRecorder? _mediaRecorder;
        private MediaPlayer? _mediaPlayer;
        private string? _currentFilePath;
        private bool _isRecording = false;
        private readonly Context _context;

        public bool IsRecording => _isRecording;

        public SimpleAudioRecorder(Context context)
        {
            _context = context;
        }

        /// <summary>
        /// 开始录音
        /// </summary>
        public async Task<bool> StartRecordingAsync()
        {
            try
            {
                SystemDiagnostics.Debug.WriteLine("🎤 [SimpleAudioRecorder] 开始初始化MediaRecorder录音...");

                if (_isRecording)
                {
                    SystemDiagnostics.Debug.WriteLine("⚠️ [SimpleAudioRecorder] 录音已在进行中");
                    return false;
                }

                // 准备录音文件路径
                var documentsPath = SystemEnvironment.GetFolderPath(SystemEnvironment.SpecialFolder.Personal);
                var fileName = $"recording_{DateTime.Now:yyyyMMdd_HHmmss}.3gp";
                _currentFilePath = SystemIO.Path.Combine(documentsPath, fileName);

                SystemDiagnostics.Debug.WriteLine($"📁 [SimpleAudioRecorder] 录音文件路径: {_currentFilePath}");

                // 确保目录存在
                var directory = SystemIO.Path.GetDirectoryName(_currentFilePath);
                if (!SystemIO.Directory.Exists(directory))
                {
                    SystemIO.Directory.CreateDirectory(directory);
                }

                // 初始化MediaRecorder
                _mediaRecorder = new MediaRecorder();
                
                // 设置音频源 (麦克风)
                _mediaRecorder.SetAudioSource(AudioSource.Mic);
                
                // 设置输出格式 (3GP格式，兼容性好)
                _mediaRecorder.SetOutputFormat(OutputFormat.ThreeGpp);
                
                // 设置音频编码器 (AMR_NB编码，兼容性好)
                _mediaRecorder.SetAudioEncoder(AudioEncoder.AmrNb);
                
                // 设置输出文件
                _mediaRecorder.SetOutputFile(_currentFilePath);

                // 准备录音
                await Task.Run(() => _mediaRecorder.Prepare());
                SystemDiagnostics.Debug.WriteLine("✅ [SimpleAudioRecorder] MediaRecorder准备完成");

                // 开始录音
                _mediaRecorder.Start();
                _isRecording = true;

                SystemDiagnostics.Debug.WriteLine("🔴 [SimpleAudioRecorder] 录音已开始");
                return true;
            }
            catch (Exception ex)
            {
                SystemDiagnostics.Debug.WriteLine($"❌ [SimpleAudioRecorder] 开始录音失败: {ex.Message}");
                
                // 清理资源
                try
                {
                    _mediaRecorder?.Release();
                    _mediaRecorder = null;
                }
                catch (Exception cleanupEx)
                {
                    SystemDiagnostics.Debug.WriteLine($"⚠️ [SimpleAudioRecorder] 清理MediaRecorder失败: {cleanupEx.Message}");
                }

                return false;
            }
        }

        /// <summary>
        /// 停止录音并返回文件路径
        /// </summary>
        public async Task<string?> StopRecordingAsync()
        {
            try
            {
                SystemDiagnostics.Debug.WriteLine("⏹️ [SimpleAudioRecorder] 停止录音...");

                if (!_isRecording || _mediaRecorder == null)
                {
                    SystemDiagnostics.Debug.WriteLine("⚠️ [SimpleAudioRecorder] 没有正在进行的录音");
                    return null;
                }

                // 停止录音
                await Task.Run(() =>
                {
                    _mediaRecorder.Stop();
                    _mediaRecorder.Release();
                });

                _mediaRecorder = null;
                _isRecording = false;

                // 检查文件是否成功创建
                if (!string.IsNullOrEmpty(_currentFilePath) && SystemIO.File.Exists(_currentFilePath))
                {
                    var fileInfo = new SystemIO.FileInfo(_currentFilePath);
                    SystemDiagnostics.Debug.WriteLine($"✅ [SimpleAudioRecorder] 录音完成: {_currentFilePath}");
                    SystemDiagnostics.Debug.WriteLine($"📏 [SimpleAudioRecorder] 文件大小: {fileInfo.Length} bytes");

                    if (fileInfo.Length > 0)
                    {
                        return _currentFilePath;
                    }
                    else
                    {
                        SystemDiagnostics.Debug.WriteLine("❌ [SimpleAudioRecorder] 录音文件为空");
                        return null;
                    }
                }
                else
                {
                    SystemDiagnostics.Debug.WriteLine("❌ [SimpleAudioRecorder] 录音文件不存在");
                    return null;
                }
            }
            catch (Exception ex)
            {
                SystemDiagnostics.Debug.WriteLine($"❌ [SimpleAudioRecorder] 停止录音失败: {ex.Message}");
                
                // 清理资源
                try
                {
                    _mediaRecorder?.Release();
                    _mediaRecorder = null;
                    _isRecording = false;
                }
                catch (Exception cleanupEx)
                {
                    SystemDiagnostics.Debug.WriteLine($"⚠️ [SimpleAudioRecorder] 清理MediaRecorder失败: {cleanupEx.Message}");
                }

                return null;
            }
        }

        /// <summary>
        /// 播放录音文件
        /// </summary>
        public async Task<bool> PlayRecordingAsync(string filePath)
        {
            try
            {
                SystemDiagnostics.Debug.WriteLine($"🔊 [SimpleAudioRecorder] 开始播放录音: {filePath}");

                if (string.IsNullOrEmpty(filePath) || !SystemIO.File.Exists(filePath))
                {
                    SystemDiagnostics.Debug.WriteLine("❌ [SimpleAudioRecorder] 录音文件不存在");
                    return false;
                }

                // 停止之前的播放
                if (_mediaPlayer != null)
                {
                    try
                    {
                        if (_mediaPlayer.IsPlaying)
                        {
                            _mediaPlayer.Stop();
                        }
                        _mediaPlayer.Release();
                    }
                    catch (Exception ex)
                    {
                        SystemDiagnostics.Debug.WriteLine($"⚠️ [SimpleAudioRecorder] 停止之前的播放失败: {ex.Message}");
                    }
                }

                // 创建新的MediaPlayer
                _mediaPlayer = new MediaPlayer();
                
                // 设置数据源
                _mediaPlayer.SetDataSource(filePath);
                
                // 准备播放
                await Task.Run(() => _mediaPlayer.Prepare());
                
                // 开始播放
                _mediaPlayer.Start();
                
                SystemDiagnostics.Debug.WriteLine("🔊 [SimpleAudioRecorder] 开始播放录音");

                // 等待播放完成
                var duration = _mediaPlayer.Duration;
                SystemDiagnostics.Debug.WriteLine($"⏰ [SimpleAudioRecorder] 录音时长: {duration}ms");
                
                if (duration > 0)
                {
                    await Task.Delay(duration + 500); // 额外等待500ms确保播放完成
                }
                else
                {
                    await Task.Delay(2000); // 默认等待2秒
                }

                SystemDiagnostics.Debug.WriteLine("✅ [SimpleAudioRecorder] 录音播放完成");
                return true;
            }
            catch (Exception ex)
            {
                SystemDiagnostics.Debug.WriteLine($"❌ [SimpleAudioRecorder] 播放录音失败: {ex.Message}");
                return false;
            }
            finally
            {
                // 清理MediaPlayer
                try
                {
                    if (_mediaPlayer != null)
                    {
                        if (_mediaPlayer.IsPlaying)
                        {
                            _mediaPlayer.Stop();
                        }
                        _mediaPlayer.Release();
                        _mediaPlayer = null;
                    }
                }
                catch (Exception ex)
                {
                    SystemDiagnostics.Debug.WriteLine($"⚠️ [SimpleAudioRecorder] 清理MediaPlayer失败: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 获取录音信息
        /// </summary>
        public string GetRecordingInfo()
        {
            var info = "📱 SimpleAudioRecorder 信息:\n";
            info += $"• 录音状态: {(_isRecording ? "录音中" : "停止")}\n";
            info += $"• 当前文件: {_currentFilePath ?? "无"}\n";
            info += $"• 录音格式: 3GP (AMR-NB)\n";
            info += $"• 音频源: 麦克风\n";
            
            if (!string.IsNullOrEmpty(_currentFilePath) && SystemIO.File.Exists(_currentFilePath))
            {
                var fileInfo = new SystemIO.FileInfo(_currentFilePath);
                info += $"• 文件大小: {fileInfo.Length} bytes\n";
                info += $"• 创建时间: {fileInfo.CreationTime:yyyy-MM-dd HH:mm:ss}\n";
            }

            return info;
        }

        public void Dispose()
        {
            try
            {
                SystemDiagnostics.Debug.WriteLine("🗑️ [SimpleAudioRecorder] 释放资源...");

                // 停止录音
                if (_isRecording)
                {
                    _ = Task.Run(async () => await StopRecordingAsync());
                }

                // 释放MediaRecorder
                if (_mediaRecorder != null)
                {
                    try
                    {
                        if (_isRecording)
                        {
                            _mediaRecorder.Stop();
                        }
                        _mediaRecorder.Release();
                    }
                    catch (Exception ex)
                    {
                        SystemDiagnostics.Debug.WriteLine($"⚠️ [SimpleAudioRecorder] 释放MediaRecorder失败: {ex.Message}");
                    }
                    _mediaRecorder = null;
                }

                // 释放MediaPlayer
                if (_mediaPlayer != null)
                {
                    try
                    {
                        if (_mediaPlayer.IsPlaying)
                        {
                            _mediaPlayer.Stop();
                        }
                        _mediaPlayer.Release();
                    }
                    catch (Exception ex)
                    {
                        SystemDiagnostics.Debug.WriteLine($"⚠️ [SimpleAudioRecorder] 释放MediaPlayer失败: {ex.Message}");
                    }
                    _mediaPlayer = null;
                }

                SystemDiagnostics.Debug.WriteLine("✅ [SimpleAudioRecorder] 资源释放完成");
            }
            catch (Exception ex)
            {
                SystemDiagnostics.Debug.WriteLine($"❌ [SimpleAudioRecorder] 释放资源失败: {ex.Message}");
            }
        }
    }
} 