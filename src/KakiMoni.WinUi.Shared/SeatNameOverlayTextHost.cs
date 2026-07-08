using KakiMoni.Core.Display;
using KakiMoni.Core.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace KakiMoni.WinUi.Shared;

public static class SeatNameOverlayTextHost
{
    private sealed class Host
    {
        public required TextBlock Fill { get; init; }

        public required List<TextBlock> StrokeLayers { get; init; }
    }

    public static void ApplyStroke(Border border, TextBlock fill, SeatNameOverlayStyle style, double canvasHeight)
    {
        var host = EnsureHost(border, fill);
        var strokeOn = style.TextStrokeEnabled;
        var strokeThickness = strokeOn
            ? SeatNameOverlayLayout.ScaledTextStrokeThickness(style, canvasHeight)
            : 0;

        if (!strokeOn || strokeThickness <= 0)
        {
            foreach (var layer in host.StrokeLayers)
                layer.Visibility = Visibility.Collapsed;
            return;
        }

        var sc = SeatNameOverlayColor.ParseArgb(style.TextStrokeColor);
        var strokeBrush = new SolidColorBrush(Color.FromArgb(sc.A, sc.R, sc.G, sc.B));
        var offsets = SeatNameOverlayStrokeOffsets.ForThickness(strokeThickness);
        EnsureStrokeLayerCount(host, offsets.Count);

        for (var i = 0; i < host.StrokeLayers.Count; i++)
        {
            var layer = host.StrokeLayers[i];
            if (i >= offsets.Count)
            {
                layer.Visibility = Visibility.Collapsed;
                continue;
            }

            var (dx, dy) = offsets[i];
            layer.Visibility = Visibility.Visible;
            layer.Text = fill.Text;
            layer.FontFamily = fill.FontFamily;
            layer.FontSize = fill.FontSize;
            layer.FontWeight = fill.FontWeight;
            layer.Foreground = strokeBrush;
            layer.RenderTransform = new TranslateTransform { X = dx, Y = dy };
        }
    }

    private static Host EnsureHost(Border border, TextBlock fill)
    {
        if (border.Tag is Host existing)
            return existing;

        var grid = new Grid();
        var strokeLayers = new List<TextBlock>();

        if (border.Child == fill)
            border.Child = null;

        for (var i = 0; i < 16; i++)
        {
            var layer = new TextBlock
            {
                TextWrapping = fill.TextWrapping,
                FontWeight = fill.FontWeight,
                Visibility = Visibility.Collapsed,
                IsHitTestVisible = false
            };
            Canvas.SetZIndex(layer, 0);
            strokeLayers.Add(layer);
            grid.Children.Add(layer);
        }

        Canvas.SetZIndex(fill, 1);
        grid.Children.Add(fill);
        border.Child = grid;

        var host = new Host { Fill = fill, StrokeLayers = strokeLayers };
        border.Tag = host;
        return host;
    }

    private static void EnsureStrokeLayerCount(Host host, int count)
    {
        while (host.StrokeLayers.Count < count)
        {
            var layer = new TextBlock
            {
                TextWrapping = host.Fill.TextWrapping,
                FontWeight = host.Fill.FontWeight,
                Visibility = Visibility.Collapsed,
                IsHitTestVisible = false
            };
            Canvas.SetZIndex(layer, 0);
            host.StrokeLayers.Add(layer);
            if (host.Fill.Parent is Grid grid)
                grid.Children.Insert(0, layer);
        }
    }
}
