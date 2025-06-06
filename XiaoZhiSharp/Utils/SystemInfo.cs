using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;

namespace XiaoZhiSharp.Utils
{
    public class SystemInfo
    {
        /// <summary>
        /// 获取设备标识符（MAC地址或替代标识符）
        /// </summary>
        /// <returns></returns>
        public static string GetMacAddress()
        {
            try
            {
                string macAddresses = "";

                foreach (NetworkInterface nic in NetworkInterface.GetAllNetworkInterfaces())
                {
                    // 仅考虑以太网、无线局域网和虚拟专用网络等常用接口类型
                    if (nic.OperationalStatus == OperationalStatus.Up &&
                        (nic.NetworkInterfaceType == NetworkInterfaceType.Ethernet ||
                         nic.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 ||
                         nic.NetworkInterfaceType == NetworkInterfaceType.Ppp))
                    {
                        PhysicalAddress address = nic.GetPhysicalAddress();
                        byte[] bytes = address.GetAddressBytes();
                        
                        // Android 6.0+ 可能返回 02:00:00:00:00:00，需要检查
                        if (bytes.Length > 0 && !IsDefaultMacAddress(bytes))
                        {
                            for (int i = 0; i < bytes.Length; i++)
                            {
                                macAddresses += bytes[i].ToString("X2");
                                if (i != bytes.Length - 1)
                                {
                                    macAddresses += ":";
                                }
                            }
                            break; // 通常只取第一个符合条件的 MAC 地址
                        }
                    }
                }

                // 如果无法获取有效MAC地址，使用设备标识符的替代方案
                if (string.IsNullOrEmpty(macAddresses) || macAddresses == "02:00:00:00:00:00")
                {
                    macAddresses = GetAlternativeDeviceId();
                }

                return macAddresses.ToLower();
            }
            catch (Exception)
            {
                // 发生异常时使用替代标识符
                return GetAlternativeDeviceId();
            }
        }

        /// <summary>
        /// 检查是否为默认/无效的MAC地址
        /// </summary>
        private static bool IsDefaultMacAddress(byte[] bytes)
        {
            if (bytes.Length == 0) return true;
            
            // 检查是否为全零或02:00:00:00:00:00
            bool allZeros = bytes.All(b => b == 0);
            bool isDefault = bytes.Length >= 6 && bytes[0] == 0x02 && bytes.Skip(1).All(b => b == 0);
            
            return allZeros || isDefault;
        }

        /// <summary>
        /// 获取替代设备标识符
        /// </summary>
        private static string GetAlternativeDeviceId()
        {
            try
            {
                // 使用机器名和环境信息生成唯一标识符
                string machineName = Environment.MachineName ?? "unknown";
                string userName = Environment.UserName ?? "user";
                string osVersion = Environment.OSVersion.ToString();
                
                // 生成基于系统信息的哈希值
                string combined = $"{machineName}-{userName}-{osVersion}";
                var hash = System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(combined));
                
                // 转换为MAC地址格式
                var macFormat = string.Join(":", hash.Take(6).Select(b => b.ToString("X2")));
                return macFormat.ToLower();
            }
            catch
            {
                // 最后的备用方案：使用当前时间戳的哈希
                var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
                var hash = System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(timestamp));
                var macFormat = string.Join(":", hash.Take(6).Select(b => b.ToString("X2")));
                return macFormat.ToLower();
            }
        }
    }
}