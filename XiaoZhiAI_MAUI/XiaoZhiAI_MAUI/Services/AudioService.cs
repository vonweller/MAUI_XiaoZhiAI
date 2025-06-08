using System.Collections.Concurrent;
using System.Diagnostics;

namespace XiaoZhiAI_MAUI.Services
{
    public class AudioService : IAudioService, IDisposable
    {
        // 音频配置常量
        private const int RECORD_SAMPLE_RATE = 16000;
        private const int PLAY_SAMPLE_RATE = 24000;
        private const int CHANNELS = 1;
        private const int RECORD_FRAME_SIZE = 960;  // 60ms at 16kHz
        private const int PLAY_FRAME_SIZE = 1440;   // 60ms at 24kHz
        private const float VAD_THRESHOLD = 0.02f;
        private const int VAD_SILENCE_FRAMES = 30;  // 约0.5秒
        private const float TTS_COOLDOWN_TIME = 1.5f; // TTS结束后冷却时间

        // 编解码器
        private OpusCodecNative _recordCodec;
        private OpusCodecNative _playCodec;

        // 录音相关
        private bool _isRecording = false;
        private bool _isInitialized = false;
        private CancellationTokenSource _recordingCancellation;
        private Task _recordingTask;

        // 播放相关
        private bool _isPlaying = false;
        private readonly ConcurrentQueue<float[]> _playbackQueue = new();
        private CancellationTokenSource _playbackCancellation;
        private Task _playbackTask;

        // VAD相关
        private bool _useVAD = true;
        private int _currentSilenceFrames = 0;
        private bool _isSpeaking = false;
        private DateTime _lastTtsEndTime = DateTime.MinValue;
        private bool _isInCooldown = false;

        // 监听状态管理（参考Unity逻辑）
        private string _listenState = "stop"; // "start" | "stop"
        public string ListenState => _listenState;

        // 音频缓冲区
        private readonly Queue<float> _recordBuffer = new();
        private readonly object _recordBufferLock = new();

        // 平台特定音频接口
        private IPlatformAudioService _platformAudio;

        // 构造函数注入
        public AudioService(IPlatformAudioService platformAudioService)
        {
            _platformAudio = platformAudioService;
        }

        // 事件
        public event EventHandler<byte[]> AudioDataReady;
        public event EventHandler<bool> RecordingStatusChanged;
        public event EventHandler<bool> PlaybackStatusChanged;
        public event EventHandler<bool> VoiceActivityDetected;
        public event EventHandler<float[]> AudioDataReceived;

        public bool IsRecording => _isRecording;
        public bool IsPlaying => _isPlaying;

        // 设置监听状态（参考Unity逻辑）
        public void SetListenState(string state)
        {
            Debug.WriteLine($"🎧 监听状态变更: {_listenState} -> {state}");
            _listenState = state;
        }

        public async Task InitializeAsync()
        {
            try
            {
                Debug.WriteLine("正在初始化音频服务...");

                // 初始化平台特定音频服务
                if (_platformAudio != null)
                {
                    await _platformAudio.InitializeAsync();
                    _platformAudio.AudioDataReceived += OnAudioDataReceived;
                }

                // 在后台线程中初始化编解码器，避免阻塞UI
                await Task.Run(() =>
                {
                    try
                    {
                        Debug.WriteLine("正在初始化Opus编解码器...");
                        _recordCodec = new OpusCodecNative(RECORD_SAMPLE_RATE, CHANNELS, RECORD_FRAME_SIZE);
                        _playCodec = new OpusCodecNative(PLAY_SAMPLE_RATE, CHANNELS, PLAY_FRAME_SIZE);
                        Debug.WriteLine("Opus编解码器初始化成功");
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Opus编解码器初始化失败: {ex.Message}");
                        Debug.WriteLine($"详细错误: {ex}");
                        // 不抛出异常，允许音频服务在没有编解码器的情况下运行
                        _recordCodec = null;
                        _playCodec = null;
                    }
                });

                // 启动播放任务
                _playbackCancellation = new CancellationTokenSource();
                _playbackTask = PlaybackLoop(_playbackCancellation.Token);

                _isInitialized = true;
                Debug.WriteLine("音频服务初始化完成");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"音频服务初始化失败: {ex.Message}");
                Debug.WriteLine($"详细错误: {ex}");
                // 不抛出异常，避免应用崩溃
                _isInitialized = false;
            }
        }

