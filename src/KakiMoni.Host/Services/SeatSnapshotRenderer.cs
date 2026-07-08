using KakiMoni.Core.Display;
using KakiMoni.Core.Drawing;
using KakiMoni_Host.Controls;
using KakiMoni_Host.Drawing;
using Microsoft.Graphics.Canvas;
using Microsoft.UI;
using Windows.Foundation;
using Windows.Storage.Streams;
using Windows.UI;
using System.Runtime.InteropServices.WindowsRuntime;

namespace KakiMoni_Host.Services;

public sealed class SeatSnapshotRenderer
{
    public const float Width = 1920f;
    public const float Height = 1080f;

    private readonly HostImageLoader _images = new();

    public async Task<byte[]> RenderPngAsync(
        SeatCardModel model,
        string? choiceRelativeUrl,
        string saveType,
        string? overlayRelativeUrl,
        string serverBaseUrl,
        bool invertJudgeStrokeColors = false,
        CancellationToken cancellationToken = default)
    {
        CanvasBitmap? bgBitmap = null;
        CanvasBitmap? fillBitmap = null;
        CanvasBitmap? choiceBitmap = null;
        CanvasBitmap? judgeBitmap = null;

        try
        {
            if (!string.IsNullOrWhiteSpace(model.BgImageUrl))
                bgBitmap = await _images.LoadCanvasBitmapAsync(model.BgImageUrl, cancellationToken);

            var isJudge = saveType.Equals("JUDGE", StringComparison.OrdinalIgnoreCase);
            var overlayUrl = isJudge ? overlayRelativeUrl ?? model.OverlayImageUrl : null;
            if (!string.IsNullOrWhiteSpace(overlayUrl))
            {
                var bitmap = await _images.LoadCanvasBitmapAsync(overlayUrl, cancellationToken);
                if (IsFillOverlay(overlayUrl))
                    fillBitmap = bitmap;
                else
                    judgeBitmap = bitmap;
            }

            if (!string.IsNullOrWhiteSpace(choiceRelativeUrl))
                choiceBitmap = await _images.LoadCanvasBitmapAsync(choiceRelativeUrl, cancellationToken);

            var device = CanvasDevice.GetSharedDevice();
            using var target = new CanvasRenderTarget(device, Width, Height, 96);
            using (var session = target.CreateDrawingSession())
            {
                session.Clear(Microsoft.UI.Colors.White);

                if (bgBitmap is not null)
                {
                    var bgScale = Math.Max(Width / bgBitmap.Size.Width, Height / bgBitmap.Size.Height);
                    var dw = bgBitmap.Size.Width * bgScale;
                    var dh = bgBitmap.Size.Height * bgScale;
                    var ox = (Width - dw) * 0.5f;
                    var oy = (Height - dh) * 0.5f;
                    session.DrawImage(bgBitmap, new Rect(ox, oy, dw, dh));
                }

                if (fillBitmap is not null)
                    session.DrawImage(fillBitmap, new Rect(0, 0, Width, Height));

                DrawStrokes(session, model, invertJudgeStrokeColors);

                if (choiceBitmap is not null)
                {
                    var choiceScale = Math.Min(Width / choiceBitmap.Size.Width, Height / choiceBitmap.Size.Height);
                    var cw = choiceBitmap.Size.Width * choiceScale;
                    var ch = choiceBitmap.Size.Height * choiceScale;
                    var cx = (Width - cw) * 0.5f;
                    var cy = (Height - ch) * 0.5f;
                    session.DrawImage(choiceBitmap, new Rect(cx, cy, cw, ch));
                }

                if (judgeBitmap is not null)
                {
                    var judgeScale = Math.Min(Width / judgeBitmap.Size.Width, Height / judgeBitmap.Size.Height);
                    var jw = judgeBitmap.Size.Width * judgeScale;
                    var jh = judgeBitmap.Size.Height * judgeScale;
                    var jx = (Width - jw) * 0.5f;
                    var jy = (Height - jh) * 0.5f;
                    session.DrawImage(judgeBitmap, new Rect(jx, jy, jw, jh));
                }

                var overlayConfig = HostSettingsStore.Load().SeatNameOverlay;
                var overlayStyle = SeatNameOverlayResolver.Resolve(overlayConfig, model.SeatId);
                SeatNameOverlayRenderer.Draw(session, Width, Height, overlayStyle, model.RawSeatName);
            }

            using var stream = new InMemoryRandomAccessStream();
            await target.SaveAsync(stream, CanvasBitmapFileFormat.Png);
            stream.Seek(0);
            var buffer = new byte[stream.Size];
            await stream.ReadAsync(buffer.AsBuffer(), (uint)stream.Size, InputStreamOptions.None);
            return buffer;
        }
        finally
        {
            bgBitmap?.Dispose();
            fillBitmap?.Dispose();
            choiceBitmap?.Dispose();
            judgeBitmap?.Dispose();
        }
    }

    private static void DrawStrokes(CanvasDrawingSession session, SeatCardModel model, bool invertColors)
    {
        var reference = model.Strokes.FirstOrDefault() ?? model.CurrentStroke;
        var srcW = reference?.SrcW ?? 1600;
        var srcH = reference?.SrcH ?? 900;
        if (srcW <= 0) srcW = 1600;
        if (srcH <= 0) srcH = 900;

        var scale = Math.Min(Width / (float)srcW, Height / (float)srcH);
        var ox = (Width - (float)srcW * scale) * 0.5f;
        var oy = (Height - (float)srcH * scale) * 0.5f;

        foreach (var stroke in model.Strokes)
            StrokeDrawHelper.DrawStroke(session, stroke, scale, ox, oy, hex => ParseColor(hex, invertColors));
        if (model.CurrentStroke is not null)
            StrokeDrawHelper.DrawStroke(session, model.CurrentStroke, scale, ox, oy, hex => ParseColor(hex, invertColors));
    }

    private static bool IsFillOverlay(string? url) =>
        !string.IsNullOrWhiteSpace(url)
        && Path.GetFileName(url).Contains("fill", StringComparison.OrdinalIgnoreCase);

    private static Color ParseColor(string hex, bool invert = false)
    {
        try
        {
            if (invert)
                hex = ColorInvertHelper.InvertHex(hex);
            hex = hex.TrimStart('#');
            if (hex.Length == 6)
                return Color.FromArgb(255,
                    Convert.ToByte(hex[..2], 16),
                    Convert.ToByte(hex[2..4], 16),
                    Convert.ToByte(hex[4..6], 16));
        }
        catch { }
        return Microsoft.UI.Colors.Black;
    }
}
