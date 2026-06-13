using KakiMoni.Core.Models;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Geometry;
using System.Numerics;
using Windows.UI;

namespace KakiMoni_Client.Drawing;

public static class StrokeDrawHelper
{
    private static readonly CanvasStrokeStyle RoundStyle = new()
    {
        StartCap = CanvasCapStyle.Round,
        EndCap = CanvasCapStyle.Round,
        LineJoin = CanvasLineJoin.Round
    };

    public static void DrawStroke(
        CanvasDrawingSession session,
        StrokeData stroke,
        float scale,
        float ox,
        float oy,
        Func<string, Color> parseColor)
    {
        var points = stroke.Points;
        if (points.Count == 0) return;

        var width = Math.Max(1f, (float)stroke.Size * scale);
        var isEraser = string.Equals(stroke.Tool, "eraser", StringComparison.OrdinalIgnoreCase);
        var color = isEraser ? Color.FromArgb(255, 255, 255, 255) : parseColor(stroke.Color);

        if (points.Count == 1)
        {
            var p = points[0];
            session.FillCircle(
                ox + (float)p.X * scale,
                oy + (float)p.Y * scale,
                width * 0.5f,
                color);
            return;
        }

        using var builder = new CanvasPathBuilder(session);
        builder.BeginFigure(
            ox + (float)points[0].X * scale,
            oy + (float)points[0].Y * scale);

        for (var i = 1; i < points.Count; i++)
        {
            var prev = points[i - 1];
            var curr = points[i];
            var px = ox + (float)prev.X * scale;
            var py = oy + (float)prev.Y * scale;
            var cx = ox + (float)curr.X * scale;
            var cy = oy + (float)curr.Y * scale;

            if (i == 1)
            {
                builder.AddLine(cx, cy);
            }
            else
            {
                builder.AddQuadraticBezier(
                    new Vector2(px, py),
                    new Vector2((px + cx) * 0.5f, (py + cy) * 0.5f));
            }
        }

        var last = points[^1];
        builder.AddLine(
            ox + (float)last.X * scale,
            oy + (float)last.Y * scale);
        builder.EndFigure(CanvasFigureLoop.Open);

        using var geometry = CanvasGeometry.CreatePath(builder);
        session.DrawGeometry(geometry, color, width, RoundStyle);
    }
}
