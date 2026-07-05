using KakiMoni.Core.Drawing;
using KakiMoni.Core.Models;
using KakiMoni_Host.Controls;
using KakiMoni_Host.Services;
using Microsoft.Graphics.Canvas;
using Windows.UI;

namespace KakiMoni_Host.Drawing;

public static class SeatPreviewRenderer
{
    public static void DrawStrokes(
        CanvasDrawingSession session,
        SeatCardModel model,
        float width,
        float height,
        Func<string, Color>? parseColor = null)
    {
        if (width <= 1 || height <= 1 || model is null || !model.IsConnected)
            return;

        var reference = model.Strokes.FirstOrDefault() ?? model.CurrentStroke;
        var srcW = reference?.SrcW ?? 1600;
        var srcH = reference?.SrcH ?? 900;
        if (srcW <= 0) srcW = 1600;
        if (srcH <= 0) srcH = 900;

        var scale = Math.Min(width / (float)srcW, height / (float)srcH);
        var ox = (width - (float)srcW * scale) * 0.5f;
        var oy = (height - (float)srcH * scale) * 0.5f;

        var colorParser = parseColor ?? (hex => ParseStrokeColor(hex, model));
        foreach (var stroke in model.Strokes)
            StrokeDrawHelper.DrawStroke(session, stroke, scale, ox, oy, colorParser);
        if (model.CurrentStroke is not null)
            StrokeDrawHelper.DrawStroke(session, model.CurrentStroke, scale, ox, oy, colorParser);
    }

    public static Color ParseStrokeColor(string hex, SeatCardModel? model)
    {
        var invert = HostSettingsStore.Load().JudgeColorMode
            && ColorInvertHelper.IsFillOverlayUrl(model?.OverlayImageUrl);
        if (invert)
            hex = ColorInvertHelper.InvertHex(hex);
        return ParseColor(hex);
    }

    public static Color ParseColor(string hex)
    {
        try
        {
            hex = hex.TrimStart('#');
            if (hex.Length == 6)
                return Color.FromArgb(255,
                    Convert.ToByte(hex[..2], 16),
                    Convert.ToByte(hex[2..4], 16),
                    Convert.ToByte(hex[4..6], 16));
        }
        catch { }

        return Color.FromArgb(255, 0, 0, 0);
    }

    public static bool IsFillOverlayUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return false;

        var fileName = System.IO.Path.GetFileName(url.Trim('/').Split('/').LastOrDefault() ?? string.Empty);
        return fileName.Contains("fill", StringComparison.OrdinalIgnoreCase);
    }
}