        public async Task StartRecordingAsync()
        {
            Debug.WriteLine($"🎤 StartRecordingAsync被调用 - 当前状态: 录音={_isRecording}, 播放={_isPlaying}, 冷却={_isInCooldown}");
            
            if (!_isInitialized)
            {
                Debug.WriteLine("初始化音频服务...");
                await InitializeAsync();
            }

            if (_isRecording) 
            {
                Debug.WriteLine("⚠️ 已在录音中，忽略重复启动请求");
                return;
            }

            try
            {
                Debug.WriteLine("🎤 开始录音操作...");
                
                _recordingCancellation?.Cancel();
                _recordingCancellation = new CancellationTokenSource();

                // 清空缓冲区
                lock (_recordBufferLock)
                {
                    var bufferCount = _recordBuffer.Count;
                    _recordBuffer.Clear();
                    Debug.WriteLine($"清空录音缓冲区，原有 {bufferCount} 个采样");
                }

                // 重置VAD状态
                _currentSilenceFrames = 0;
                _isSpeaking = false;
                Debug.WriteLine("重置VAD状态");

                // 启动录音
                if (_platformAudio != null)
                {
                    Debug.WriteLine("调用平台音频服务开始录音...");
                    await _platformAudio.StartRecordingAsync();
                    Debug.WriteLine("平台音频服务录音已启动");
                }
                else
                {
                    Debug.WriteLine("❌ 平台音频服务为null，无法启动录音");
                    return;
                }

                _recordingTask = AudioProcessingLoop(_recordingCancellation.Token);
                _isRecording = true;

                RecordingStatusChanged?.Invoke(this, true);
                Debug.WriteLine("✅ 录音已成功开始");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ 开始录音失败: {ex.Message}");
                Debug.WriteLine($"异常详情: {ex}");
                throw;
            }
        }

        public async Task StopRecordingAsync()
        {
            Debug.WriteLine($"🛑 StopRecordingAsync被调用 - 当前录音状态: {_isRecording}");
            
            if (!_isRecording) 
            {
                Debug.WriteLine("⚠️ 当前未在录音，忽略停止请求");
                return;
            }

            try
            {
                Debug.WriteLine("🛑 停止录音操作...");

                _isRecording = false;
                _recordingCancellation?.Cancel();

                if (_platformAudio != null)
                {
                    Debug.WriteLine("调用平台音频服务停止录音...");
                    await _platformAudio.StopRecordingAsync();
                    Debug.WriteLine("平台音频服务录音已停止");
                }

                if (_recordingTask != null)
                {
                    Debug.WriteLine("等待录音任务结束...");
                    await _recordingTask;
                    Debug.WriteLine("录音任务已结束");
                }

                RecordingStatusChanged?.Invoke(this, false);
                Debug.WriteLine("✅ 录音已成功停止");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ 停止录音失败: {ex.Message}");
                Debug.WriteLine($"异常详情: {ex}");
            }
        }

        public void PlayAudio(byte[] opusData)
        {
            if (opusData == null || opusData.Length == 0) 
            {
                Debug.WriteLine("PlayAudio: 接收到空的音频数据");
                return;
            }

            if (_playCodec == null)
            {
                Debug.WriteLine("PlayAudio: 播放编解码器未初始化，无法播放音频");
                return;
            }

            try
            {
                Debug.WriteLine($"PlayAudio: 开始解码 {opusData.Length} 字节的Opus数据");
                
                // 立即进入TTS播放状态，暂停录音处理
                StartTtsPlayback();
                
                // 解码Opus数据
                var decodedData = _playCodec.Decode(opusData);
                if (decodedData != null && decodedData.Length > 0)
                {
                    Debug.WriteLine($"PlayAudio: 解码成功，得到 {decodedData.Length} 个音频采样");
                    
                    // 验证音频数据
                    float maxSample = 0;
                    float avgSample = 0;
                    for (int i = 0; i < Math.Min(decodedData.Length, 100); i++)
                    {
                        float abs = Math.Abs(decodedData[i]);
                        if (abs > maxSample) maxSample = abs;
                        avgSample += abs;
                    }
                    avgSample /= Math.Min(decodedData.Length, 100);
                    
                    Debug.WriteLine($"PlayAudio: 音频数据分析 - 最大值: {maxSample:F4}, 平均值: {avgSample:F4}");
                    
                    _playbackQueue.Enqueue(decodedData);
                    
                    if (!_isPlaying)
                    {
                        _isPlaying = true;
                        Debug.WriteLine("PlayAudio: 开始播放状态");
                        PlaybackStatusChanged?.Invoke(this, true);
                    }
                }
                else
                {
                    Debug.WriteLine("PlayAudio: 解码失败，未得到有效音频数据");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"播放音频失败: {ex.Message}");
                Debug.WriteLine($"异常详情: {ex}");
            }
        }

