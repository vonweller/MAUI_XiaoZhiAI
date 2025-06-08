using System.Net.NetworkInformation;

namespace XiaoZhiAI_MAUI.Utils;

public static class DeviceInfoHelper
{
    private static readonly Random _random = new();

    public static string GetClientId()
    {
        var clientId = Preferences.Get("client_id", string.Empty);
        if (string.IsNullOrEmpty(clientId))
        {
            clientId = Guid.NewGuid().ToString();
            Preferences.Set("client_id", clientId);
        }
        return clientId;
    }

    public static string GetDeviceMacAddress()
    {
        // 优先从缓存中获取已保存的MAC地址
        var cachedMac = Preferences.Get("CachedMacAddress", string.Empty);
        if (!string.IsNullOrEmpty(cachedMac))
        {
            return cachedMac;
        }

        // 尝试获取真实的MAC地址
        var realMacAddress = GetRealMacAddress();
        if (!string.IsNullOrEmpty(realMacAddress))
        {
            // 缓存真实的MAC地址
            Preferences.Set("CachedMacAddress", realMacAddress);
            return realMacAddress;
        }

        // 如果无法获取真实MAC地址，生成一个固定的设备标识符（非随机）
        var fallbackDeviceId = GetFallbackDeviceId();
        Preferences.Set("CachedMacAddress", fallbackDeviceId);
        return fallbackDeviceId;
    }
    
    private static string? GetRealMacAddress()
    {
        try
        {
            // 获取所有网络接口
            var networkInterfaces = NetworkInterface.GetAllNetworkInterfaces();
            
            // 优先选择以太网接口
            var ethernetInterface = networkInterfaces.FirstOrDefault(ni =>
                ni.NetworkInterfaceType == NetworkInterfaceType.Ethernet &&
                ni.OperationalStatus == OperationalStatus.Up &&
                IsValidMacAddress(ni.GetPhysicalAddress()));

            if (ethernetInterface != null)
            {
                return FormatMacAddress(ethernetInterface.GetPhysicalAddress());
            }

            // 如果没有以太网，选择无线网络接口
            var wirelessInterface = networkInterfaces.FirstOrDefault(ni =>
                (ni.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 ||
                 ni.NetworkInterfaceType == NetworkInterfaceType.AsymmetricDsl ||
                 ni.NetworkInterfaceType == NetworkInterfaceType.Wman ||
                 ni.NetworkInterfaceType == NetworkInterfaceType.Wwanpp) &&
                ni.OperationalStatus == OperationalStatus.Up &&
                IsValidMacAddress(ni.GetPhysicalAddress()));

            if (wirelessInterface != null)
            {
                return FormatMacAddress(wirelessInterface.GetPhysicalAddress());
            }

            // 最后选择任何有效的网络接口
            var validInterface = networkInterfaces.FirstOrDefault(ni =>
                ni.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                ni.OperationalStatus == OperationalStatus.Up &&
                IsValidMacAddress(ni.GetPhysicalAddress()));

            if (validInterface != null)
            {
                return FormatMacAddress(validInterface.GetPhysicalAddress());
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"获取真实MAC地址失败: {ex.Message}");
        }
        
        return null;
    }

    private static bool IsValidMacAddress(PhysicalAddress? physicalAddress)
    {
        if (physicalAddress == null) return false;
        
        var bytes = physicalAddress.GetAddressBytes();
        if (bytes.Length != 6) return false;
        
        // 跳过全零MAC地址
        if (bytes.All(b => b == 0)) return false;
        
        // 跳过全FF MAC地址
        if (bytes.All(b => b == 0xFF)) return false;
        
        return true;
    }

    private static string FormatMacAddress(PhysicalAddress physicalAddress)
    {
        var bytes = physicalAddress.GetAddressBytes();
        return string.Join(":", bytes.Select(b => b.ToString("X2")));
    }
    
    private static string GetFallbackDeviceId()
    {
        try
        {
            // 使用设备的固定特征生成一个稳定的标识符
            var deviceInfo = Microsoft.Maui.Devices.DeviceInfo.Current;
            var deviceName = deviceInfo.Name ?? "Unknown";
            var platform = deviceInfo.Platform.ToString();
            var model = deviceInfo.Model ?? "Unknown";
            var manufacturer = deviceInfo.Manufacturer ?? "Unknown";
            
            // 创建一个基于设备特征的固定字符串
            var deviceString = $"{platform}-{manufacturer}-{model}-{deviceName}";
            
            // 使用SHA256生成固定的哈希值
            using (var sha256 = System.Security.Cryptography.SHA256.Create())
            {
                var hashBytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(deviceString));
                
                // 取前6字节作为MAC地址格式
                var macBytes = hashBytes.Take(6).ToArray();
                
                // 设置本地管理位，确保这是一个有效的私有MAC地址格式
                macBytes[0] = (byte)((macBytes[0] | 0x02) & 0xFE);
                
                return string.Join(":", macBytes.Select(b => b.ToString("X2")));
            }
        }
        catch (Exception)
        {
            // 如果设备信息获取失败，使用预设的固定标识符
            return "02:00:00:00:00:01"; // 固定的私有MAC地址格式
        }
    }

    /// <summary>
    /// 清除缓存的MAC地址，强制重新获取（仅用于调试）
    /// </summary>
    public static void ClearCachedMacAddress()
    {
        Preferences.Remove("CachedMacAddress");
    }
} 