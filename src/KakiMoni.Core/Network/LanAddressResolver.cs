using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace KakiMoni.Core.Network;

public static class LanAddressResolver
{
    public static IReadOnlyList<LanAddressEntry> ListCandidates()
    {
        var result = new List<LanAddressEntry>();
        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (nic.OperationalStatus != OperationalStatus.Up)
                continue;

            var isWireless = nic.NetworkInterfaceType == NetworkInterfaceType.Wireless80211;
            var isEthernet = nic.NetworkInterfaceType == NetworkInterfaceType.Ethernet
                             || nic.NetworkInterfaceType == NetworkInterfaceType.GigabitEthernet;

            foreach (var uni in nic.GetIPProperties().UnicastAddresses)
            {
                if (uni.Address.AddressFamily != AddressFamily.InterNetwork)
                    continue;
                if (IPAddress.IsLoopback(uni.Address))
                    continue;

                result.Add(new LanAddressEntry
                {
                    Name = nic.Name,
                    Description = nic.Description,
                    Address = uni.Address.ToString(),
                    IsWireless = isWireless,
                    IsEthernet = isEthernet
                });
            }
        }

        return result;
    }

    public static string? ResolveAddress(
        LanAddressPreference preference,
        string? manualAddress,
        bool manualMode)
    {
        if (manualMode)
        {
            var manual = (manualAddress ?? string.Empty).Trim();
            return string.IsNullOrWhiteSpace(manual) ? null : manual;
        }

        var candidates = ListCandidates();
        if (candidates.Count == 0)
            return null;

        return preference switch
        {
            LanAddressPreference.WiFiFirst => candidates.FirstOrDefault(c => c.IsWireless)?.Address
                                                ?? candidates.FirstOrDefault(c => c.IsEthernet)?.Address
                                                ?? candidates[0].Address,
            LanAddressPreference.EthernetFirst => candidates.FirstOrDefault(c => c.IsEthernet)?.Address
                                                  ?? candidates.FirstOrDefault(c => c.IsWireless)?.Address
                                                  ?? candidates[0].Address,
            _ => candidates[0].Address
        };
    }

    public static string BuildChildBaseUrl(int port, string? lanAddress) =>
        string.IsNullOrWhiteSpace(lanAddress)
            ? string.Empty
            : $"http://{lanAddress}:{port}";

    public static string BuildLocalBaseUrl(int port) => $"http://127.0.0.1:{port}";
}
