namespace KakiMoni.Core.Network;

public sealed class LanAddressEntry
{
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required string Address { get; init; }
    public bool IsWireless { get; init; }
    public bool IsEthernet { get; init; }

    public string DisplayLabel
    {
        get
        {
            var kind = IsWireless ? "Wi-Fi" : IsEthernet ? "有線" : "LAN";
            var label = string.IsNullOrWhiteSpace(Description) ? Name : Description;
            return $"{kind} / {label} ({Address})";
        }
    }
}
