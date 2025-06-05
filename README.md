# 小智AI助手 - 跨平台应用

一个基于.NET MAUI的智能语音助手应用，支持Android、iOS、Windows和macOS平台。

## 🚀 功能特性

- **跨平台支持**: 一套代码，运行在Android、iOS、Windows、macOS
- **智能语音交互**: 支持语音识别和语音合成
- **实时通信**: 基于WebSocket的实时AI对话
- **现代UI**: 使用Blazor WebView构建的现代化用户界面
- **设备适配**: 自动适配不同设备类型和屏幕尺寸

## 📋 系统要求

### 开发环境
- **Visual Studio 2022** (17.8或更高版本)
- **.NET 9.0 SDK**
- **MAUI工作负载**: `dotnet workload install maui`

### 平台特定要求

#### Android
- Android SDK API 24 (Android 7.0) 或更高版本
- Android模拟器或物理设备

#### iOS
- Xcode 15.0 或更高版本
- iOS 15.0 或更高版本
- macOS开发环境（用于iOS开发）

#### Windows
- Windows 10 版本 1903 (Build 18362) 或更高版本
- Windows App SDK

#### macOS
- macOS 12.0 或更高版本
- Xcode 15.0 或更高版本

## 🛠️ 项目结构

```
xiaozhi-sharp/
├── XiaoZhiSharp/                    # 核心类库
│   ├── Services/                    # 服务层
│   ├── Protocols/                   # 通信协议
│   └── Utils/                       # 工具类
├── XiaoZhiSharp_ConsoleApp/         # 控制台应用
├── XiaoZhiSharpMAUI/               # MAUI跨平台项目
│   ├── XiaoZhiSharpMAUI/           # 主应用项目
│   ├── XiaoZhiSharpMAUI.Shared/    # 共享组件
│   ├── XiaoZhiSharpMAUI.Web/       # Web服务器
│   └── XiaoZhiSharpMAUI.Web.Client/ # Web客户端
└── build-all-platforms.ps1         # 构建脚本
```

## 🔧 快速开始

### 1. 克隆项目
```bash
git clone https://github.com/your-repo/xiaozhi-sharp.git
cd xiaozhi-sharp
```

### 2. 安装依赖
```bash
# 安装MAUI工作负载
dotnet workload install maui

# 恢复NuGet包
dotnet restore
```

### 3. 构建项目

#### 使用Visual Studio
1. 打开 `xiaozhi-sharp.sln`
2. 选择目标平台（Android、iOS、Windows等）
3. 按F5运行或Ctrl+Shift+B构建

#### 使用命令行
```powershell
# 构建所有平台
.\build-all-platforms.ps1 -Configuration Release -Platform All

# 构建特定平台
.\build-all-platforms.ps1 -Configuration Debug -Platform Android
```

### 4. 运行应用

#### Android
```bash
dotnet build -f net9.0-android
dotnet run -f net9.0-android
```

#### iOS (需要macOS)
```bash
dotnet build -f net9.0-ios
# 需要通过Xcode或Visual Studio for Mac部署到设备
```

#### Windows
```bash
dotnet build -f net9.0-windows10.0.19041.0
dotnet run -f net9.0-windows10.0.19041.0
```

#### macOS
```bash
dotnet build -f net9.0-maccatalyst
dotnet run -f net9.0-maccatalyst
```

## 📱 平台特定配置

### Android权限
应用需要以下权限：
- `INTERNET` - 网络访问
- `ACCESS_NETWORK_STATE` - 网络状态检查
- `RECORD_AUDIO` - 音频录制
- `MODIFY_AUDIO_SETTINGS` - 音频设置修改
- `WRITE_EXTERNAL_STORAGE` - 外部存储写入
- `READ_EXTERNAL_STORAGE` - 外部存储读取

### iOS权限
需要在Info.plist中配置：
- `NSMicrophoneUsageDescription` - 麦克风使用说明
- `NSAppTransportSecurity` - 网络安全配置

## 🔧 开发指南

### 添加新功能
1. 在`XiaoZhiSharp`核心库中实现业务逻辑
2. 在`XiaoZhiSharpMAUI.Shared`中添加共享UI组件
3. 在平台特定文件夹中添加平台相关代码

### 调试技巧
- 使用Visual Studio的诊断工具监控性能
- 启用MAUI Blazor开发者工具进行UI调试
- 查看输出窗口的日志信息

### 性能优化
- 使用异步编程模式
- 合理管理内存和资源
- 优化图片和资源大小
- 使用编译时绑定提高性能

## 📦 部署

### Android APK
```bash
dotnet publish -f net9.0-android -c Release
```
生成的APK位于：`bin/Release/net9.0-android/publish/`

### iOS IPA (需要Apple开发者账号)
```bash
dotnet publish -f net9.0-ios -c Release
```

### Windows MSIX
```bash
dotnet publish -f net9.0-windows10.0.19041.0 -c Release
```

### macOS APP
```bash
dotnet publish -f net9.0-maccatalyst -c Release
```

## 🐛 故障排除

### 常见问题

#### 1. 构建失败
- 确保安装了最新的.NET 9.0 SDK
- 检查MAUI工作负载是否正确安装
- 清理并重新构建解决方案

#### 2. Android模拟器问题
- 确保Android SDK和模拟器已正确配置
- 检查模拟器的API级别是否支持

#### 3. iOS构建问题
- 确保在macOS上进行iOS开发
- 检查Xcode和iOS SDK版本
- 验证Apple开发者证书配置

#### 4. 权限问题
- 检查平台特定的权限配置
- 确保在运行时请求必要的权限

### 日志和调试
```csharp
// 启用详细日志
builder.Logging.SetMinimumLevel(LogLevel.Debug);

// 查看应用日志
_logger.LogInformation("应用启动成功");
```

## 🤝 贡献

欢迎提交Issue和Pull Request！

1. Fork项目
2. 创建功能分支 (`git checkout -b feature/AmazingFeature`)
3. 提交更改 (`git commit -m 'Add some AmazingFeature'`)
4. 推送到分支 (`git push origin feature/AmazingFeature`)
5. 打开Pull Request

## 📄 许可证

本项目采用MIT许可证 - 查看 [LICENSE](LICENSE) 文件了解详情。

## 📞 联系方式

- 项目主页: [GitHub Repository](https://github.com/your-repo/xiaozhi-sharp)
- 问题反馈: [Issues](https://github.com/your-repo/xiaozhi-sharp/issues)
- 邮箱: your-email@example.com

## 🙏 致谢

- [.NET MAUI](https://docs.microsoft.com/dotnet/maui/) - 跨平台UI框架
- [Blazor](https://blazor.net/) - Web UI框架
- [CommunityToolkit.Maui](https://github.com/CommunityToolkit/Maui) - MAUI社区工具包

---

**让AI助手在每个平台上都能完美运行！** 🎯 