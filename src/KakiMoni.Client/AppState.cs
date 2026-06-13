using KakiMoni_Client.Services;
using KakiMoni.Core.Drawing;
using Microsoft.UI;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace KakiMoni_Client;

public static class AppState
{
    public static string ServerUrl { get; set; } = ClientApiService.DefaultServerUrl;
    public static int SeatId { get; set; } = 1;
    public static string? BgImageUrl { get; set; }
    public static string? ChoiceImageUrl { get; set; }
    public static string? LogoImageUrl { get; set; }
    public static string? OverlayImageUrl { get; set; }
    public static string PlayerName { get; set; } = string.Empty;
    public static string PenColor { get; set; } = ClientSettings.DefaultPalette[0];
    public static double PenSize { get; set; } = 8;
    public static double EraserSize { get; set; } = 24;
    public static List<string> Palette { get; set; } = ClientSettings.DefaultPalette.ToList();
    public static bool WritingFullscreen { get; set; }
    public static bool ExternalOutputEnabled { get; set; }
    public static bool ExternalAutoPlacement { get; set; }
    public static bool ExternalFullscreen { get; set; }
    public static bool ShowConfirmButton { get; set; } = true;
    public static bool ShowClearButton { get; set; } = true;
    public static bool ShowEraserTool { get; set; } = true;

    /// <summary>コンパネ設定: fill 判定表示中に描画色を反転する。</summary>
    public static bool JudgeColorMode { get; set; }

    /// <summary>コンパネ設定: ロックオーバーレイの不透明度（0–100）。</summary>
    public static int LockOverlayOpacityPercent { get; set; } = 80;

    /// <summary>fill 型判定オーバーレイ表示中。</summary>
    public static bool JudgingFillOverlay { get; set; }

    public static void ApplySettings(ClientSettings settings)
    {
        ServerUrl = ClientApiService.NormalizeServerUrl(settings.ServerUrl ?? ClientApiService.DefaultServerUrl);
        SeatId = settings.SeatId;
        BgImageUrl = settings.BgImageUrl;
        PenColor = settings.PenColor;
        PenSize = settings.PenSize;
        EraserSize = settings.EraserSize;
        Palette = settings.Palette.ToList();
        WritingFullscreen = settings.WritingFullscreen;
        ExternalOutputEnabled = settings.ExternalOutputEnabled;
        ExternalAutoPlacement = settings.ExternalAutoPlacement;
        ExternalFullscreen = settings.ExternalFullscreen;
        ShowConfirmButton = settings.ShowConfirmButton;
        ShowClearButton = settings.ShowClearButton;
        ShowEraserTool = settings.ShowEraserTool;
        ResetPenToPaletteStart();
    }

    /// <summary>書き画面の起動時は常にパレット先頭（左端）の色を使う。</summary>
    public static void ResetPenToPaletteStart()
    {
        if (Palette.Count == 0)
        {
            PenColor = ClientSettings.StartupPenColor;
            return;
        }

        PenColor = Palette[0];
    }

    public static void ApplyStartupPen()
    {
        ResetPenToPaletteStart();
    }

    public static ClientSettings ToSettings() => new()
    {
        ServerUrl = ServerUrl,
        SeatId = SeatId,
        BgImageUrl = BgImageUrl,
        PenColor = PenColor,
        PenSize = PenSize,
        EraserSize = EraserSize,
        Palette = Palette.ToList(),
        WritingFullscreen = WritingFullscreen,
        ExternalOutputEnabled = ExternalOutputEnabled,
        ExternalAutoPlacement = ExternalAutoPlacement,
        ExternalFullscreen = ExternalFullscreen,
        ShowConfirmButton = ShowConfirmButton,
        ShowClearButton = ShowClearButton,
        ShowEraserTool = ShowEraserTool
    };

    public static Color PenColorUi() => ParseColor(PenColor);

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
        return Colors.Black;
    }

    public static Color ParseStrokeColor(string hex)
    {
        if (JudgeColorMode && JudgingFillOverlay && !string.IsNullOrWhiteSpace(hex))
            hex = ColorInvertHelper.InvertHex(hex);
        return ParseColor(hex);
    }
}
