using KakiMoni.Core.Models;

namespace KakiMoni.Core.Display;

public readonly record struct SeatNameOverlayRect(double Left, double Top, double Width, double Height);

public static class SeatNameOverlayLayout
{
    public static double ScaleForHeight(double canvasHeight) =>
        canvasHeight <= 0 ? 1.0 : canvasHeight / SeatNameOverlayStyle.ReferenceHeight;

    public static double ScaledFontSize(SeatNameOverlayStyle style, double canvasHeight)
    {
        var scaled = style.FontSize * ScaleForHeight(canvasHeight);
        return Math.Max(8, scaled);
    }

    public static double ScaledMarginX(SeatNameOverlayStyle style, double canvasHeight) =>
        style.MarginX * ScaleForHeight(canvasHeight);

    public static double ScaledMarginY(SeatNameOverlayStyle style, double canvasHeight) =>
        style.MarginY * ScaleForHeight(canvasHeight);

    public static double ScaledBorderThickness(SeatNameOverlayStyle style, double canvasHeight) =>
        style.BorderThickness * ScaleForHeight(canvasHeight);

    public static double ScaledTextStrokeThickness(SeatNameOverlayStyle style, double canvasHeight) =>
        style.TextStrokeThickness * ScaleForHeight(canvasHeight);

    public static double ScaledPadding(SeatNameOverlayStyle style, double canvasHeight) =>
        Math.Max(4, 8 * ScaleForHeight(canvasHeight));

    public static SeatNameOverlayRect Compute(
        SeatNameOverlayStyle style,
        double canvasWidth,
        double canvasHeight,
        double contentWidth,
        double contentHeight)
    {
        if (canvasWidth <= 0 || canvasHeight <= 0)
            return new SeatNameOverlayRect(0, 0, 0, 0);

        var marginX = ScaledMarginX(style, canvasHeight);
        var marginY = ScaledMarginY(style, canvasHeight);
        var width = Math.Min(contentWidth, Math.Max(0, canvasWidth - marginX * 2));
        var height = Math.Min(contentHeight, Math.Max(0, canvasHeight - marginY * 2));

        var left = style.Anchor switch
        {
            SeatNameOverlayAnchor.TopLeft or SeatNameOverlayAnchor.BottomLeft => marginX,
            SeatNameOverlayAnchor.TopCenter or SeatNameOverlayAnchor.BottomCenter => (canvasWidth - width) * 0.5,
            SeatNameOverlayAnchor.TopRight or SeatNameOverlayAnchor.BottomRight => canvasWidth - marginX - width,
            _ => marginX
        };

        var top = style.Anchor switch
        {
            SeatNameOverlayAnchor.TopLeft or SeatNameOverlayAnchor.TopCenter or SeatNameOverlayAnchor.TopRight => marginY,
            SeatNameOverlayAnchor.BottomLeft or SeatNameOverlayAnchor.BottomCenter or SeatNameOverlayAnchor.BottomRight => canvasHeight - marginY - height,
            _ => marginY
        };

        return new SeatNameOverlayRect(left, top, width, height);
    }

    public static bool ShouldShow(SeatNameOverlayStyle style, string? seatName) =>
        style.Enabled && !string.IsNullOrWhiteSpace(seatName);
}
