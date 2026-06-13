using KakiMoni_Host.Services;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace KakiMoni_Host.Controls;

public sealed partial class SeatActionBoard : UserControl
{
    private const string SeatIdleBg = "#293548";
    private const string GoFillBg = "#0ea5e9";
    private const string DefaultEdge = "#94a3b8";
    private const string SelectBorder = "#22d3ee";
    private const string JudgeCorrectColor = "#dc2626";
    private const string JudgeIncorrectColor = "#2563eb";
    private const string JudgeCorrectLabel = "正解";
    private const string JudgeIncorrectLabel = "不正解";
    private const double DefaultEdgeThickness = 2;
    private const double SelectedEdgeThickness = 4;

    private readonly Dictionary<int, Border> _selectFrames = new();
    private readonly Dictionary<int, Button> _selectButtons = new();
    private readonly Dictionary<int, Border> _lockFrames = new();
    private readonly Dictionary<int, Button> _lockButtons = new();
    private readonly Dictionary<int, Border> _judgeFrames = new();
    private readonly Dictionary<int, ToggleButton> _judgeButtons = new();
    private readonly Dictionary<int, Border> _blackoutFrames = new();
    private readonly Dictionary<int, Button> _blackoutButtons = new();
    private IReadOnlyDictionary<int, SeatCardModel>? _seats;
    private double _buttonSize;
    private bool _suppressJudgeToggle;

    public event EventHandler<int>? SeatSelectClicked;
    public event EventHandler<int>? SeatLockClicked;
    public event EventHandler<int>? SeatBlackoutClicked;
    public event EventHandler? SelectAllToggleClicked;
    public event EventHandler? LockAllClicked;

    public SeatActionBoard()
    {
        InitializeComponent();
        _buttonSize = CompanelLayoutMetrics.ComputeSeatActionButtonSize(CompanelLayoutMetrics.DesignWidth);
        BuildSelectButtonGrid();
        BuildFramedSeatButtonGrid(LockButtonGrid, _lockFrames, _lockButtons, id => SeatLockClicked?.Invoke(this, id));
        BuildJudgeButtonGrid();
        BuildFramedSeatButtonGrid(BlackoutButtonGrid, _blackoutFrames, _blackoutButtons, id => SeatBlackoutClicked?.Invoke(this, id));
        Loaded += (_, _) => UpdateButtonMetrics();
        SizeChanged += OnBoardSizeChanged;
        ApplySquareSizeToAllButtons();
        ApplySideSeparatorMetrics();
    }

    public void BindSeats(IReadOnlyDictionary<int, SeatCardModel> seats)
    {
        foreach (var model in _seats?.Values ?? [])
            model.PropertyChanged -= OnSeatPropertyChanged;

        _seats = seats;
        foreach (var model in seats.Values)
            model.PropertyChanged += OnSeatPropertyChanged;

        RefreshAll();
    }

    public void RefreshLockAllLabel(bool allLocked) =>
        LockAllButton.Content = allLocked ? "全解除" : "全ロック";

    public void RefreshSelectAllLabel(bool allSelected) =>
        SelectAllToggleButton.Content = allSelected ? "全解除" : "全選択";

    private void OnBoardSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (e.NewSize.Width < 1100)
        {
            RootGrid.ColumnDefinitions[0].Width = new GridLength(1, GridUnitType.Star);
            RootGrid.ColumnDefinitions[1].Width = new GridLength(1, GridUnitType.Star);
        }

