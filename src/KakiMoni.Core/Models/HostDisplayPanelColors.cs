namespace KakiMoni.Core.Models;

public static class HostDisplayPanelColors
{
  /// <summary>席 ID 1〜10 の既定パネル色（ARGB）。</summary>
  public static readonly uint[] SeatDefaults =
  [
      0xFF2563EB, // 1 blue
      0xFFDC2626, // 2 red
      0xFF16A34A, // 3 green
      0xFFD97706, // 4 amber
      0xFF9333EA, // 5 purple
      0xFF0891B2, // 6 cyan
      0xFFDB2777, // 7 pink
      0xFF65A30D, // 8 lime
      0xFFEA580C, // 9 orange
      0xFF4F46E5, // 10 indigo
  ];

  public static uint EmptySeatColor => 0xFF64748B;

  public static uint GetDefaultForSeat(int? seatId) =>
      seatId is >= 1 and <= 10 ? SeatDefaults[seatId.Value - 1] : EmptySeatColor;

  public static uint Resolve(uint? fillColorArgb, int? seatId) =>
      fillColorArgb ?? GetDefaultForSeat(seatId);
}
