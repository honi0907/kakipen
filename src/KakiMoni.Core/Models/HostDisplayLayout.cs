namespace KakiMoni.Core.Models;

public sealed class HostDisplayLayout
{
    public string? BackgroundUrl { get; set; }
    public List<HostDisplayCell> Cells { get; set; } = new();

    public bool HasCells => Cells.Count > 0;
}

public sealed class HostDisplayCell
{
    public int? SeatId { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
    public double W { get; set; }
    public double H { get; set; }

    /// <summary>パネル塗り色（ARGB）。null のとき席 ID の既定色。</summary>
    public uint? FillColorArgb { get; set; }

    /// <summary>重なり順（大きいほど前面）。</summary>
    public int ZIndex { get; set; }

    /// <summary>レイアウト編集で位置・サイズ変更をロックする。</summary>
    public bool IsLocked { get; set; }
}
