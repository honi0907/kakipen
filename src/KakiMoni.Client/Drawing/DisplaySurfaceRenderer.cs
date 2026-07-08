using KakiMoni.Core.Models;
using Microsoft.Graphics.Canvas;
using Windows.Foundation;
using Windows.UI;

namespace KakiMoni_Client.Drawing;

public static class DisplaySurfaceRenderer
{
    public static void Draw(
        CanvasDrawingSession session,
        float width,
        float height,
        CanvasBitmap? bgBitmap,
        CanvasBitmap? fillOverlayBitmap,
        IReadOnlyList<StrokeData> strokes,
        StrokeData? currentStroke,
        Func<string, Color> parseColor,
        CanvasBitmap? choiceBitmap = null,
        CanvasBitmap? judgeBitmap = null,
        SeatNameOverlayStyle? seatNameOverlay = null,
        string? seatName = null)
    {
        session.Clear(Microsoft.UI.Colors.White);

        if (bgBitmap is not null)
        {
            var scale = Math.Max(width / bgBitmap.Size.Width, height / bgBitmap.Size.Height);
            var dw = bgBitmap.Size.Width * scale;
            var dh = bgBitmap.Size.Height * scale;
            var ox = (width - dw) * 0.5f;
            var oy = (height - dh) * 0.5f;
            session.DrawImage(bgBitmap, new Rect(ox, oy, dw, dh));
        }

        // fill 判定: BG の上に合成（Electron client-display の多層 background と同じ）
        if (fillOverlayBitmap is not null)
            session.DrawImage(fillOverlayBitmap, new Rect(0, 0, width, height));

        foreach (var stroke in strokes)
            StrokeDrawHelper.DrawStroke(session, stroke, 1f, 0f, 0f, parseColor);
        if (currentStroke is not null)
            StrokeDrawHelper.DrawStroke(session, currentStroke, 1f, 0f, 0f, parseColor);

        if (choiceBitmap is not null)
        {
            var choiceScale = Math.Min(width / choiceBitmap.Size.Width, height / choiceBitmap.Size.Height);
            var cw = choiceBitmap.Size.Width * choiceScale;
            var ch = choiceBitmap.Size.Height * choiceScale;
            var cx = (width - cw) * 0.5f;
            var cy = (height - ch) * 0.5f;
            session.DrawImage(choiceBitmap, new Rect(cx, cy, cw, ch));
        }

        if (judgeBitmap is not null)
        {
            var judgeScale = Math.Min(width / judgeBitmap.Size.Width, height / judgeBitmap.Size.Height);
            var jw = judgeBitmap.Size.Width * judgeScale;
            var jh = judgeBitmap.Size.Height * judgeScale;
            var jx = (width - jw) * 0.5f;
            var jy = (height - jh) * 0.5f;
            session.DrawImage(judgeBitmap, new Rect(jx, jy, jw, jh));
        }

        if (seatNameOverlay is not null)
            SeatNameOverlayRenderer.Draw(session, width, height, seatNameOverlay, seatName);
    }

    /// <summary>描画レイヤーのみ（背景・fill 判定は Image レイヤー側）。</summary>
    public static void DrawStrokesOnly(
        CanvasDrawingSession session,
        float width,
        float height,
        IReadOnlyList<StrokeData> strokes,
        StrokeData? currentStroke,
        Func<string, Color> parseColor)
    {
        session.Clear(Microsoft.UI.Colors.Transparent);

        var reference = strokes.FirstOrDefault() ?? currentStroke;
        var srcW = (float)(reference?.SrcW ?? width);
        var srcH = (float)(reference?.SrcH ?? height);
        if (srcW <= 0) srcW = width;
        if (srcH <= 0) srcH = height;

        var scale = Math.Min(width / srcW, height / srcH);
        var ox = (width - srcW * scale) * 0.5f;
        var oy = (height - srcH * scale) * 0.5f;

        foreach (var stroke in strokes)
            StrokeDrawHelper.DrawStroke(session, stroke, scale, ox, oy, parseColor);
        if (currentStroke is not null)
            StrokeDrawHelper.DrawStroke(session, currentStroke, scale, ox, oy, parseColor);
    }
}
