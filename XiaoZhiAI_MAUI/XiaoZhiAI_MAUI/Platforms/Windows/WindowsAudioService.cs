using System.Diagnostics;
using XiaoZhiAI_MAUI.Services;
using System.Collections.Concurrent;
using System.Media;
using System.Runtime.InteropServices;

namespace XiaoZhiAI_MAUI.Platforms.Windows
{
    /// <summary>
    /// Windows平台音频服务 - 使用Windows WaveOut API实现真实音频播放
    /// </summary>
    public class WindowsAudioService : IPlatformAudioService
    {
        // Windows WaveOut API
        [DllImport("winmm.dll")]
        private static extern int waveOutOpen(out IntPtr hWaveOut, int uDeviceID, ref WaveFormat lpFormat, WaveDelegate dwCallback, IntPtr dwInstance, int dwFlags);
        
        [DllImport("winmm.dll")]
        private static extern int waveOutPrepareHeader(IntPtr hWaveOut, ref WaveHeader lpWaveOutHdr, int uSize);
        
        [DllImport("winmm.dll")]
        private static extern int waveOutWrite(IntPtr hWaveOut, ref WaveHeader lpWaveOutHdr, int uSize);
        
        [DllImport("winmm.dll")]
        private static extern int waveOutUnprepareHeader(IntPtr hWaveOut, ref WaveHeader lpWaveOutHdr, int uSize);
        
        [DllImport("winmm.dll")]
        private static extern int waveOutClose(IntPtr hWaveOut);

        // WaveIn API for recording
        [DllImport("winmm.dll")]
        private static extern int waveInOpen(out IntPtr hWaveIn, int uDeviceID, ref WaveFormat lpFormat, WaveDelegate dwCallback, IntPtr dwInstance, int dwFlags);
        
        [DllImport("winmm.dll")]
        private static extern int waveInPrepareHeader(IntPtr hWaveIn, ref WaveHeader lpWaveInHdr, int uSize);
        
        [DllImport("winmm.dll")]
        private static extern int waveInAddBuffer(IntPtr hWaveIn, ref WaveHeader lpWaveInHdr, int uSize);
        
        [DllImport("winmm.dll")]
        private static extern int waveInStart(IntPtr hWaveIn);
        
        [DllImport("winmm.dll")]
        private static extern int waveInStop(IntPtr hWaveIn);
        
        [DllImport("winmm.dll")]
        private static extern int waveInReset(IntPtr hWaveIn);
        
        [DllImport("winmm.dll")]
        private static extern int waveInUnprepareHeader(IntPtr hWaveIn, ref WaveHeader lpWaveInHdr, int uSize);
        
        [DllImport("winmm.dll")]
        private static extern int waveInClose(IntPtr hWaveIn);

        private delegate void WaveDelegate(IntPtr hdrvr, int uMsg, int dwUser, ref WaveHeader wavhdr, int dwParam2);

        [StructLayout(LayoutKind.Sequential)]
        private struct WaveFormat
        {
            public short wFormatTag;
            public short nChannels;
            public int nSamplesPerSec;
            public int nAvgBytesPerSec;
            public short nBlockAlign;
            public short wBitsPerSample;
            public short cbSize;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct WaveHeader
        {
            public IntPtr lpData;
            public int dwBufferLength;
            public int dwBytesRecorded;
            public IntPtr dwUser;
            public int dwFlags;
            public int dwLoops;
            public IntPtr lpNext;
            public IntPtr reserved;
        }

        private const int WAVE_FORMAT_PCM = 1;
        private const int CALLBACK_FUNCTION = 0x00030000;
        private const int WOM_DONE = 0x3BD;
        private const int WIM_DATA = 0x3C0;
        private const int WIM_CLOSE = 0x3C1;
        
        private IntPtr _waveOut = IntPtr.Zero;
        private IntPtr _waveIn = IntPtr.Zero;
        private WaveFormat _waveFormat;
        private WaveFormat _recordFormat;
        private readonly Queue<GCHandle> _bufferHandles = new();
        private readonly Queue<GCHandle> _headerHandles = new();
        private readonly Queue<GCHandle> _recordBufferHandles = new();
        private readonly Queue<GCHandle> _recordHeaderHandles = new();
        private readonly List<byte[]> _recordBuffers = new();
        private readonly List<GCHandle> _activeRecordHandles = new();
        private bool _isRecording = false;
        private bool _isInitialized = false;
        private CancellationTokenSource _recordingCts;
        private CancellationTokenSource _playbackCts;
        private Task _recordingTask;
        private Task _playbackTask;
        
