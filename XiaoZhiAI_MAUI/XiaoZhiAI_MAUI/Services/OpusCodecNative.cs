using System;
using System.Runtime.InteropServices;
using System.Diagnostics;

namespace XiaoZhiAI_MAUI.Services
{
    /// <summary>
    /// 真正的Opus编解码器 - 基于原生Opus库
    /// 移植自Dissonance VoIP库，适配MAUI平台
    /// </summary>
    public class OpusCodecNative : IDisposable
    {
        // 根据平台选择正确的库名称
#if ANDROID
        private const string OpusLib = "libopus";
#elif IOS
        private const string OpusLib = "__Internal";
#elif WINDOWS
        private const string OpusLib = "opus";
#elif MACCATALYST
        private const string OpusLib = "libopus";
#else
        private const string OpusLib = "opus";
#endif

        private const CallingConvention OpusCallingConvention = CallingConvention.Cdecl;

        #region Native Methods
        private enum OpusErrors
        {
            Ok = 0,
            BadArg = -1,
            BufferTooSmall = -2,
            InternalError = -3,
            InvalidPacket = -4,
            Unimplemented = -5,
            InvalidState = -6,
            AllocFail = -7
        }

        private enum Application
        {
            Voip = 2048,
            Audio = 2049,
            RestrictedLowLatency = 2051
        }

        private enum Ctl
        {
            SetBitrateRequest = 4002,
            GetBitrateRequest = 4003,
            SetInbandFECRequest = 4012,
            GetInbandFECRequest = 4013,
            SetPacketLossPercRequest = 4014,
            GetPacketLossPercRequest = 4015,
            ResetState = 4028
        }

        [DllImport(OpusLib, CallingConvention = OpusCallingConvention)]
        private static extern IntPtr opus_encoder_create(int Fs, int channels, int application, out int error);

        [DllImport(OpusLib, CallingConvention = OpusCallingConvention)]
        private static extern IntPtr opus_decoder_create(int Fs, int channels, out int error);

        [DllImport(OpusLib, CallingConvention = OpusCallingConvention)]
        private static extern void opus_encoder_destroy(IntPtr encoder);

        [DllImport(OpusLib, CallingConvention = OpusCallingConvention)]
        private static extern void opus_decoder_destroy(IntPtr decoder);

        [DllImport(OpusLib, CallingConvention = OpusCallingConvention)]
        private static extern int opus_encode_float(IntPtr encoder, float[] pcm, int frame_size, byte[] data, int max_data_bytes);

        [DllImport(OpusLib, CallingConvention = OpusCallingConvention)]
        private static extern int opus_decode_float(IntPtr decoder, byte[] data, int len, float[] pcm, int frame_size, int decode_fec);

        [DllImport(OpusLib, CallingConvention = OpusCallingConvention)]
        private static extern int opus_encoder_ctl(IntPtr encoder, int request, int value);

        [DllImport(OpusLib, CallingConvention = OpusCallingConvention)]
        private static extern int opus_decoder_ctl(IntPtr decoder, int request, int value);

        [DllImport(OpusLib, CallingConvention = OpusCallingConvention)]
        private static extern void opus_pcm_soft_clip(IntPtr pcm, int frameSize, int channels, float[] softClipMem);
        #endregion

        private readonly IntPtr _encoder;
        private readonly IntPtr _decoder;
        private readonly int _sampleRate;
        private readonly int _channels;
        private readonly int _frameSize;
        private readonly float[] _softClipMem;
        private bool _disposed;

        /// <summary>
        /// 创建Opus编解码器
        /// </summary>
        /// <param name="sampleRate">采样率 (8000, 12000, 16000, 24000, 48000)</param>
        /// <param name="channels">通道数 (1 或 2)</param>
        /// <param name="frameSize">帧大小（样本数）</param>
        public OpusCodecNative(int sampleRate, int channels, int frameSize)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"OpusCodecNative 构造函数开始: SR={sampleRate}, CH={channels}, FS={frameSize}");
                
                this._sampleRate = sampleRate;
                this._channels = channels;
                this._frameSize = frameSize;

#if ANDROID
                System.Diagnostics.Debug.WriteLine("Android平台 - 开始初始化opus");
                // 继续执行后续的opus初始化代码
#endif

                // 初始化编码器
                int encoderError;
                _encoder = opus_encoder_create(sampleRate, channels, (int)Application.Voip, out encoderError);
                
                if (encoderError != (int)OpusErrors.Ok || _encoder == IntPtr.Zero)
                    throw new Exception($"无法创建Opus编码器: {(OpusErrors)encoderError}");

                // 设置编码器参数
                opus_encoder_ctl(_encoder, (int)Ctl.SetBitrateRequest, 24000); // 24kbps
                opus_encoder_ctl(_encoder, (int)Ctl.SetInbandFECRequest, 1);  // 启用FEC
                opus_encoder_ctl(_encoder, (int)Ctl.SetPacketLossPercRequest, 10); // 10%丢包率

