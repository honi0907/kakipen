using KakiMoni.Core.Models;
using KakiMoni_Layout.Services;

namespace KakiMoni_Layout;

public static class AppLayoutContext
{
    public static LayoutHubService Hub { get; } = new();
    public static LayoutDisplayOutputService DisplayOutput { get; } = new();
    public static LayoutSeatStateService Seats { get; } = new();

    public static string? ServerBaseUrl { get; set; }

    public static bool JudgeColorMode { get; set; }

    public static SeatNameOverlayConfig SeatNameOverlay { get; set; } = new();

    public static readonly Dictionary<string, HostDisplayLayout> SlotLayouts = new(StringComparer.Ordinal);

    public static HostDisplayLayout GetSlotLayout(string slot) =>
        SlotLayouts.TryGetValue(slot, out var layout) ? layout : new HostDisplayLayout();

    public static void SetSlotLayout(string slot, HostDisplayLayout layout) =>
        SlotLayouts[slot] = layout;
}
