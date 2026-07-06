namespace KakiMoni.Core.Display;

/// <summary>
/// 外部出力セルの表示ピクセルに合わせた Bitmap デコード幅を決める。
/// </summary>
public static class DisplayCellImageDecode
{
    public const int MinDecodeWidth = 320;
    public const int MaxDecodeWidth = 3840;

    public static int ResolveDecodeWidth(double logicalWidth, double logicalHeight, double rasterizationScale)
    {
        if (logicalWidth <= 0 && logicalHeight <= 0)
            return 640;

        var scale = rasterizationScale > 0 ? rasterizationScale : 1.0;
        var physical = Math.Max(logicalWidth, logicalHeight) * scale;
        if (physical <= 0)
            return 640;

        return (int)Math.Clamp(Math.Ceiling(physical), MinDecodeWidth, MaxDecodeWidth);
    }
}