        private readonly ConcurrentQueue<float[]> _playbackQueue = new();
        private readonly object _lockObject = new();

        public event EventHandler<float[]> AudioDataReceived;

        public async Task InitializeAsync()
        {
            Debug.WriteLine("初始化Windows音频服务（WaveOut API）");

            try
            {
                // 设置播放音频格式：24kHz, 16位, 单声道
                _waveFormat = new WaveFormat
                {
                    wFormatTag = WAVE_FORMAT_PCM,
                    nChannels = 1,
                    nSamplesPerSec = 24000,
                    wBitsPerSample = 16,
                    nBlockAlign = 2, // 16位 * 1声道 / 8 = 2字节
                    nAvgBytesPerSec = 24000 * 2, // 24000 * 2字节 = 48000字节/秒
                    cbSize = 0
                };

                // 设置录音音频格式：16kHz, 16位, 单声道
                _recordFormat = new WaveFormat
                {
                    wFormatTag = WAVE_FORMAT_PCM,
                    nChannels = 1,
                    nSamplesPerSec = 16000,
                    wBitsPerSample = 16,
                    nBlockAlign = 2, // 16位 * 1声道 / 8 = 2字节
                    nAvgBytesPerSec = 16000 * 2, // 16000 * 2字节 = 32000字节/秒
                    cbSize = 0
                };

                // 打开音频输出设备
                int result = waveOutOpen(out _waveOut, -1, ref _waveFormat, WaveOutCallback, IntPtr.Zero, CALLBACK_FUNCTION);
                if (result != 0)
                {
                    throw new Exception($"无法打开音频输出设备，错误代码: {result}");
                }

                _isInitialized = true;
                
                // 启动播放任务
                _playbackCts = new CancellationTokenSource();
                _playbackTask = PlaybackLoop(_playbackCts.Token);
                
                Debug.WriteLine("Windows音频服务初始化完成（WaveOut API）");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Windows音频服务初始化失败: {ex.Message}");
                throw;
            }
        }

        public async Task StartRecordingAsync()
        {
            if (!_isInitialized || _isRecording) return;

            try
            {
                Debug.WriteLine("Windows开始录音（模拟）");

                lock (_lockObject)
                {
                    _recordingCts?.Cancel();
                    _recordingCts = new CancellationTokenSource();
                    _isRecording = true;
                }

                // 启动真实录音任务
                _recordingTask = StartRealRecording(_recordingCts.Token);

                Debug.WriteLine("Windows录音已开始（真实麦克风）");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Windows开始录音失败: {ex.Message}");
                throw;
            }
        }

        public async Task StopRecordingAsync()
        {
            if (!_isRecording) return;

            try
            {
                Debug.WriteLine("Windows停止录音");

                lock (_lockObject)
                {
                    _isRecording = false;
                    _recordingCts?.Cancel();
                }

                if (_recordingTask != null)
                {
                    await _recordingTask;
                }

                Debug.WriteLine("Windows录音已停止");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Windows停止录音失败: {ex.Message}");
            }
        }

        public async Task PlayAudioAsync(float[] audioData)
        {
            if (!_isInitialized || audioData == null || audioData.Length == 0) return;

            try
            {
                Debug.WriteLine($"Windows接收到音频数据: {audioData.Length} 采样");
                
                // 将音频数据加入播放队列以供播放循环处理
                _playbackQueue.Enqueue(audioData);
                
                Debug.WriteLine($"Windows排队播放音频: {audioData.Length} 采样，队列长度: {_playbackQueue.Count}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Windows播放音频失败: {ex.Message}");
            }
        }



