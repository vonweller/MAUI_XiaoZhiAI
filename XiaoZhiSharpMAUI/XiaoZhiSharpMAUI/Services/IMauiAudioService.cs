using System;
using System.Threading.Tasks;

namespace XiaoZhiSharpMAUI.Services
{
    public interface IMauiAudioService : IDisposable
    {
        /// <summary>
        /// 是否正在录音
        /// </summary>
        bool IsRecording { get; }

        /// <summary>
        /// 是否正在播放
        /// </summary>
        bool IsPlaying { get; }

        /// <summary>
        /// 录音数据事件
        /// </summary>
        event EventHandler<byte[]>? RecordDataAvailable;

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
        /// <param name="audioData">音频数据</param>
        Task PlayAudioAsync(byte[] audioData);

        /// <summary>
        /// 播放音频流
        /// </summary>
        /// <param name="audioStream">音频流</param>
        Task PlayAudioStreamAsync(Stream audioStream);

        /// <summary>
        /// 停止播放
        /// </summary>
        Task StopPlayingAsync();

        /// <summary>
        /// 设置音量
        /// </summary>
        /// <param name="volume">音量 (0.0 - 1.0)</param>
        void SetVolume(double volume);
    }
} 