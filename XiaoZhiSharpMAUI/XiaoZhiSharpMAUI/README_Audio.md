# 🎵 MAUI音频服务使用指南

## ✅ 已修复的问题

1. **权限处理错误** - 使用正确的 MAUI `Permissions` 类替代不存在的 IAudioManager 权限方法
2. **API 不匹配** - 基于 Plugin.Maui.Audio 2.1.0 的实际 API 进行实现
3. **依赖注入配置** - 使用正确的服务注册方式

## 🚀 功能特性

- ✅ **跨平台录音** - 支持 Android, iOS, Windows
- ✅ **音频播放** - 支持多种音频格式 (MP3, WAV, AAC等)
- ✅ **权限管理** - 自动处理麦克风权限
- ✅ **音量控制** - 动态调节播放音量
- ✅ **状态监控** - 实时录音/播放状态
- ✅ **事件通知** - 录音数据可用事件

## 📋 使用方法

### 1. 在页面中注入服务

```csharp
@inject IMauiAudioService AudioService
```

### 2. 录音功能

```csharp
// 开始录音 (自动处理权限)
await AudioService.StartRecordingAsync();

// 停止录音
await AudioService.StopRecordingAsync();

// 检查录音状态
bool isRecording = AudioService.IsRecording;
```

### 3. 播放功能

```csharp
// 播放字节数组音频
await AudioService.PlayAudioAsync(audioData);

// 播放音频流
using var stream = new FileStream("audio.mp3", FileMode.Open);
await AudioService.PlayAudioStreamAsync(stream);

// 停止播放
await AudioService.StopPlayingAsync();

// 音量控制 (0.0 - 1.0)
AudioService.SetVolume(0.5);
```

### 4. 事件处理

```csharp
// 订阅录音数据事件
AudioService.RecordDataAvailable += (sender, audioData) => {
    // 处理录音数据
    Console.WriteLine($"收到录音数据: {audioData.Length} 字节");
};
```

## 🎯 测试页面

访问 `/audio-test` 页面可以测试所有音频功能：

- 🎙️ 录音测试
- 🔊 播放测试  
- 🎚️ 音量控制
- 📊 状态监控

## 📱 平台支持

| 功能 | Android | iOS | Windows |
|------|---------|-----|---------|
| 录音 | ✅ | ✅ | ✅ |
| 播放 | ✅ | ✅ | ✅ |
| 权限 | ✅ | ✅ | N/A |
| 音量控制 | ✅ | ✅ | ✅ |

## ⚠️ 注意事项

1. **权限** - 首次录音会自动请求麦克风权限
2. **音频格式** - 不同平台支持的录音格式可能不同
3. **实时数据流** - 当前版本可能不支持实时音频流处理
4. **Windows权限** - Windows平台的麦克风权限总是返回已授权

## 🔧 下一步优化

1. **实时音频流** - 实现音频数据的实时处理
2. **音频格式转换** - 添加不同格式间的转换
3. **录音质量设置** - 支持采样率、比特率等参数配置
4. **音频可视化** - 添加音频波形显示

---

🎉 现在您可以在 MAUI 应用中享受现代化的跨平台音频功能了！ 