                // 初始化解码器
                int decoderError;
                _decoder = opus_decoder_create(sampleRate, channels, out decoderError);
                
                if (decoderError != (int)OpusErrors.Ok || _decoder == IntPtr.Zero)
                {
                    opus_encoder_destroy(_encoder);
                    throw new Exception($"无法创建Opus解码器: {(OpusErrors)decoderError}");
                }

                _softClipMem = new float[channels];

                System.Diagnostics.Debug.WriteLine("OpusCodecNative 初始化成功");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"OpusCodecNative 构造函数异常: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"详细异常: {ex}");
                // 设置为无效状态
                _encoder = IntPtr.Zero;
                _decoder = IntPtr.Zero;
                // 不重新抛出异常，避免崩溃
            }
        }

        /// <summary>
        /// 编码PCM音频数据为Opus格式
        /// </summary>
        public byte[] Encode(float[] pcmData)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(OpusCodecNative));

            if (pcmData == null || pcmData.Length == 0)
            {
                System.Diagnostics.Debug.WriteLine("PCM数据为空");
                return null;
            }

            if (_encoder == IntPtr.Zero)
            {
                System.Diagnostics.Debug.WriteLine("编码器未初始化，跳过编码");
                return null;
            }

            try
            {
                // 检查输入数据长度
                if (pcmData.Length != _frameSize * _channels)
                {
                    Debug.WriteLine($"PCM数据长度不匹配：{pcmData.Length} vs {_frameSize * _channels}，将进行调整");
                    float[] resizedData = new float[_frameSize * _channels];
                    int copyLength = Math.Min(pcmData.Length, resizedData.Length);
                    Array.Copy(pcmData, resizedData, copyLength);
                    pcmData = resizedData;
                }

                // 应用软限幅
                unsafe
                {
                    fixed (float* pcmPtr = pcmData)
                    {
                        opus_pcm_soft_clip((IntPtr)pcmPtr, _frameSize, _channels, _softClipMem);
                    }
                }

                // 创建编码缓冲区
                byte[] encodedBuffer = new byte[1275]; // 最大Opus帧大小
                int encodedBytes = opus_encode_float(_encoder, pcmData, _frameSize, encodedBuffer, encodedBuffer.Length);

                if (encodedBytes < 0)
                {
                    throw new Exception($"编码失败: {(OpusErrors)encodedBytes}");
                }

                // 返回实际编码的数据
                byte[] result = new byte[encodedBytes];
                Array.Copy(encodedBuffer, result, encodedBytes);
                return result;
            }
            catch (Exception e)
            {
                Debug.WriteLine($"编码异常：{e.Message}");
                return null;
            }
        }

        /// <summary>
        /// 解码Opus数据为PCM格式
        /// </summary>
        public float[] Decode(byte[] opusData, bool decodeFEC = false)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(OpusCodecNative));

            if (opusData == null || opusData.Length == 0)
            {
                System.Diagnostics.Debug.WriteLine("Opus数据为空");
                return null;
            }

            if (_decoder == IntPtr.Zero)
            {
                System.Diagnostics.Debug.WriteLine("解码器未初始化，跳过解码");
                return null;
            }

            try
            {
                // 创建解码缓冲区
                float[] decodedBuffer = new float[_frameSize * _channels];
                int decodedSamples = opus_decode_float(_decoder, opusData, opusData.Length, decodedBuffer, _frameSize, decodeFEC ? 1 : 0);

                if (decodedSamples < 0)
                {
                    throw new Exception($"解码失败: {(OpusErrors)decodedSamples}");
                }

                // 返回实际解码的数据
                if (decodedSamples != _frameSize)
                {
                    float[] result = new float[decodedSamples * _channels];
                    Array.Copy(decodedBuffer, result, result.Length);
                    return result;
                }

                return decodedBuffer;
            }
            catch (Exception e)
            {
                Debug.WriteLine($"解码异常：{e.Message}");
                return null;
            }
        }

        /// <summary>
        /// 重置编解码器状态
        /// </summary>
        public void Reset()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(OpusCodecNative));

            // 重置编码器和解码器状态
            opus_encoder_ctl(_encoder, (int)Ctl.ResetState, 0);
            opus_decoder_ctl(_decoder, (int)Ctl.ResetState, 0);
            Array.Clear(_softClipMem, 0, _softClipMem.Length);
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
                return;

            if (_encoder != IntPtr.Zero)
                opus_encoder_destroy(_encoder);
            if (_decoder != IntPtr.Zero)
                opus_decoder_destroy(_decoder);

            _disposed = true;
            GC.SuppressFinalize(this);
        }

        ~OpusCodecNative()
        {
            Dispose();
        }
    }
} 