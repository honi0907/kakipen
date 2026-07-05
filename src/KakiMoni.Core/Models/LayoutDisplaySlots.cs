namespace KakiMoni.Core.Models;

public static class LayoutDisplaySlots
{
    public const string Slot1 = "slot1";
    public const string Slot2 = "slot2";
    public const string Slot3 = "slot3";

    public static readonly IReadOnlyList<string> All = [Slot1, Slot2, Slot3];

    public static bool IsValid(string? group) =>
        !string.IsNullOrWhiteSpace(group)
        && All.Contains(group.Trim(), StringComparer.Ordinal);
}
