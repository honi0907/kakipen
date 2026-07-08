using KakiMoni.Core.Display;

using KakiMoni.Core.Models;

using KakiMoni.WinUi.Shared;

using KakiMoni_Host.Services;

using Microsoft.UI.Xaml;

using Microsoft.UI.Xaml.Controls;

using Microsoft.UI.Xaml.Media;

using Windows.UI;



namespace KakiMoni_Host.Controls;



public static class SeatNameOverlayUi

{

    public static SeatNameOverlayConfig GetConfig()

    {

        var config = HostSettingsStore.Load().SeatNameOverlay.Clone();

        config.Normalize();

        return config;

    }



    public static SeatNameOverlayStyle GetStyle(int seatId) =>

        SeatNameOverlayResolver.Resolve(GetConfig(), seatId);



    public static void Apply(

        Border border,

        TextBlock text,

        SeatNameOverlayStyle style,

        string? seatName,

        double canvasWidth,

        double canvasHeight)

    {

        if (!SeatNameOverlayLayout.ShouldShow(style, seatName) || canvasWidth <= 0 || canvasHeight <= 0)

        {

            border.Visibility = Visibility.Collapsed;

            return;

        }



        border.Visibility = Visibility.Visible;

        text.Text = seatName!.Trim();

        text.FontFamily = new FontFamily(style.FontFamily);

        text.FontSize = SeatNameOverlayLayout.ScaledFontSize(style, canvasHeight);



        var fg = SeatNameOverlayColor.ParseArgb(style.TextColor);

        text.Foreground = new SolidColorBrush(Color.FromArgb(fg.A, fg.R, fg.G, fg.B));



        var bg = SeatNameOverlayColor.ParseArgb(style.BackgroundColor, 0);

        border.Background = style.BackgroundEnabled

            ? new SolidColorBrush(Color.FromArgb(bg.A, bg.R, bg.G, bg.B))

            : new SolidColorBrush(Color.FromArgb(0, 0, 0, 0));



        var bc = SeatNameOverlayColor.ParseArgb(style.BorderColor);

        border.BorderBrush = style.BorderEnabled

            ? new SolidColorBrush(Color.FromArgb(bc.A, bc.R, bc.G, bc.B))

            : new SolidColorBrush(Color.FromArgb(0, 0, 0, 0));

        var borderThickness = style.BorderEnabled

            ? SeatNameOverlayLayout.ScaledBorderThickness(style, canvasHeight)

            : 0;

        border.BorderThickness = new Thickness(borderThickness);

        border.Padding = new Thickness(SeatNameOverlayLayout.ScaledPadding(style, canvasHeight));

        border.CornerRadius = new CornerRadius(4);



        var marginX = SeatNameOverlayLayout.ScaledMarginX(style, canvasHeight);

        var marginY = SeatNameOverlayLayout.ScaledMarginY(style, canvasHeight);

        border.HorizontalAlignment = style.Anchor switch

        {

            SeatNameOverlayAnchor.TopLeft or SeatNameOverlayAnchor.BottomLeft => HorizontalAlignment.Left,

            SeatNameOverlayAnchor.TopCenter or SeatNameOverlayAnchor.BottomCenter => HorizontalAlignment.Center,

            _ => HorizontalAlignment.Right

        };

        border.VerticalAlignment = style.Anchor switch

        {

            SeatNameOverlayAnchor.TopLeft or SeatNameOverlayAnchor.TopCenter or SeatNameOverlayAnchor.TopRight => VerticalAlignment.Top,

            _ => VerticalAlignment.Bottom

        };

        border.Margin = new Thickness(marginX, marginY, marginX, marginY);

        border.IsHitTestVisible = false;

        SeatNameOverlayTextHost.ApplyStroke(border, text, style, canvasHeight);

    }

}


