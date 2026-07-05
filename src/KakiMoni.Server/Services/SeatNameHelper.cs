namespace KakiMoni.Server.Services;

public static class SeatNameHelper
{
    public static string GetDisplayName(int seatId, string? name) =>
        string.IsNullOrWhiteSpace(name) ? $"ID {seatId}" : name.Trim();
}