        UpdateButtonMetrics();
    }

    private void UpdateButtonMetrics()
    {
        var next = CompanelLayoutMetrics.ComputeSeatActionButtonSize(ActualWidth);
        if (Math.Abs(next - _buttonSize) < 0.5)
            return;

        _buttonSize = next;
        ApplySquareSizeToAllButtons();
        ApplySideSeparatorMetrics();
    }

    private void ApplySideSeparatorMetrics()
    {
        SelectSep.Height = _buttonSize;
        LockSep.Height = _buttonSize;
    }

    private void ApplySquareSizeToAllButtons()
    {
        var margin = new Thickness(CompanelLayoutMetrics.SeatActionButtonMargin);
        var fontSize = _buttonSize >= 64 ? 12 : 11;

        foreach (var frame in AllSeatFrames())
        {
            frame.Width = _buttonSize;
            frame.Height = _buttonSize;
            frame.MinWidth = _buttonSize;
            frame.MinHeight = _buttonSize;
            frame.Margin = margin;
        }

        foreach (var btn in _selectButtons.Values)
        {
            btn.MinWidth = 0;
            btn.MinHeight = 0;
            btn.FontSize = fontSize;
        }

        foreach (var btn in _lockButtons.Values.Concat(_blackoutButtons.Values))
        {
            btn.MinWidth = 0;
            btn.MinHeight = 0;
            btn.FontSize = fontSize;
        }

        foreach (var btn in _judgeButtons.Values)
        {
            btn.MinWidth = 0;
            btn.MinHeight = 0;
            btn.FontSize = _buttonSize >= 64 ? 11 : 9;
        }

        foreach (var btn in SideButtons())
        {
            btn.Width = _buttonSize;
            btn.Height = _buttonSize;
            btn.MinWidth = _buttonSize;
            btn.MinHeight = _buttonSize;
            btn.FontSize = 11;
        }
    }

    private IEnumerable<Border> AllSeatFrames() =>
        _selectFrames.Values
            .Concat(_lockFrames.Values)
            .Concat(_judgeFrames.Values)
            .Concat(_blackoutFrames.Values);

    private IEnumerable<Button> SideButtons()
    {
        yield return SelectAllToggleButton;
        yield return LockAllButton;
    }

    private static Border WrapWithEdgeFrame(FrameworkElement inner, bool innerHitTestVisible = false)
    {
        inner.HorizontalAlignment = HorizontalAlignment.Stretch;
        inner.VerticalAlignment = VerticalAlignment.Stretch;
        inner.IsHitTestVisible = innerHitTestVisible;
        if (inner is Control ctrl)
        {
            ctrl.MinWidth = 0;
            ctrl.MinHeight = 0;
        }

        var host = new Grid();
        host.Children.Add(inner);

        return new Border
        {
            CornerRadius = new CornerRadius(4),
            BorderThickness = new Thickness(DefaultEdgeThickness),
            BorderBrush = Brush(DefaultEdge),
            Background = new SolidColorBrush(Colors.Transparent),
            Child = host
        };
    }

    private void BuildSelectButtonGrid()
    {
        var host = SelectButtonGrid;
        for (var row = 0; row < 2; row++)
            host.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        for (var col = 0; col < 5; col++)
            host.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var style = (Style)Application.Current.Resources["SeatChromeButtonStyle"];
        for (var seatId = 1; seatId <= 10; seatId++)
        {
            var btn = new Button
            {
                Content = $"ID{seatId}",
                Tag = seatId,
                Style = style,
                BorderThickness = new Thickness(0),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                Padding = new Thickness(0)
            };

            var frame = WrapWithEdgeFrame(btn);
            var id = seatId;
            frame.Tapped += (_, e) =>
            {
                SeatSelectClicked?.Invoke(this, id);
                e.Handled = true;
            };
            _selectButtons[seatId] = btn;
            _selectFrames[seatId] = frame;
            Grid.SetRow(frame, (seatId - 1) / 5);
            Grid.SetColumn(frame, (seatId - 1) % 5);
            host.Children.Add(frame);
        }
    }

    private void BuildFramedSeatButtonGrid(
        Grid host,
        Dictionary<int, Border> frameMap,
        Dictionary<int, Button> buttonMap,
        Action<int> onClick)
    {
        for (var row = 0; row < 2; row++)
            host.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        for (var col = 0; col < 5; col++)
            host.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var style = (Style)Application.Current.Resources["SeatChromeButtonStyle"];
        for (var seatId = 1; seatId <= 10; seatId++)
        {
            var btn = new Button
            {
                Content = $"ID{seatId}",
                Tag = seatId,
                Style = style,
                BorderThickness = new Thickness(0),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                Padding = new Thickness(0)
            };

            var frame = WrapWithEdgeFrame(btn);
            var id = seatId;
            frame.Tapped += (_, e) =>
            {
                onClick(id);
                e.Handled = true;
            };
            buttonMap[seatId] = btn;
            frameMap[seatId] = frame;
            Grid.SetRow(frame, (seatId - 1) / 5);
            Grid.SetColumn(frame, (seatId - 1) % 5);
            host.Children.Add(frame);
        }
    }

    private void BuildJudgeButtonGrid()
    {
        var host = JudgeButtonGrid;
        for (var row = 0; row < 2; row++)
            host.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        for (var col = 0; col < 5; col++)
            host.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var style = (Style)Application.Current.Resources["JudgeSeatToggleStyle"];
        for (var seatId = 1; seatId <= 10; seatId++)
        {
            var btn = new ToggleButton
            {
                Content = JudgeIncorrectLabel,
                Tag = seatId,
                Style = style,
                IsChecked = false,
                BorderThickness = new Thickness(0),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                Padding = new Thickness(0)
            };

            var frame = WrapWithEdgeFrame(btn);
            var id = seatId;
            frame.Tapped += (_, e) =>
            {
                OnJudgeSeatTapped(id);
                e.Handled = true;
            };
            _judgeButtons[seatId] = btn;
            _judgeFrames[seatId] = frame;
            Grid.SetRow(frame, (seatId - 1) / 5);
            Grid.SetColumn(frame, (seatId - 1) % 5);
            host.Children.Add(frame);
        }
    }

    private void OnJudgeSeatTapped(int seatId)
    {
        if (_seats is null || !_seats.TryGetValue(seatId, out var model) || !model.IsConnected)
            return;

        var isCorrect = string.Equals(model.JudgeKind, "correct", StringComparison.OrdinalIgnoreCase);
        model.JudgeKind = isCorrect ? "incorrect" : "correct";
        RefreshSeat(seatId);
    }

    private void OnSeatPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (sender is not SeatCardModel model) return;
        if (e.PropertyName is null or nameof(SeatCardModel.IsSelected)
            or nameof(SeatCardModel.IsLocked)
            or nameof(SeatCardModel.IsConnected)
            or nameof(SeatCardModel.WritingBlackout)
            or nameof(SeatCardModel.JudgeKind)
            or nameof(SeatCardModel.OverlayImageUrl)
            or nameof(SeatCardModel.Revealed))
            RefreshSeat(model.SeatId);
    }

    public void RefreshAll()
    {
        if (_seats is null) return;
        foreach (var seatId in _seats.Keys)
            RefreshSeat(seatId);
    }

    public void RefreshSeat(int seatId)
    {
        if (_seats is null || !_seats.TryGetValue(seatId, out var model)) return;

        ApplySelectStyle(_selectFrames[seatId], _selectButtons[seatId], model);
        ApplyLockStyle(_lockFrames[seatId], _lockButtons[seatId], model);
        ApplyJudgeStyle(_judgeFrames[seatId], _judgeButtons[seatId], model);
        ApplyBlackoutStyle(_blackoutFrames[seatId], _blackoutButtons[seatId], model);
    }

    private static void ApplyEdgeFrame(
        Border frame,
        SeatCardModel model,
        string selectedColor,
        bool selected,
        bool thickWhenSelected = true)
    {
        frame.IsHitTestVisible = model.IsConnected;

        if (!model.IsConnected)
        {
            frame.Opacity = 0.4;
            frame.BorderBrush = Brush(DefaultEdge);
            frame.BorderThickness = new Thickness(DefaultEdgeThickness);
            return;
        }

        frame.Opacity = 1.0;
        frame.BorderBrush = Brush(selected ? selectedColor : DefaultEdge);
        frame.BorderThickness = new Thickness(
            selected && thickWhenSelected ? SelectedEdgeThickness : DefaultEdgeThickness);
    }

    /// <summary>正誤用: 接続中は常に正/誤色の縁（太さは一定）。</summary>
    private static void ApplyJudgeEdgeFrame(Border frame, SeatCardModel model, string accentColor)
    {
        frame.IsHitTestVisible = model.IsConnected;

        if (!model.IsConnected)
        {
            frame.Opacity = 0.4;
            frame.BorderBrush = Brush(DefaultEdge);
            frame.BorderThickness = new Thickness(DefaultEdgeThickness);
            return;
        }

        frame.Opacity = 1.0;
        frame.BorderBrush = Brush(accentColor);
        frame.BorderThickness = new Thickness(DefaultEdgeThickness);
    }

    /// <summary>選択 ID: 常時グレー縁 / 選択中=水色縁 / GO 済み=内側水色。</summary>
    private static void ApplySelectStyle(Border frame, Button btn, SeatCardModel model)
    {
        btn.IsEnabled = model.IsConnected;
        btn.IsHitTestVisible = false;
        btn.Foreground = new SolidColorBrush(Colors.White);
        btn.BorderThickness = new Thickness(0);

        ApplyEdgeFrame(frame, model, SelectBorder, model.IsSelected);

        btn.Background = !model.IsConnected || !model.Revealed
            ? Brush(SeatIdleBg)
            : Brush(GoFillBg);
    }

    /// <summary>正誤: 常時赤/青縁（正誤連動）/ 判定 GO 済み=内側赤 or 青。</summary>
    private void ApplyJudgeStyle(Border frame, ToggleButton btn, SeatCardModel model)
    {
        var isCorrect = string.Equals(model.JudgeKind, "correct", StringComparison.OrdinalIgnoreCase);
        var judged = !string.IsNullOrWhiteSpace(model.OverlayImageUrl);
        var accent = isCorrect ? JudgeCorrectColor : JudgeIncorrectColor;
        btn.Content = isCorrect ? JudgeCorrectLabel : JudgeIncorrectLabel;

        _suppressJudgeToggle = true;
        btn.IsChecked = isCorrect;
        _suppressJudgeToggle = false;

        btn.IsEnabled = model.IsConnected;
        btn.IsHitTestVisible = false;
        btn.Foreground = new SolidColorBrush(Colors.White);
        btn.BorderThickness = new Thickness(0);

        ApplyJudgeEdgeFrame(frame, model, accent);

        btn.Background = !model.IsConnected || !judged
            ? Brush(SeatIdleBg)
            : Brush(accent);
    }

    private static void ApplyLockStyle(Border frame, Button btn, SeatCardModel model)
    {
        btn.IsEnabled = model.IsConnected;
        btn.IsHitTestVisible = false;
        btn.Foreground = new SolidColorBrush(Colors.White);
        btn.BorderThickness = new Thickness(0);

        ApplyEdgeFrame(frame, model, DefaultEdge, model.IsLocked, thickWhenSelected: false);

        btn.Background = !model.IsConnected
            ? Brush(SeatIdleBg)
            : model.IsLocked
                ? Brush("#c2660a")
                : Brush(SeatIdleBg);
    }

    private static void ApplyBlackoutStyle(Border frame, Button btn, SeatCardModel model)
    {
        btn.IsEnabled = model.IsConnected;
        btn.IsHitTestVisible = false;
        btn.Foreground = new SolidColorBrush(Colors.White);
        btn.BorderThickness = new Thickness(0);

        ApplyEdgeFrame(frame, model, DefaultEdge, model.WritingBlackout, thickWhenSelected: false);

        btn.Background = !model.IsConnected
            ? Brush(SeatIdleBg)
            : model.WritingBlackout
                ? Brush("#000000")
                : Brush(SeatIdleBg);
    }

    private static SolidColorBrush Brush(string hex)
    {
        hex = hex.TrimStart('#');
        return new SolidColorBrush(Color.FromArgb(255,
            Convert.ToByte(hex[..2], 16),
            Convert.ToByte(hex[2..4], 16),
            Convert.ToByte(hex[4..6], 16)));
    }

    private void OnSelectAllToggleClick(object sender, RoutedEventArgs e) =>
        SelectAllToggleClicked?.Invoke(this, EventArgs.Empty);

    private void OnLockAllClick(object sender, RoutedEventArgs e) =>
        LockAllClicked?.Invoke(this, EventArgs.Empty);
}
