using KakiMoni.Core.Network;

namespace KakiMoni_Host.Services;

public static class HostNetworkService
{
    public static string? ResolveLanAddress(HostSettings settings) =>
        LanAddressResolver.ResolveAddress(
            settings.NetworkPreference,
            settings.ManualNetworkAddress,
            settings.NetworkMode == HostNetworkMode.Manual);

    public static string ResolveBindAddress(HostSettings settings)
    {
        if (settings.NetworkMode != HostNetworkMode.Manual)
            return "0.0.0.0";

        var address = ResolveLanAddress(settings);
        return string.IsNullOrWhiteSpace(address) ? "0.0.0.0" : address;
    }

    public static string BuildChildBaseUrl(int port, HostSettings settings)
    {
        var lan = ResolveLanAddress(settings);
        return LanAddressResolver.BuildChildBaseUrl(port, lan);
    }

    public static string FormatChildUrlForDisplay(int port, HostSettings settings)
    {
        var url = BuildChildBaseUrl(port, settings);
        return string.IsNullOrWhiteSpace(url) ? "（LAN IP 未検出）" : url;
    }
}