        public async Task PlayAudioAsync(float[] audioData)
        {
            if (audioData == null || audioData.Length == 0)
            {
                Debug.WriteLine("PlayAudioAsync: 接收到空的音频数据");
                return;
            }

            try
            {
                Debug.WriteLine($"PlayAudioAsync: 开始播放 {audioData.Length} 个音频采样");
                
                if (_platformAudio != null)
                {
                    await _platformAudio.PlayAudioAsync(audioData);
                    Debug.WriteLine("PlayAudioAsync: 播放完成");
                }
                else
                {
                    Debug.WriteLine("PlayAudioAsync: 警告 - 平台音频服务为null");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"PlayAudioAsync失败: {ex.Message}");
                throw;
            }
        }

        public void ResetPlayback()
        {
            Debug.WriteLine("重置播放缓冲区");
            
            // 清空播放队列
            while (_playbackQueue.TryDequeue(out _)) { }
            
            // 停止播放状态
            if (_isPlaying)
            {
                _isPlaying = false;
                PlaybackStatusChanged?.Invoke(this, false);
            }

            // 结束TTS播放状态
            EndTtsPlayback();
        }

        private void StartTtsPlayback()
        {
            Debug.WriteLine("开始TTS播放，进入防回音模式");
            
            // 立即进入冷却状态，阻止VAD检测
            _isInCooldown = true;
            _lastTtsEndTime = DateTime.Now;
            
            // 清空录音缓冲区，避免播放期间的录音数据被处理
            lock (_recordBufferLock)
            {
                _recordBuffer.Clear();
                Debug.WriteLine("清空录音缓冲区，防止回音");
            }
            
            // 重置VAD状态
            _isSpeaking = false;
            _currentSilenceFrames = 0;
        }

        private void EndTtsPlayback()
        {
            Debug.WriteLine("TTS播放结束，开始冷却期");
            
            // 记录TTS结束时间，开始冷却
            _lastTtsEndTime = DateTime.Now;
            _isInCooldown = true;

            // 清空录音缓冲区，确保没有残留的回音数据
            lock (_recordBufferLock)
            {
                _recordBuffer.Clear();
                Debug.WriteLine("TTS结束后清空录音缓冲区");
            }

            // 延迟结束冷却期
            _ = Task.Run(async () =>
            {
                await Task.Delay(TimeSpan.FromSeconds(TTS_COOLDOWN_TIME));
                _isInCooldown = false;
                Debug.WriteLine("TTS冷却期结束，恢复语音检测");
            });
        }

        private void OnAudioDataReceived(object sender, float[] audioData)
        {
            // 严格检查：只有在真正录音状态下才处理音频数据
            if (!_isRecording || !_isInitialized)
            {
                Debug.WriteLine($"⚠️ 收到音频数据但状态不符 - 录音:{_isRecording}, 初始化:{_isInitialized}, 数据长度: {audioData?.Length ?? 0}");
                return;
            }
            
            if (audioData == null || audioData.Length == 0) 
            {
                Debug.WriteLine("⚠️ 收到空的音频数据，忽略");
                return;
            }

            Debug.WriteLine($"📥 正在录音，收到音频数据: {audioData.Length} 采样");

            // 只触发AudioDataReceived事件给测试页面（聊天页面不需要原始音频数据）
            // 注意：聊天页面只需要编码后的数据（通过AudioDataReady事件）
            AudioDataReceived?.Invoke(this, audioData);

            lock (_recordBufferLock)
            {
                var originalCount = _recordBuffer.Count;
                foreach (var sample in audioData)
                {
                    _recordBuffer.Enqueue(sample);
                }
                Debug.WriteLine($"录音缓冲区: {originalCount} -> {_recordBuffer.Count} 采样");
            }
        }

