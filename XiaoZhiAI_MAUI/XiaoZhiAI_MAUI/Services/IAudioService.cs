namespace XiaoZhiAI_MAUI.Services
{
    public interface IAudioService
    {
        /// <summary>
        /// 初始化音频服务
        /// </summary>
        Task InitializeAsync();

        /// <summary>
        /// 开始录音
        /// </summary>
        Task StartRecordingAsync();

        /// <summary>
        /// 停止录音
        /// </summary>
        Task StopRecordingAsync();

        /// <summary>
        /// 播放音频数据
        /// </summary>
        void PlayAudio(byte[] opusData);

        /// <summary>
        /// 播放音频数据（异步）
        /// </summary>
        Task PlayAudioAsync(float[] audioData);

        /// <summary>
        /// 重置播放缓冲区
        /// </summary>
        void ResetPlayback();

        /// <summary>
        /// 是否正在录音
        /// </summary>
        bool IsRecording { get; }

        /// <summary>
        /// 是否正在播放
        /// </summary>
        bool IsPlaying { get; }

        /// <summary>
        /// 设置监听状态（参考Unity逻辑）
        /// </summary>
        void SetListenState(string state);

        /// <summary>
        /// 音频数据就绪事件（编码后的Opus数据）
        /// </summary>
        event EventHandler<byte[]> AudioDataReady;

        /// <summary>
        /// 实时音频数据接收事件（原始音频数据）
        /// </summary>
        event EventHandler<float[]> AudioDataReceived;

        /// <summary>
        /// 录音状态变化事件
        /// </summary>
        event EventHandler<bool> RecordingStatusChanged;

        /// <summary>
        /// 播放状态变化事件
        /// </summary>
        event EventHandler<bool> PlaybackStatusChanged;

        /// <summary>
        /// VAD检测事件
        /// </summary>
        event EventHandler<bool> VoiceActivityDetected;

        /// <summary>
        /// 释放资源
        /// </summary>
        void Dispose();
    }
} 