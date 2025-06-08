using System;

namespace XiaoZhiAI_MAUI.Services
{
    /// <summary>
    /// 语音活动检测器
    /// </summary>
    public static class VoiceActivityDetector
    {
        private const int SMOOTH_FRAMES = 3; // 平滑帧数
        private static float[] _energyHistory = new float[SMOOTH_FRAMES];
        private static int _historyIndex = 0;
        private static bool _historyInitialized = false;
        
        /// <summary>
        /// 检测语音活动
        /// </summary>
        /// <param name="audioData">音频数据</param>
        /// <param name="threshold">检测阈值</param>
        /// <returns>是否检测到语音</returns>
        public static bool DetectVoiceActivity(float[] audioData, float threshold)
        {
            if (audioData == null || audioData.Length == 0)
                return false;
                
            // 计算音频能量（RMS）
            float energy = CalculateRMS(audioData);
            
            // 存储到历史记录中
            _energyHistory[_historyIndex] = energy;
            _historyIndex = (_historyIndex + 1) % SMOOTH_FRAMES;
            
            if (!_historyInitialized && _historyIndex == 0)
            {
                _historyInitialized = true;
            }
            
            // 计算平滑后的能量
            float smoothedEnergy = CalculateSmoothedEnergy();
            
            // 简单的阈值检测
            bool hasVoice = smoothedEnergy > threshold;
            
            return hasVoice;
        }
        
        /// <summary>
        /// 计算音频RMS能量
        /// </summary>
        /// <param name="audioData">音频数据</param>
        /// <returns>RMS能量值</returns>
        private static float CalculateRMS(float[] audioData)
        {
            double sum = 0.0;
            foreach (float sample in audioData)
            {
                sum += sample * sample;
            }
            return (float)Math.Sqrt(sum / audioData.Length);
        }
        
        /// <summary>
        /// 计算平滑后的能量
        /// </summary>
        /// <returns>平滑能量值</returns>
        private static float CalculateSmoothedEnergy()
        {
            if (!_historyInitialized)
            {
                // 历史记录未完全初始化，返回当前能量
                return _energyHistory[(_historyIndex - 1 + SMOOTH_FRAMES) % SMOOTH_FRAMES];
            }
            
            float sum = 0;
            for (int i = 0; i < SMOOTH_FRAMES; i++)
            {
                sum += _energyHistory[i];
            }
            return sum / SMOOTH_FRAMES;
        }
        
        /// <summary>
        /// 重置VAD状态
        /// </summary>
        public static void Reset()
        {
            _historyIndex = 0;
            _historyInitialized = false;
            Array.Clear(_energyHistory, 0, _energyHistory.Length);
        }
    }
} 