using System.ComponentModel;
using System.Runtime.CompilerServices;
using KakiMoni.Core.Models;

namespace KakiMoni_Layout.Models;

public sealed class SeatDisplayModel : INotifyPropertyChanged
{
    private bool _isConnected;
    private bool _isReconnecting;
    private bool _isLocked;
    private bool _isSelected;
    private bool _writingBlackout;
    private bool _revealed;
    private string _judgeKind = "incorrect";
    private string? _choiceImageUrl;
    private string? _bgImageUrl;
    private string? _overlayImageUrl;
    private string _seatName = string.Empty;
    private readonly List<StrokeData> _strokes = new();
    private StrokeData? _current;

    public int SeatId { get; init; }

    public bool IsConnected
    {
        get => _isConnected;
        set { _isConnected = value; OnPropertyChanged(); OnPropertyChanged(nameof(Status)); }
    }

    public bool IsReconnecting
    {
        get => _isReconnecting;
        set { _isReconnecting = value; OnPropertyChanged(); OnPropertyChanged(nameof(Status)); }
    }

    public bool IsLocked
    {
        get => _isLocked;
        set { _isLocked = value; OnPropertyChanged(); OnPropertyChanged(nameof(Status)); }
    }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value) return;
            _isSelected = value;
            OnPropertyChanged();
        }
    }

    public bool WritingBlackout
    {
        get => _writingBlackout;
        set { _writingBlackout = value; OnPropertyChanged(); }
    }

    public bool Revealed
    {
        get => _revealed;
        set { _revealed = value; OnPropertyChanged(); }
    }

    public string JudgeKind
    {
        get => _judgeKind;
        set
        {
            if (string.Equals(_judgeKind, value, StringComparison.OrdinalIgnoreCase))
                return;
            _judgeKind = value;
            OnPropertyChanged();
        }
    }

    public string Status => IsConnected
        ? (IsLocked ? "接続・ロック" : "接続中")
        : IsReconnecting ? "再接続中" : "未接続";

    public string DisplayName =>
        string.IsNullOrWhiteSpace(_seatName) ? $"ID {SeatId}" : _seatName;

    public string? ChoiceImageUrl
    {
        get => _choiceImageUrl;
        set { _choiceImageUrl = value; OnPropertyChanged(); }
    }

    public string? BgImageUrl
    {
        get => _bgImageUrl;
        set { _bgImageUrl = value; OnPropertyChanged(); }
    }

    public string? OverlayImageUrl
    {
        get => _overlayImageUrl;
        set { _overlayImageUrl = value; OnPropertyChanged(); }
    }

    public IReadOnlyList<StrokeData> Strokes => _strokes;

    public StrokeData? CurrentStroke => _current;

    public event PropertyChangedEventHandler? PropertyChanged;

    public void ApplyState(SeatClientState seat)
    {
        IsConnected = !string.IsNullOrEmpty(seat.ConnectionId);
        if (IsConnected)
            IsReconnecting = false;
        IsLocked = seat.Locked;
        Revealed = seat.Revealed;
        WritingBlackout = seat.WritingBlackout;
        BgImageUrl = string.IsNullOrWhiteSpace(seat.BgImageUrl) ? null : seat.BgImageUrl;
        OverlayImageUrl = string.IsNullOrWhiteSpace(seat.OverlayImageUrl) ? null : seat.OverlayImageUrl;
        _seatName = seat.Name ?? string.Empty;
        OnPropertyChanged(nameof(DisplayName));

        if (!IsConnected)
        {
            IsSelected = false;
            Revealed = false;
            WritingBlackout = false;
        }

        _strokes.Clear();
        _strokes.AddRange(seat.Strokes.Select(CloneStroke));
        _current = null;
        OnPropertyChanged(nameof(Strokes));
        OnPropertyChanged(nameof(CurrentStroke));
    }

    public void BeginStroke(StrokeData stroke)
    {
        _current = CloneStroke(stroke);
        OnPropertyChanged(nameof(CurrentStroke));
    }

    public void AddPoint(StrokePoint point)
    {
        _current?.Points.Add(new StrokePoint { X = point.X, Y = point.Y });
        OnPropertyChanged(nameof(CurrentStroke));
    }

    public void EndStroke()
    {
        if (_current is not null)
        {
            _strokes.Add(_current);
            _current = null;
            OnPropertyChanged(nameof(Strokes));
            OnPropertyChanged(nameof(CurrentStroke));
        }
    }

    public void ClearStrokes()
    {
        _strokes.Clear();
        _current = null;
        OnPropertyChanged(nameof(Strokes));
        OnPropertyChanged(nameof(CurrentStroke));
    }

    private static StrokeData CloneStroke(StrokeData source) => new()
    {
        Tool = source.Tool,
        Color = source.Color,
        Size = source.Size,
        SrcW = source.SrcW,
        SrcH = source.SrcH,
        Points = source.Points.Select(p => new StrokePoint { X = p.X, Y = p.Y }).ToList()
    };

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