        private async Task StartRealRecording(CancellationToken cancellationToken)
        {
            try
            {
                Debug.WriteLine("开始真实麦克风录音");

                // 打开录音设备
                int result = waveInOpen(out _waveIn, -1, ref _recordFormat, WaveInCallback, IntPtr.Zero, CALLBACK_FUNCTION);
                if (result != 0)
                {
                    throw new Exception($"无法打开录音设备，错误代码: {result}");
                }

                // 准备录音缓冲区
                const int bufferCount = 4;
                const int bufferSize = 1920; // 60ms at 16kHz * 2 bytes = 1920 bytes
                
                for (int i = 0; i < bufferCount; i++)
                {
                    byte[] buffer = new byte[bufferSize];
                    _recordBuffers.Add(buffer);
                    
                    GCHandle bufferHandle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
                    _activeRecordHandles.Add(bufferHandle);
                    
                    WaveHeader header = new WaveHeader
                    {
                        lpData = bufferHandle.AddrOfPinnedObject(),
                        dwBufferLength = bufferSize,
                        dwFlags = 0
                    };
                    
                    GCHandle headerHandle = GCHandle.Alloc(header, GCHandleType.Pinned);
                    _activeRecordHandles.Add(headerHandle);
                    
                    // 准备缓冲区
                    result = waveInPrepareHeader(_waveIn, ref header, Marshal.SizeOf<WaveHeader>());
                    if (result == 0)
                    {
                        // 添加缓冲区到录音队列
                        waveInAddBuffer(_waveIn, ref header, Marshal.SizeOf<WaveHeader>());
                    }
                }

                // 开始录音
                result = waveInStart(_waveIn);
                if (result != 0)
                {
                    throw new Exception($"无法开始录音，错误代码: {result}");
                }

                Debug.WriteLine("Windows真实录音已开始");

                // 等待录音结束
                while (!cancellationToken.IsCancellationRequested && _isRecording)
                {
                    await Task.Delay(100, cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                Debug.WriteLine("录音被取消");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"真实录音错误: {ex.Message}");
            }
            finally
            {
                // 停止并清理录音设备
                if (_waveIn != IntPtr.Zero)
                {
                    try
                    {
                        waveInStop(_waveIn);
                        waveInReset(_waveIn);
                        
                        // 清理缓冲区
                        foreach (var handle in _activeRecordHandles)
                        {
                            try
                            {
                                handle.Free();
                            }
                            catch { }
                        }
                        _activeRecordHandles.Clear();
                        _recordBuffers.Clear();
                        
                        waveInClose(_waveIn);
                        _waveIn = IntPtr.Zero;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"清理录音设备失败: {ex.Message}");
                    }
                }
            }
        }

        private async Task PlaybackLoop(CancellationToken cancellationToken)
        {
            Debug.WriteLine("Windows播放循环已启动");
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    if (_playbackQueue.TryDequeue(out var audioData))
                    {
                        Debug.WriteLine($"Windows播放循环: 取出音频数据 {audioData.Length} 采样");
                        
                        // 真实播放音频
                        await PlayAudioDataAsync(audioData, cancellationToken);
                        
                        Debug.WriteLine($"Windows播放音频完成: {audioData.Length} 采样");
                    }
                    else
                    {
                        await Task.Delay(10, cancellationToken);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                Debug.WriteLine("Windows播放循环被取消");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"播放循环错误: {ex.Message}");
            }
            Debug.WriteLine("Windows播放循环已结束");
        }

        private async Task PlayAudioDataAsync(float[] audioData, CancellationToken cancellationToken)
        {
            if (_waveOut == IntPtr.Zero || audioData == null || audioData.Length == 0)
                return;

            try
            {
                // 将float数据转换为16位PCM
                byte[] pcmData = new byte[audioData.Length * 2];
                for (int i = 0; i < audioData.Length; i++)
                {
                    short sample = (short)(audioData[i] * 32767f);
                    pcmData[i * 2] = (byte)(sample & 0xFF);
                    pcmData[i * 2 + 1] = (byte)(sample >> 8);
                }

                // 固定内存以防止GC移动
                GCHandle bufferHandle = GCHandle.Alloc(pcmData, GCHandleType.Pinned);
                _bufferHandles.Enqueue(bufferHandle);

                // 创建WaveHeader
                WaveHeader header = new WaveHeader
                {
                    lpData = bufferHandle.AddrOfPinnedObject(),
                    dwBufferLength = pcmData.Length,
                    dwFlags = 0,
                    dwLoops = 0
                };

                GCHandle headerHandle = GCHandle.Alloc(header, GCHandleType.Pinned);
                _headerHandles.Enqueue(headerHandle);

                // 准备头部
                int result = waveOutPrepareHeader(_waveOut, ref header, Marshal.SizeOf<WaveHeader>());
                if (result == 0)
                {
                    // 播放音频
                    result = waveOutWrite(_waveOut, ref header, Marshal.SizeOf<WaveHeader>());
                    if (result == 0)
                    {
                        Debug.WriteLine($"Windows WaveOut: 成功播放 {audioData.Length} 采样");
                        
                        // 等待播放完成（近似时间）
                        var playTime = (int)(audioData.Length * 1000.0 / 24000.0);
                        await Task.Delay(Math.Max(playTime, 10), cancellationToken);
                    }
                    else
                    {
                        Debug.WriteLine($"WaveOut播放失败，错误代码: {result}");
                    }

                    // 清理头部
                    waveOutUnprepareHeader(_waveOut, ref header, Marshal.SizeOf<WaveHeader>());
                }
                else
                {
                    Debug.WriteLine($"WaveOut准备头部失败，错误代码: {result}");
                }

                // 清理GC句柄
                CleanupHandles();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"播放音频数据失败: {ex.Message}");
            }
        }