        private async Task AudioProcessingLoop(CancellationToken cancellationToken)
        {
            var frameBuffer = new float[RECORD_FRAME_SIZE * CHANNELS];
            
            try
            {
                while (!cancellationToken.IsCancellationRequested && _isRecording)
                {
                    bool hasEnoughData = false;
                    
                    // 检查缓冲区是否有足够的数据
                    lock (_recordBufferLock)
                    {
                        if (_recordBuffer.Count >= frameBuffer.Length)
                        {
                            // 提取一帧数据
                            for (int i = 0; i < frameBuffer.Length; i++)
                            {
                                frameBuffer[i] = _recordBuffer.Dequeue();
                            }
                            hasEnoughData = true;
                        }
                    }

                    if (!hasEnoughData)
                    {
                        await Task.Delay(10, cancellationToken); // 等待更多数据
                        continue;
                    }

                    // 防回音检查：只在TTS播放期间阻断音频处理
                    if (_isPlaying)
                    {
                        Debug.WriteLine("音频处理循环: 正在播放TTS，跳过录音数据处理防止回音");
                        continue; // 跳过整个音频处理
                    }

                    // VAD检测
                    if (_useVAD)
                    {
                        bool hasVoice = VoiceActivityDetector.DetectVoiceActivity(frameBuffer, VAD_THRESHOLD);
                        
                        if (hasVoice)
                        {
                            _currentSilenceFrames = 0;
                            if (!_isSpeaking)
                            {
                                // 只在冷却期内进行回音检查
                                if (_isInCooldown)
                                {
                                    var timeSinceTts = DateTime.Now - _lastTtsEndTime;
                                    if (timeSinceTts.TotalSeconds < TTS_COOLDOWN_TIME)
                                    {
                                        Debug.WriteLine($"VAD: TTS冷却期内({timeSinceTts.TotalSeconds:F2}秒)检测到声音，可能是回音，忽略");
                                        continue;
                                    }
                                }

                                _isSpeaking = true;
                                Debug.WriteLine("VAD: 检测到语音开始");
                                VoiceActivityDetected?.Invoke(this, true);
                            }
                        }
                        else
                        {
                            _currentSilenceFrames++;
                            
                            if (_isSpeaking && _currentSilenceFrames > VAD_SILENCE_FRAMES)
                            {
                                _isSpeaking = false;
                                Debug.WriteLine($"VAD: 检测到语音结束，静音帧数: {_currentSilenceFrames}");
                                VoiceActivityDetected?.Invoke(this, false);
                                
                                // 自动停止录音
                                _ = Task.Run(async () => await StopRecordingAsync());
                            }
                        }
                    }

                    // 关键修复：只有在监听状态为"start"时才编码并发送音频数据（参考Unity逻辑）
                    if (_listenState == "start" && _recordCodec != null)
                    {
                        var encodedData = _recordCodec.Encode(frameBuffer);
                        if (encodedData != null)
                        {
                            Debug.WriteLine($"✅ 监听中，发送音频数据: {encodedData.Length} 字节");
                            AudioDataReady?.Invoke(this, encodedData);
                        }
                    }
                    else if (_listenState != "start")
                    {
                        Debug.WriteLine($"⚠️ 监听状态为 '{_listenState}'，跳过音频发送");
                    }
                    else
                    {
                        Debug.WriteLine("录音编解码器未初始化，跳过编码");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"音频处理循环错误: {ex.Message}");
            }
        }

        private async Task PlaybackLoop(CancellationToken cancellationToken)
        {
            Debug.WriteLine("PlaybackLoop: 播放循环已启动");
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    if (_playbackQueue.TryDequeue(out var audioData))
                    {
                        Debug.WriteLine($"PlaybackLoop: 从队列取出音频数据 {audioData.Length} 采样");
                        if (_platformAudio != null)
                        {
                            Debug.WriteLine("PlaybackLoop: 调用平台音频播放");
                            await _platformAudio.PlayAudioAsync(audioData);
                            Debug.WriteLine("PlaybackLoop: 平台音频播放完成");
                        }
                        else
                        {
                            Debug.WriteLine("PlaybackLoop: 警告 - 平台音频服务为null");
                        }
                    }
                    else
                    {
                        // 队列为空时，结束播放状态
                        if (_isPlaying)
                        {
                            _isPlaying = false;
                            Debug.WriteLine("PlaybackLoop: 播放队列为空，停止播放状态");
                            PlaybackStatusChanged?.Invoke(this, false);
                            
                            // 注意：不在这里调用EndTtsPlayback，避免重复调用
                            // EndTtsPlayback由ResetPlayback或播放结束时统一处理
                        }
                        
                        await Task.Delay(10, cancellationToken);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"播放循环错误: {ex.Message}");
                Debug.WriteLine($"异常详情: {ex}");
            }
            Debug.WriteLine("PlaybackLoop: 播放循环已结束");
        }

        public void Dispose()
        {
            Debug.WriteLine("释放音频服务资源");
            
            _recordingCancellation?.Cancel();
            _playbackCancellation?.Cancel();

            _recordCodec?.Dispose();
            _playCodec?.Dispose();
            
            _platformAudio?.Dispose();

            try
            {
                _recordingTask?.Wait(1000);
                _playbackTask?.Wait(1000);
            }
            catch { }

            _recordingCancellation?.Dispose();
            _playbackCancellation?.Dispose();
        }
    }

    /// <summary>
    /// 平台特定音频服务接口
    /// </summary>
    public interface IPlatformAudioService : IDisposable
    {
        Task InitializeAsync();
        Task StartRecordingAsync();
        Task StopRecordingAsync();
        Task PlayAudioAsync(float[] audioData);
        event EventHandler<float[]> AudioDataReceived;
    }
} 