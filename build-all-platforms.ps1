# 小智AI助手 - 跨平台构建脚本
# 支持 Android, iOS, Windows, macOS

param(
    [string]$Configuration = "Release",
    [string]$Platform = "All"
)

Write-Host "开始构建小智AI助手跨平台应用..." -ForegroundColor Green
Write-Host "配置: $Configuration" -ForegroundColor Yellow
Write-Host "平台: $Platform" -ForegroundColor Yellow

$ProjectPath = "XiaoZhiSharpMAUI\XiaoZhiSharpMAUI\XiaoZhiSharpMAUI.csproj"

# 检查项目文件是否存在
if (-not (Test-Path $ProjectPath)) {
    Write-Error "项目文件不存在: $ProjectPath"
    exit 1
}

# 清理之前的构建
Write-Host "清理之前的构建..." -ForegroundColor Cyan
dotnet clean $ProjectPath

# 恢复NuGet包
Write-Host "恢复NuGet包..." -ForegroundColor Cyan
dotnet restore $ProjectPath

# 构建函数
function Build-Platform {
    param(
        [string]$TargetFramework,
        [string]$PlatformName
    )
    
    Write-Host "构建 $PlatformName ($TargetFramework)..." -ForegroundColor Magenta
    
    $BuildCommand = "dotnet build `"$ProjectPath`" -f $TargetFramework -c $Configuration"
    
    try {
        Invoke-Expression $BuildCommand
        if ($LASTEXITCODE -eq 0) {
            Write-Host "✅ $PlatformName 构建成功!" -ForegroundColor Green
        } else {
            Write-Host "❌ $PlatformName 构建失败!" -ForegroundColor Red
        }
    }
    catch {
        Write-Host "❌ $PlatformName 构建出错: $_" -ForegroundColor Red
    }
}

# 发布函数
function Publish-Platform {
    param(
        [string]$TargetFramework,
        [string]$PlatformName,
        [string]$RuntimeIdentifier = ""
    )
    
    Write-Host "发布 $PlatformName ($TargetFramework)..." -ForegroundColor Magenta
    
    $PublishCommand = "dotnet publish `"$ProjectPath`" -f $TargetFramework -c $Configuration"
    if ($RuntimeIdentifier) {
        $PublishCommand += " -r $RuntimeIdentifier"
    }
    
    try {
        Invoke-Expression $PublishCommand
        if ($LASTEXITCODE -eq 0) {
            Write-Host "✅ $PlatformName 发布成功!" -ForegroundColor Green
        } else {
            Write-Host "❌ $PlatformName 发布失败!" -ForegroundColor Red
        }
    }
    catch {
        Write-Host "❌ $PlatformName 发布出错: $_" -ForegroundColor Red
    }
}

# 根据平台参数构建
switch ($Platform.ToLower()) {
    "android" {
        Build-Platform "net9.0-android" "Android"
        if ($Configuration -eq "Release") {
            Publish-Platform "net9.0-android" "Android"
        }
    }
    "ios" {
        Build-Platform "net9.0-ios" "iOS"
        if ($Configuration -eq "Release") {
            Publish-Platform "net9.0-ios" "iOS"
        }
    }
    "windows" {
        Build-Platform "net9.0-windows10.0.19041.0" "Windows"
        if ($Configuration -eq "Release") {
            Publish-Platform "net9.0-windows10.0.19041.0" "Windows" "win-x64"
        }
    }
    "macos" {
        Build-Platform "net9.0-maccatalyst" "macOS"
        if ($Configuration -eq "Release") {
            Publish-Platform "net9.0-maccatalyst" "macOS"
        }
    }
    "all" {
        # 构建所有平台
        Build-Platform "net9.0-android" "Android"
        Build-Platform "net9.0-ios" "iOS"
        
        # Windows 平台检查
        if ($IsWindows -or $env:OS -eq "Windows_NT") {
            Build-Platform "net9.0-windows10.0.19041.0" "Windows"
        }
        
        # macOS 平台检查
        if ($IsMacOS -or [System.Runtime.InteropServices.RuntimeInformation]::IsOSPlatform([System.Runtime.InteropServices.OSPlatform]::OSX)) {
            Build-Platform "net9.0-maccatalyst" "macOS"
        }
        
        # 如果是Release配置，也进行发布
        if ($Configuration -eq "Release") {
            Write-Host "开始发布所有平台..." -ForegroundColor Yellow
            Publish-Platform "net9.0-android" "Android"
            Publish-Platform "net9.0-ios" "iOS"
            
            if ($IsWindows -or $env:OS -eq "Windows_NT") {
                Publish-Platform "net9.0-windows10.0.19041.0" "Windows" "win-x64"
            }
            
            if ($IsMacOS -or [System.Runtime.InteropServices.RuntimeInformation]::IsOSPlatform([System.Runtime.InteropServices.OSPlatform]::OSX)) {
                Publish-Platform "net9.0-maccatalyst" "macOS"
            }
        }
    }
    default {
        Write-Error "不支持的平台: $Platform. 支持的平台: Android, iOS, Windows, macOS, All"
        exit 1
    }
}

Write-Host "构建完成!" -ForegroundColor Green
Write-Host "使用方法:" -ForegroundColor Cyan
Write-Host "  .\build-all-platforms.ps1 -Configuration Debug -Platform Android" -ForegroundColor Gray
Write-Host "  .\build-all-platforms.ps1 -Configuration Release -Platform All" -ForegroundColor Gray 