        private void WaveOutCallback(IntPtr hdrvr, int uMsg, int dwUser, ref WaveHeader wavhdr, int dwParam2)
        {
            if (uMsg == WOM_DONE)
            {
                Debug.WriteLine("WaveOut播放完成回调");
            }
        }

        private void WaveInCallback(IntPtr hdrvr, int uMsg, int dwUser, ref WaveHeader wavhdr, int dwParam2)
        {
            if (uMsg == WIM_DATA && _isRecording)
            {
                try
                {
                    // 处理录音数据
                    ProcessRecordedData(ref wavhdr);
                    
                    // 重新添加缓冲区到录音队列
                    if (_waveIn != IntPtr.Zero)
                    {
                        waveInAddBuffer(_waveIn, ref wavhdr, Marshal.SizeOf<WaveHeader>());
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"录音回调处理失败: {ex.Message}");
                }
            }
        }

        private void ProcessRecordedData(ref WaveHeader header)
        {
            try
            {
                if (header.dwBytesRecorded > 0)
                {
                    // 将录音的字节数据转换为float数组
                    byte[] recordedBytes = new byte[header.dwBytesRecorded];
                    Marshal.Copy(header.lpData, recordedBytes, 0, (int)header.dwBytesRecorded);
                    
                    // 转换16位PCM到float
                    int sampleCount = recordedBytes.Length / 2;
                    float[] audioData = new float[sampleCount];
                    
                    for (int i = 0; i < sampleCount; i++)
                    {
                        short sample = (short)(recordedBytes[i * 2] | (recordedBytes[i * 2 + 1] << 8));
                        audioData[i] = sample / 32768.0f;
                    }
                    
                    Debug.WriteLine($"录音回调: 处理了 {audioData.Length} 采样");
                    
                    // 触发音频数据事件
                    AudioDataReceived?.Invoke(this, audioData);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"处理录音数据失败: {ex.Message}");
            }
        }

        private void CleanupHandles()
        {
            // 清理一些旧的句柄以防内存泄漏
            while (_bufferHandles.Count > 10)
            {
                if (_bufferHandles.TryDequeue(out var handle))
                {
                    try
                    {
                        handle.Free();
                    }
                    catch { }
                }
            }

            while (_headerHandles.Count > 10)
            {
                if (_headerHandles.TryDequeue(out var handle))
                {
                    try
                    {
                        handle.Free();
                    }
                    catch { }
                }
            }
        }

        public void Dispose()
        {
            Debug.WriteLine("释放Windows音频服务资源");

            lock (_lockObject)
            {
                _isRecording = false;
                _recordingCts?.Cancel();
                _playbackCts?.Cancel();
            }

            try
            {
                _recordingTask?.Wait(1000);
                _playbackTask?.Wait(1000);
            }
            catch { }

            // 关闭WaveIn设备
            if (_waveIn != IntPtr.Zero)
            {
                try
                {
                    waveInStop(_waveIn);
                    waveInReset(_waveIn);
                    waveInClose(_waveIn);
                    _waveIn = IntPtr.Zero;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"关闭录音设备失败: {ex.Message}");
                }
            }

            // 关闭WaveOut设备
            if (_waveOut != IntPtr.Zero)
            {
                try
                {
                    waveOutClose(_waveOut);
                    _waveOut = IntPtr.Zero;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"关闭WaveOut设备失败: {ex.Message}");
                }
            }

            // 清理所有GC句柄
            while (_bufferHandles.TryDequeue(out var bufferHandle))
            {
                try
                {
                    bufferHandle.Free();
                }
                catch { }
            }

            while (_headerHandles.TryDequeue(out var headerHandle))
            {
                try
                {
                    headerHandle.Free();
                }
                catch { }
            }

            _recordingCts?.Dispose();
            _playbackCts?.Dispose();
        }
    }
} 