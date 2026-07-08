using KakiMoni.Core.Display;
using KakiMoni.Core.Models;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Text;
using Windows.Foundation;
using Windows.UI;

namespace KakiMoni_Host.Drawing;

public static class SeatNameOverlayRenderer
{
    public static void Draw(
        CanvasDrawingSession session,
        float width,
        float height,
        SeatNameOverlayStyle style,
        string? seatName)
    {
        if (!SeatNameOverlayLayout.ShouldShow(style, seatName))
            return;

        var text = seatName!.Trim();
        var fontSize = (float)SeatNameOverlayLayout.ScaledFontSize(style, height);
        var padding = (float)SeatNameOverlayLayout.ScaledPadding(style, height);
        var border = style.BorderEnabled
            ? (float)SeatNameOverlayLayout.ScaledBorderThickness(style, height)
            : 0f;

        using var layout = new CanvasTextLayout(
            session.Device,
            text,
            new CanvasTextFormat
            {
                FontFamily = style.FontFamily,
                FontSize = fontSize,
                WordWrapping = CanvasWordWrapping.NoWrap
            },
            Math.Max(1, width),
            Math.Max(1, height));

        var contentW = (float)Math.Min(width, layout.LayoutBounds.Width + padding * 2);
        var contentH = (float)Math.Min(height, layout.LayoutBounds.Height + padding * 2);
        var rect = SeatNameOverlayLayout.Compute(style, width, height, contentW, contentH);

        var bg = SeatNameOverlayColor.ParseArgb(style.BackgroundColor, 0);
        if (style.BackgroundEnabled && bg.A > 0)
        {
            session.FillRoundedRectangle(
                new Rect(rect.Left, rect.Top, rect.Width, rect.Height),
                4,
                4,
                Color.FromArgb(bg.A, bg.R, bg.G, bg.B));
        }

        if (style.BorderEnabled && border > 0)
        {
            var bc = SeatNameOverlayColor.ParseArgb(style.BorderColor);
            session.DrawRoundedRectangle(
                new Rect(rect.Left, rect.Top, rect.Width, rect.Height),
                4,
                4,
                Color.FromArgb(bc.A, bc.R, bc.G, bc.B),
                border);
        }

        var tc = SeatNameOverlayColor.ParseArgb(style.TextColor);
        var textX = (float)(rect.Left + padding);
        var textY = (float)(rect.Top + padding);

        if (style.TextStrokeEnabled)
        {
            var strokeWidth = SeatNameOverlayLayout.ScaledTextStrokeThickness(style, height);
            if (strokeWidth > 0)
            {
                var sc = SeatNameOverlayColor.ParseArgb(style.TextStrokeColor);
                var strokeColor = Color.FromArgb(sc.A, sc.R, sc.G, sc.B);
                foreach (var (dx, dy) in SeatNameOverlayStrokeOffsets.ForThickness(strokeWidth))
                {
                    session.DrawTextLayout(
                        layout,
                        textX + (float)dx,
                        textY + (float)dy,
                        strokeColor);
                }
            }
        }

        session.DrawTextLayout(
            layout,
            textX,
            textY,
            Color.FromArgb(tc.A, tc.R, tc.G, tc.B));
    }
}
