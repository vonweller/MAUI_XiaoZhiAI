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
        // Temporarily disable caching to always get fresh MAC address for testing
        var macAddress = GetRealMacAddress() ?? GenerateRandomMacAddress();
        return macAddress;
    }
    
    private static string? GetRealMacAddress()
    {
        try
        {
            // Get the first active network interface with a valid MAC address
            var networkInterfaces = NetworkInterface.GetAllNetworkInterfaces();
            
            foreach (var networkInterface in networkInterfaces)
            {
                // Skip loopback and non-operational interfaces
                if (networkInterface.NetworkInterfaceType == NetworkInterfaceType.Loopback ||
                    networkInterface.OperationalStatus != OperationalStatus.Up)
                    continue;

                var physicalAddress = networkInterface.GetPhysicalAddress();
                if (physicalAddress != null && physicalAddress.GetAddressBytes().Length == 6)
                {
                    var bytes = physicalAddress.GetAddressBytes();
                    // Skip all-zero MAC addresses
                    if (bytes.All(b => b == 0))
                        continue;
                        
                    return string.Join(":", bytes.Select(b => b.ToString("X2")));
                }
            }
        }
        catch (Exception)
        {
            // Fall back to random MAC if we can't get a real one
        }
        
        return null;
    }
    
    private static string GenerateRandomMacAddress()
    {
        var buffer = new byte[6];
        _random.NextBytes(buffer);
        // Set the locally administered bit and unset the multicast bit
        // to ensure it's a valid private MAC address.
        buffer[0] = (byte)((buffer[0] | 0x02) & 0xFE);
        return string.Join(":", buffer.Select(b => b.ToString("X2")));
    }
} 