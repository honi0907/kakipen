namespace KakiMoni_Host.Services;

/// <summary>本番解像度 1280×960 を基準としたコンパネ寸法。</summary>
public static class CompanelLayoutMetrics
{
    public const int DesignWidth = 1280;
    public const int DesignHeight = 960;

    public const double GlobalButtonSize = 72;

    public const double GlobalButtonWidth = GlobalButtonSize;
    public const double GlobalButtonHeight = GlobalButtonSize;

    public const double SeatActionButtonMargin = 2;
    public const double SeatActionSideColumnWidth = 82;
    public const double SeatActionPanelChrome = 26;
    public const double SeatActionButtonMinSize = 72;
    public const double SeatActionButtonMaxSize = 98;
    /// <summary>席プレビュー領域確保のためアクションボタンを縮小する倍率。</summary>
    public const double SeatActionButtonSizeScale = 0.7;

    /// <summary>5列グリッド＋サイドボタン列に収まる正方形ボタンサイズ。</summary>
    public static double ComputeSeatActionButtonSize(double boardWidth)
    {
        if (boardWidth <= 0)
            return Math.Floor(88 * SeatActionButtonSizeScale);

        var panelW = (boardWidth - 8 - 18) / 2 - SeatActionPanelChrome;
        var gridW = panelW - SeatActionSideColumnWidth;
        var size = Math.Floor((gridW - 10 * SeatActionButtonMargin) / 5);
        var clamped = Math.Clamp(size, SeatActionButtonMinSize, SeatActionButtonMaxSize);
        return Math.Floor(clamped * SeatActionButtonSizeScale);
    }

    public const double SeatGridRowGap = 8;
    public const double SeatGridColumnGap = 8;
    public const double SeatGridPadding = 8;

    public const double SeatCardHeaderHeight = 22;
    public const double SeatCardBorderPadding = 10;
    public const double PreviewAspectRatio = 16.0 / 9.0;

    public static double ComputeSeatCellWidth(double seatsAreaWidth) =>
        Math.Max(100, (seatsAreaWidth - SeatGridColumnGap * 4) / 5);

    /// <summary>16:9 プレビュー＋ヘッダー（幅基準・比率固定）。</summary>
    public static (double Width, double Height) ComputeSeatCellSize(double seatsAreaWidth, double seatsAreaHeight)
    {
        var idealW = ComputeSeatCellWidth(seatsAreaWidth);
        var idealH = SeatCardHeaderHeight + idealW / PreviewAspectRatio + SeatCardBorderPadding;
        return (idealW, idealH);
    }

    public static double SeatActionGridRowGap =>
        SeatActionButtonMargin * 2;
}
