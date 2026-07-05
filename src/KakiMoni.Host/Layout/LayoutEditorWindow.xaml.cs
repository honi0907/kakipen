using KakiMoni.Core.Models;
using KakiMoni_Host.Services;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.Foundation;
using Windows.System;
using Windows.UI;

namespace KakiMoni_Host.Layout;

public sealed partial class LayoutEditorWindow : Window
{
    private const double RefW = 1920;
    private const double RefH = 1080;
    private const string ResizeTag = "resize";
    private const double MinPanelPx = 24;
    private const double MarginGuideMinLength = 2;
    private const double MarginGuideLabelFontSize = 28;
    private const double MarginGuideVerticalLabelOffset = 16;

    private static readonly SolidColorBrush MarginGuideBrush =
        new(Color.FromArgb(255, 251, 191, 36));

    private static readonly SolidColorBrush MarginGuideInputTransparentBrush =
        new(Color.FromArgb(0, 0, 0, 0));

    private readonly HostImageLoader _images = new();
    private readonly List<EditorPanel> _panels = new();
    private readonly Dictionary<int, Border> _panelBorders = new();
    private int _nextPanelId = 1;
    private int? _selectedPanelId;
    private bool _syncingUi;
    private double _linkAspectRatio = 16.0 / 9.0;

    private EditorPanel? _dragPanel;
    private Point _dragStart;
    private double _dragStartX;
    private double _dragStartY;
    private uint? _dragPointerId;

    private EditorPanel? _resizePanel;
    private Point _resizeStart;
    private double _resizeStartWPx;
    private double _resizeStartHPx;
    private double _resizeAspectRatio;
    private uint? _resizePointerId;

    private string? _backgroundUrl;
    private bool _syncingPresetUi;
    private bool _syncingGridUi;
    private bool _syncingMarginGuideUi;
    private bool _syncingColorUi;
    private bool _syncingPanelListUi;
    private bool _syncingPanelLockUi;
    private bool _marginInputsInitialized;

    private readonly Dictionary<MarginGuideEdge, TextBox> _marginInputBoxes = new();

    private enum MarginGuideEdge
    {
        Left,
        Top,
        Right,
        Bottom
    }

    private sealed class EditorPanel
    {
        public int Id { get; init; }
        public int? SeatId { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public double W { get; set; }
        public double H { get; set; }
        public uint FillColorArgb { get; set; } = HostDisplayPanelColors.EmptySeatColor;
        public int ZIndex { get; set; }
        public bool IsLocked { get; set; }
    }

    public LayoutEditorWindow()
    {
        InitializeComponent();
        Title = "KakiMoni レイアウト編集";
        PopulateSeatCombo();
        SelectMatchingGridPreset(120);

        EditorCanvas.PointerMoved += OnCanvasPointerMoved;
        EditorCanvas.PointerReleased += OnCanvasPointerReleased;
        EditorCanvas.PointerCanceled += OnCanvasPointerReleased;
        EditorCanvas.PointerCaptureLost += OnCanvasPointerCaptureLost;

        RootLayoutGrid.PreviewKeyDown += OnRootPreviewKeyDown;
        CanvasHostGrid.IsTabStop = true;
        EditorCanvas.IsTabStop = true;

        if (Content is FrameworkElement root)
            root.Loaded += OnRootLoaded;

        UpdateSnapToGridAvailability();
        BuildPresetColorSwatches();
        ConfigureWindowChrome();
    }

    private void ConfigureWindowChrome()
    {
        try
        {
            var appWindow = HostDisplayWindowLayout.GetAppWindow(this);
            appWindow.Resize(new Windows.Graphics.SizeInt32(1400, 860));
            if (appWindow.Presenter is OverlappedPresenter presenter)
            {
                presenter.IsResizable = true;
                presenter.IsMaximizable = true;
                presenter.PreferredMinimumWidth = 1100;
                presenter.PreferredMinimumHeight = 640;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[LayoutEditorWindow] resize failed: {ex}");
        }
    }

    private void OnRootLoaded(object sender, RoutedEventArgs e)
    {
        if (Content is FrameworkElement root)
            root.Loaded -= OnRootLoaded;

        Canvas.SetZIndex(SidebarHost, 1);

        DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
        {
            try
            {
                LoadFromStore();
                RefreshPresetList();
                RebuildGridOverlay();
                _ = LoadBackgroundsAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LayoutEditorWindow] init failed: {ex}");
            }
        });
    }

    private void PopulateSeatCombo()
    {
        SeatComboBox.Items.Clear();
        SeatComboBox.Items.Add(new ComboBoxItem { Content = "（なし）", Tag = null });
        for (var i = 1; i <= 10; i++)
            SeatComboBox.Items.Add(new ComboBoxItem { Content = $"ID {i}", Tag = i });
    }

    private async Task LoadBackgroundsAsync()
    {
        if (!AppHostContext.Server.IsRunning)
            return;

        try
        {
            var entries = await HostApiService.GetBackgroundsAsync(AppHostContext.Server.LocalBaseUrl);
            BgComboBox.Items.Clear();
            BgComboBox.Items.Add(new ComboBoxItem { Content = "なし", Tag = null });
            foreach (var entry in entries)
            {
                BgComboBox.Items.Add(new ComboBoxItem
                {
                    Content = entry.FileName,
                    Tag = entry.RelativeUrl
                });
            }

            if (!string.IsNullOrWhiteSpace(_backgroundUrl))
                SyncBgComboSelection();
        }
        catch { }
    }

    private void SyncBgComboSelection()
    {
        if (string.IsNullOrWhiteSpace(_backgroundUrl))
        {
            BgComboBox.SelectedItem = BgComboBox.Items.Count > 0 ? BgComboBox.Items[0] : null;
            return;
        }

        foreach (ComboBoxItem item in BgComboBox.Items)
        {
            if (item.Tag is string url && string.Equals(url, _backgroundUrl, StringComparison.OrdinalIgnoreCase))
            {
                BgComboBox.SelectedItem = item;
                return;
            }
        }
    }

    private void LoadFromStore()
    {
        LoadFromLayout(HostDisplayLayoutStore.Load());
    }

    private void LoadFromLayout(HostDisplayLayout layout)
    {
        _backgroundUrl = layout.BackgroundUrl;
        _panels.Clear();
        _selectedPanelId = null;
        _nextPanelId = 1;

        foreach (var cell in layout.Cells)
        {
            _panels.Add(new EditorPanel
            {
                Id = _nextPanelId++,
                SeatId = cell.SeatId,
                X = cell.X,
                Y = cell.Y,
                W = cell.W,
                H = cell.H,
                FillColorArgb = HostDisplayPanelColors.Resolve(cell.FillColorArgb, cell.SeatId),
                ZIndex = cell.ZIndex,
                IsLocked = cell.IsLocked
            });
        }

        NormalizePanelZOrder();

        RebuildCanvas();
        RebuildPanelSelectCombo();
        SelectPanel(null);
        _ = UpdateEditorBackgroundAsync();
        if (BgComboBox.Items.Count > 0)
            SyncBgComboSelection();
    }

    private HostDisplayLayout BuildCurrentLayout() => new()
    {
        BackgroundUrl = _backgroundUrl,
        Cells = _panels.Select(p => new HostDisplayCell
        {
            SeatId = p.SeatId,
            X = p.X,
            Y = p.Y,
            W = p.W,
            H = p.H,
            FillColorArgb = p.FillColorArgb,
            ZIndex = p.ZIndex,
            IsLocked = p.IsLocked
        }).ToList()
    };

    private void RefreshPresetList(string? selectName = null)
    {
        _syncingPresetUi = true;
        try
        {
            PresetComboBox.Items.Clear();
            foreach (var name in HostDisplayLayoutPresetStore.List())
                PresetComboBox.Items.Add(name);

            if (!string.IsNullOrWhiteSpace(selectName))
            {
                PresetComboBox.SelectedItem = selectName;
                PresetNameBox.Text = selectName;
            }
        }
        finally
        {
            _syncingPresetUi = false;
        }
    }

    private void OnPresetComboChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_syncingPresetUi || PresetComboBox.SelectedItem is not string name)
            return;

        PresetNameBox.Text = name;
    }

    private async void OnSavePresetClick(object sender, RoutedEventArgs e)
    {
        if (!HostDisplayLayoutPresetStore.TryNormalizeName(PresetNameBox.Text, out var name))
        {
            await ShowDialogAsync("プリセット保存", "プリセット名を入力してください。");
            return;
        }

        if (HostDisplayLayoutPresetStore.Exists(name))
        {
            var confirm = new ContentDialog
            {
                Title = "プリセット上書き",
                Content = $"「{name}」を上書きしますか？",
                PrimaryButtonText = "上書き",
                CloseButtonText = "キャンセル",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = Content.XamlRoot
            };

            if (await confirm.ShowAsync() != ContentDialogResult.Primary)
                return;
        }

        HostDisplayLayoutPresetStore.Save(name, BuildCurrentLayout());
        RefreshPresetList(name);
    }

    private async void OnLoadPresetClick(object sender, RoutedEventArgs e)
    {
        var name = PresetComboBox.SelectedItem as string;
        if (!HostDisplayLayoutPresetStore.TryNormalizeName(name ?? PresetNameBox.Text, out var normalized))
        {
            await ShowDialogAsync("プリセット読込", "読み込むプリセットを選択するか、名前を入力してください。");
            return;
        }

        var layout = HostDisplayLayoutPresetStore.Load(normalized);
        if (layout is null)
        {
            await ShowDialogAsync("プリセット読込", $"「{normalized}」が見つかりません。");
            return;
        }

        LoadFromLayout(layout);
        PresetNameBox.Text = normalized;
        RefreshPresetList(normalized);
    }

    private async Task ShowDialogAsync(string title, string message)
    {
        var dialog = new ContentDialog
        {
            Title = title,
            Content = message,
            CloseButtonText = "OK",
            XamlRoot = Content.XamlRoot
        };
        await dialog.ShowAsync();
    }

    private void RebuildCanvas()
    {
        EditorCanvas.Children.Clear();
        _panelBorders.Clear();

        foreach (var panel in _panels.OrderBy(p => p.ZIndex).ThenBy(p => p.Id))
        {
            var border = CreatePanelBorder(panel);
            _panelBorders[panel.Id] = border;
            EditorCanvas.Children.Add(border);
            UpdatePanelVisual(panel);
        }
    }

    private void NormalizePanelZOrder()
    {
        var ordered = _panels.OrderBy(p => p.ZIndex).ThenBy(p => p.Id).ToList();
        for (var i = 0; i < ordered.Count; i++)
            ordered[i].ZIndex = i;
    }

    private void ApplyPanelZOrder()
    {
        foreach (var panel in _panels)
        {
            if (_panelBorders.TryGetValue(panel.Id, out var border))
                Canvas.SetZIndex(border, panel.ZIndex);
        }
    }

    private void SyncPanelZOrderUi(EditorPanel? panel)
    {
        if (panel is null)
        {
            PanelZOrderSection.Opacity = 0.45;
            BringForwardButton.IsEnabled = false;
            SendBackwardButton.IsEnabled = false;
            PanelZOrderHintText.Text = "パネルを選択してください";
            return;
        }

        PanelZOrderSection.Opacity = 1;
        var minZ = _panels.Min(p => p.ZIndex);
        var maxZ = _panels.Max(p => p.ZIndex);
        BringForwardButton.IsEnabled = panel.ZIndex < maxZ;
        SendBackwardButton.IsEnabled = panel.ZIndex > minZ;
        PanelZOrderHintText.Text = $"重なり: {panel.ZIndex + 1} / {_panels.Count}（大きいほど前面）";
    }

    private void OnBringForwardClick(object sender, RoutedEventArgs e) =>
        MoveSelectedPanelZOrder(forward: true);

    private void OnSendBackwardClick(object sender, RoutedEventArgs e) =>
        MoveSelectedPanelZOrder(forward: false);

    private void MoveSelectedPanelZOrder(bool forward)
    {
        if (_selectedPanelId is null)
            return;

        var panel = _panels.FirstOrDefault(p => p.Id == _selectedPanelId);
        if (panel is null)
            return;

        var neighbor = forward
            ? _panels.Where(p => p.ZIndex > panel.ZIndex).OrderBy(p => p.ZIndex).FirstOrDefault()
            : _panels.Where(p => p.ZIndex < panel.ZIndex).OrderByDescending(p => p.ZIndex).FirstOrDefault();

        if (neighbor is null)
            return;

        (panel.ZIndex, neighbor.ZIndex) = (neighbor.ZIndex, panel.ZIndex);
        ApplyPanelZOrder();
        SyncPanelZOrderUi(panel);
        RefreshPanelSelectCombo();
    }

    private int GetGridStepPx()
    {
        if (GridPresetComboBox?.SelectedItem is ComboBoxItem item &&
            TryParseGridPresetTag(item.Tag, out var preset) &&
            preset > 0)
            return Math.Clamp(preset, 8, 480);

        if (GridStepBox is null || double.IsNaN(GridStepBox.Value))
            return 120;

        var step = (int)Math.Round(GridStepBox.Value);
        return Math.Clamp(step, 8, 480);
    }

    private bool IsSnapToGridEnabled() =>
        ShowGridCheckBox?.IsChecked == true && SnapToGridCheckBox?.IsChecked == true;

    private void UpdateSnapToGridAvailability()
    {
        if (SnapToGridCheckBox is null || ShowGridCheckBox is null)
            return;

        SnapToGridCheckBox.IsEnabled = ShowGridCheckBox.IsChecked == true;
    }

    private static double SnapPx(double px, int step) =>
        step <= 0 ? px : Math.Round(px / step) * step;

    private void SnapPanelGeometry(EditorPanel panel)
    {
        if (!IsSnapToGridEnabled())
            return;

        var step = GetGridStepPx();
        var wPx = ToPxW(panel.W);
        var hPx = ToPxH(panel.H);
        var xPx = SnapPx(ToPxX(panel.X), step);
        var yPx = SnapPx(ToPxY(panel.Y), step);
        ApplyPixelGeometry(panel, xPx, yPx, wPx, hPx, applySnap: false);
    }

    private void RebuildGridOverlay()
    {
        if (GridOverlayCanvas is null || ShowGridCheckBox is null || GridStepBox is null)
            return;

        try
        {
            GridOverlayCanvas.Children.Clear();

            if (ShowGridCheckBox.IsChecked != true)
            {
                GridOverlayCanvas.Visibility = Visibility.Collapsed;
                return;
            }

            var step = GetGridStepPx();
            if (step <= 0)
                return;

            GridOverlayCanvas.Visibility = Visibility.Visible;

            var minorBrush = new SolidColorBrush(Color.FromArgb(56, 148, 163, 184));
            var majorBrush = new SolidColorBrush(Color.FromArgb(112, 148, 163, 184));
            const int majorEvery = 5;
            var majorStep = step * majorEvery;

            for (var x = 0; x <= RefW; x += step)
            {
                var brush = majorStep > 0 && x % majorStep == 0 ? majorBrush : minorBrush;
                GridOverlayCanvas.Children.Add(new Line
                {
                    X1 = x,
                    Y1 = 0,
                    X2 = x,
                    Y2 = RefH,
                    Stroke = brush,
                    StrokeThickness = 1
                });
            }

            for (var y = 0; y <= RefH; y += step)
            {
                var brush = majorStep > 0 && y % majorStep == 0 ? majorBrush : minorBrush;
                GridOverlayCanvas.Children.Add(new Line
                {
                    X1 = 0,
                    Y1 = y,
                    X2 = RefW,
                    Y2 = y,
                    Stroke = brush,
                    StrokeThickness = 1
                });
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[LayoutEditorWindow] RebuildGridOverlay failed: {ex}");
            GridOverlayCanvas.Visibility = Visibility.Collapsed;
        }
    }

    private void OnGridSettingsChanged(object sender, RoutedEventArgs e)
    {
        UpdateSnapToGridAvailability();
        RebuildGridOverlay();
    }

    private void OnGridPresetChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_syncingGridUi || GridPresetComboBox.SelectedItem is not ComboBoxItem item)
            return;

        if (!TryParseGridPresetTag(item.Tag, out var tag))
            return;

        _syncingGridUi = true;
        try
        {
            if (tag > 0)
                GridStepBox.Value = tag;
        }
        finally
        {
            _syncingGridUi = false;
        }

        UpdateGridCustomStepVisibility();
        RebuildGridOverlay();
    }

    private void OnGridStepLostFocus(object sender, RoutedEventArgs e) => ApplyGridStepFromInput();

    private void OnGridStepKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key != VirtualKey.Enter)
            return;

        ApplyGridStepFromInput();
        e.Handled = true;
    }

    private void ApplyGridStepFromInput()
    {
        if (_syncingGridUi || double.IsNaN(GridStepBox.Value))
            return;

        var step = GetGridStepPx();
        _syncingGridUi = true;
        try
        {
            GridStepBox.Value = step;
            SelectMatchingGridPreset(step);
        }
        finally
        {
            _syncingGridUi = false;
        }

        RebuildGridOverlay();
    }

    private void SelectMatchingGridPreset(int step)
    {
        foreach (ComboBoxItem item in GridPresetComboBox.Items)
        {
            if (TryParseGridPresetTag(item.Tag, out var tag) && tag == step)
            {
                GridPresetComboBox.SelectedItem = item;
                UpdateGridCustomStepVisibility();
                return;
            }
        }

        foreach (ComboBoxItem item in GridPresetComboBox.Items)
        {
            if (TryParseGridPresetTag(item.Tag, out var tag) && tag < 0)
            {
                GridPresetComboBox.SelectedItem = item;
                _syncingGridUi = true;
                try
                {
                    GridStepBox.Value = step;
                }
                finally
                {
                    _syncingGridUi = false;
                }

                UpdateGridCustomStepVisibility();
                return;
            }
        }
    }

    private void UpdateGridCustomStepVisibility()
    {
        if (GridStepBox is null || GridPresetComboBox?.SelectedItem is not ComboBoxItem item)
            return;

        var isCustom = TryParseGridPresetTag(item.Tag, out var tag) && tag < 0;
        GridStepBox.Visibility = isCustom ? Visibility.Visible : Visibility.Collapsed;
    }

    private static bool TryParseGridPresetTag(object? tag, out int value)
    {
        switch (tag)
        {
            case int i:
                value = i;
                return true;
            case string s when int.TryParse(s, out var parsed):
                value = parsed;
                return true;
            default:
                value = 0;
                return false;
        }
    }

    private static (double TitleSize, double InfoSize, double StackSpacing) ComputePanelLabelMetrics(double wPx, double hPx)
    {
        var minDim = Math.Min(wPx, hPx);
        var titleSize = Math.Clamp(minDim * 0.14, 40, 96);
        var infoSize = Math.Clamp(minDim * 0.095, 28, 68);
        var stackSpacing = Math.Clamp(minDim * 0.035, 8, 24);
        return (titleSize, infoSize, stackSpacing);
    }

    private static void ApplyPanelLabelMetrics(StackPanel labelStack, double wPx, double hPx)
    {
        var (titleSize, infoSize, stackSpacing) = ComputePanelLabelMetrics(wPx, hPx);
        labelStack.Spacing = stackSpacing;

        foreach (var child in labelStack.Children)
        {
            if (child is not TextBlock textBlock)
                continue;

            if (Equals(textBlock.Tag, "title"))
            {
                textBlock.FontSize = titleSize;
                textBlock.FontWeight = Microsoft.UI.Text.FontWeights.Bold;
            }
            else if (Equals(textBlock.Tag, "info"))
            {
                textBlock.FontSize = infoSize;
                textBlock.FontWeight = Microsoft.UI.Text.FontWeights.SemiBold;
            }
        }
    }

    private Border CreatePanelBorder(EditorPanel panel)
    {
        var wPx = Math.Max(MinPanelPx, ToPxW(panel.W));
        var hPx = Math.Max(MinPanelPx, ToPxH(panel.H));

        var titleBlock = new TextBlock
        {
            Tag = "title",
            Foreground = new SolidColorBrush(Color.FromArgb(255, 255, 255, 255)),
            FontWeight = Microsoft.UI.Text.FontWeights.Bold,
            HorizontalAlignment = HorizontalAlignment.Center,
            TextAlignment = TextAlignment.Center,
            IsHitTestVisible = false,
            Text = PanelTitleText(panel)
        };

        var infoBlock = new TextBlock
        {
            Tag = "info",
            Foreground = new SolidColorBrush(Color.FromArgb(255, 255, 255, 255)),
            HorizontalAlignment = HorizontalAlignment.Center,
            TextAlignment = TextAlignment.Center,
            IsHitTestVisible = false,
            Text = PanelInfoText(panel)
        };

        var labelStack = new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(10)
        };
        labelStack.Children.Add(titleBlock);
        labelStack.Children.Add(infoBlock);
        ApplyPanelLabelMetrics(labelStack, wPx, hPx);

        var resize = new Border
        {
            Width = 18,
            Height = 18,
            Tag = ResizeTag,
            Background = new SolidColorBrush(Color.FromArgb(255, 255, 255, 255)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(255, 30, 64, 175)),
            BorderThickness = new Thickness(1),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(0, 0, 3, 3)
        };
        resize.PointerPressed += OnResizePointerPressed;

        var grid = new Grid();
        grid.Children.Add(labelStack);
        grid.Children.Add(resize);

        var border = new Border
        {
            Background = PanelFillBrush(panel.FillColorArgb),
            BorderBrush = new SolidColorBrush(Color.FromArgb(255, 147, 197, 253)),
            BorderThickness = new Thickness(2),
            Child = grid,
            Tag = panel.Id
        };

        border.PointerPressed += OnPanelPointerPressed;

        return border;
    }

    private static string PanelTitleText(EditorPanel panel)
    {
        var title = panel.SeatId is >= 1 and <= 10 ? $"ID {panel.SeatId}" : "空";
        return panel.IsLocked ? $"🔒 {title}" : title;
    }

    private static string PanelInfoText(EditorPanel panel)
    {
        var xPx = (int)Math.Round(ToPxX(panel.X));
        var yPx = (int)Math.Round(ToPxY(panel.Y));
        var wPx = (int)Math.Round(Math.Max(MinPanelPx, ToPxW(panel.W)));
        var hPx = (int)Math.Round(Math.Max(MinPanelPx, ToPxH(panel.H)));
        return $"X {xPx}  Y {yPx}\nW {wPx}  H {hPx}";
    }

    private static SolidColorBrush PanelFillBrush(uint fillColorArgb) =>
        new(Color.FromArgb(200, GetRed(fillColorArgb), GetGreen(fillColorArgb), GetBlue(fillColorArgb)));

    private static byte GetRed(uint argb) => (byte)((argb >> 16) & 0xFF);
    private static byte GetGreen(uint argb) => (byte)((argb >> 8) & 0xFF);
    private static byte GetBlue(uint argb) => (byte)(argb & 0xFF);

    private void BuildPresetColorSwatches()
    {
        PresetColorGrid.Children.Clear();
        PresetColorGrid.RowDefinitions.Clear();
        PresetColorGrid.ColumnDefinitions.Clear();
        for (var r = 0; r < 2; r++)
            PresetColorGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        for (var c = 0; c < 5; c++)
            PresetColorGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        for (var i = 0; i < HostDisplayPanelColors.SeatDefaults.Length; i++)
        {
            var seatId = i + 1;
            var color = HostDisplayPanelColors.SeatDefaults[i];
            var swatch = new Border
            {
                Height = 32,
                MinWidth = 36,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Margin = new Thickness(2),
                CornerRadius = new CornerRadius(4),
                BorderThickness = new Thickness(1),
                BorderBrush = new SolidColorBrush(Color.FromArgb(255, 148, 163, 184)),
                Background = new SolidColorBrush(Color.FromArgb(255, GetRed(color), GetGreen(color), GetBlue(color))),
                Tag = color,
                IsHitTestVisible = true
            };
            ToolTipService.SetToolTip(swatch, $"ID {seatId}");
            swatch.PointerPressed += OnPresetColorPointerPressed;
            Grid.SetRow(swatch, i / 5);
            Grid.SetColumn(swatch, i % 5);
            PresetColorGrid.Children.Add(swatch);
        }
    }

    private void OnPresetColorPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (_selectedPanelId is null || sender is not Border border || !TryReadColorTag(border.Tag, out var color))
            return;

        e.Handled = true;

        var panel = _panels.FirstOrDefault(p => p.Id == _selectedPanelId);
        if (panel is null)
            return;

        panel.FillColorArgb = color;
        UpdatePanelVisual(panel);
        SyncPanelColorUi(panel);
    }

    private static bool TryReadColorTag(object? tag, out uint color)
    {
        switch (tag)
        {
            case uint u:
                color = u;
                return true;
            case int i when i >= 0:
                color = (uint)i;
                return true;
            default:
                color = 0;
                return false;
        }
    }

    private void OnPanelColorPreviewPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (_selectedPanelId is null)
            return;

        var panel = _panels.FirstOrDefault(p => p.Id == _selectedPanelId);
        if (panel is null)
            return;

        e.Handled = true;
        _syncingColorUi = true;
        try
        {
            PanelColorPicker.Color = Color.FromArgb(
                255,
                GetRed(panel.FillColorArgb),
                GetGreen(panel.FillColorArgb),
                GetBlue(panel.FillColorArgb));
        }
        finally
        {
            _syncingColorUi = false;
        }

        PanelColorFlyout.ShowAt(PanelColorPreview);
    }

    private void OnPanelColorPickerColorChanged(ColorPicker sender, ColorChangedEventArgs args)
    {
        if (_syncingColorUi || _selectedPanelId is null)
            return;

        var panel = _panels.FirstOrDefault(p => p.Id == _selectedPanelId);
        if (panel is null)
            return;

        var picked = args.NewColor;
        panel.FillColorArgb = ColorToArgb(picked);
        UpdatePanelVisual(panel);
        SyncPanelColorUi(panel);
    }

    private static uint ColorToArgb(Color color) =>
        (uint)((color.A << 24) | (color.R << 16) | (color.G << 8) | color.B);

    private void SyncPanelColorUi(EditorPanel? panel)
    {
        _syncingColorUi = true;
        try
        {
            if (panel is null)
            {
                PanelColorSection.Opacity = 0.45;
                PanelColorPreview.IsHitTestVisible = false;
                PanelColorPreviewLabel.Text = "パネルを選択してください";
                PanelColorPreview.Background = new SolidColorBrush(Color.FromArgb(255, 100, 116, 139));
                PanelColorHintText.Text = "パネルを選択してください";
                HighlightSelectedColorSwatch(null);
                return;
            }

            PanelColorSection.Opacity = 1;
            PanelColorPreview.IsHitTestVisible = true;
            PanelColorPreviewLabel.Text = "クリックして色を選ぶ";
            PanelColorPreview.Background = new SolidColorBrush(Color.FromArgb(
                255, GetRed(panel.FillColorArgb), GetGreen(panel.FillColorArgb), GetBlue(panel.FillColorArgb)));
            PanelColorHintText.Text = panel.SeatId is >= 1 and <= 10
                ? $"ID {panel.SeatId}：下の色か上のプレビューで変更"
                : "下の色を選ぶか、上のプレビューで細かく指定できます";
            HighlightSelectedColorSwatch(panel.FillColorArgb);
        }
        finally
        {
            _syncingColorUi = false;
        }
    }

    private void HighlightSelectedColorSwatch(uint? selectedColor)
    {
        foreach (var child in PresetColorGrid.Children)
        {
            if (child is not Border swatch || !TryReadColorTag(swatch.Tag, out var color))
                continue;

            var selected = selectedColor is uint value && color == value;
            swatch.BorderThickness = new Thickness(selected ? 3 : 1);
            swatch.BorderBrush = new SolidColorBrush(selected
                ? Color.FromArgb(255, 251, 191, 36)
                : Color.FromArgb(255, 148, 163, 184));
        }
    }

    private static double ToPxX(double pct) => pct / 100.0 * RefW;
    private static double ToPxY(double pct) => pct / 100.0 * RefH;
    private static double ToPxW(double pct) => pct / 100.0 * RefW;
    private static double ToPxH(double pct) => pct / 100.0 * RefH;
    private static double ToPctX(double px) => px / RefW * 100.0;
    private static double ToPctY(double px) => px / RefH * 100.0;
    private static double ToPctW(double px) => px / RefW * 100.0;
    private static double ToPctH(double px) => px / RefH * 100.0;

    private void UpdatePanelVisual(EditorPanel panel)
    {
        if (!_panelBorders.TryGetValue(panel.Id, out var border))
            return;

        var wPx = Math.Max(MinPanelPx, ToPxW(panel.W));
        var hPx = Math.Max(MinPanelPx, ToPxH(panel.H));
        Canvas.SetLeft(border, ToPxX(panel.X));
        Canvas.SetTop(border, ToPxY(panel.Y));
        border.Width = wPx;
        border.Height = hPx;
        Canvas.SetZIndex(border, panel.ZIndex);

        if (panel.Id == _selectedPanelId)
        {
            border.BorderBrush = new SolidColorBrush(panel.IsLocked
                ? Color.FromArgb(255, 148, 163, 184)
                : Color.FromArgb(255, 251, 191, 36));
            border.BorderThickness = new Thickness(3);
        }
        else
        {
            border.BorderBrush = new SolidColorBrush(Color.FromArgb(255, 147, 197, 253));
            border.BorderThickness = new Thickness(panel.IsLocked ? 1 : 2);
        }

        border.Background = PanelFillBrush(panel.FillColorArgb);

        if (border.Child is Grid grid)
        {
            if (grid.Children.Count > 1 && grid.Children[1] is Border resizeHandle)
                resizeHandle.Visibility = panel.IsLocked ? Visibility.Collapsed : Visibility.Visible;

            if (grid.Children[0] is StackPanel labelStack)
            {
                ApplyPanelLabelMetrics(labelStack, wPx, hPx);

                foreach (var child in labelStack.Children)
                {
                    if (child is not TextBlock textBlock)
                        continue;

                    if (Equals(textBlock.Tag, "title"))
                        textBlock.Text = PanelTitleText(panel);
                    else if (Equals(textBlock.Tag, "info"))
                        textBlock.Text = PanelInfoText(panel);
                }
            }
        }

        if (panel.Id == _selectedPanelId && !_syncingMarginGuideUi)
            RefreshMarginGuides();
    }

    private readonly record struct MarginPx(double Left, double Top, double Right, double Bottom);

    private static MarginPx ComputeMarginsPx(EditorPanel panel)
    {
        var xPx = ToPxX(panel.X);
        var yPx = ToPxY(panel.Y);
        var wPx = Math.Max(MinPanelPx, ToPxW(panel.W));
        var hPx = Math.Max(MinPanelPx, ToPxH(panel.H));
        return new MarginPx(
            xPx,
            yPx,
            RefW - (xPx + wPx),
            RefH - (yPx + hPx));
    }

    private void EnsureMarginInputs()
    {
        if (_marginInputsInitialized || MarginGuideInputsCanvas is null)
            return;

        foreach (MarginGuideEdge edge in Enum.GetValues<MarginGuideEdge>())
        {
            var box = CreateMarginGuideTextBox(edge);
            box.LostFocus += OnMarginGuideTextLostFocus;
            box.KeyDown += OnMarginGuideTextKeyDown;
            box.GotFocus += OnMarginGuideTextGotFocus;
            box.PointerPressed += OnMarginGuideTextPointerPressed;

            _marginInputBoxes[edge] = box;
            MarginGuideInputsCanvas.Children.Add(box);
        }

        _marginInputsInitialized = true;
    }

    private void RefreshMarginGuides()
    {
        RefreshMarginGuideLines();
        UpdateMarginInputs();
    }

    private void RefreshMarginGuideLines()
    {
        if (MarginGuideLinesCanvas is null)
            return;

        MarginGuideLinesCanvas.Children.Clear();

        if (_selectedPanelId is null)
        {
            MarginGuideLinesCanvas.Visibility = Visibility.Collapsed;
            return;
        }

        var panel = _panels.FirstOrDefault(p => p.Id == _selectedPanelId);
        if (panel is null)
        {
            MarginGuideLinesCanvas.Visibility = Visibility.Collapsed;
            return;
        }

        MarginGuideLinesCanvas.Visibility = Visibility.Visible;

        var xPx = ToPxX(panel.X);
        var yPx = ToPxY(panel.Y);
        var wPx = Math.Max(MinPanelPx, ToPxW(panel.W));
        var hPx = Math.Max(MinPanelPx, ToPxH(panel.H));
        var rightX = xPx + wPx;
        var bottomY = yPx + hPx;
        var cx = xPx + wPx / 2;
        var cy = yPx + hPx / 2;

        AddHorizontalMarginGuideLines(0, xPx, cy);
        AddHorizontalMarginGuideLines(rightX, RefW, cy);
        AddVerticalMarginGuideLines(0, yPx, cx);
        AddVerticalMarginGuideLines(bottomY, RefH, cx);
    }

    private void UpdateMarginInputs()
    {
        EnsureMarginInputs();
        if (MarginGuideInputsCanvas is null)
            return;

        if (_selectedPanelId is null)
        {
            MarginGuideInputsCanvas.Visibility = Visibility.Collapsed;
            return;
        }

        var panel = _panels.FirstOrDefault(p => p.Id == _selectedPanelId);
        if (panel is null)
        {
            MarginGuideInputsCanvas.Visibility = Visibility.Collapsed;
            return;
        }

        MarginGuideInputsCanvas.Visibility = Visibility.Visible;

        var margins = ComputeMarginsPx(panel);
        var xPx = margins.Left;
        var yPx = margins.Top;
        var wPx = Math.Max(MinPanelPx, ToPxW(panel.W));
        var hPx = Math.Max(MinPanelPx, ToPxH(panel.H));
        var rightX = xPx + wPx;
        var bottomY = yPx + hPx;
        var cx = xPx + wPx / 2;
        var cy = yPx + hPx / 2;

        _syncingMarginGuideUi = true;
        try
        {
            PlaceMarginInput(MarginGuideEdge.Left, margins.Left, xPx / 2, cy);
            PlaceMarginInput(MarginGuideEdge.Right, margins.Right, rightX + margins.Right / 2, cy);
            PlaceMarginInput(MarginGuideEdge.Top, margins.Top, cx, yPx / 2);
            PlaceMarginInput(MarginGuideEdge.Bottom, margins.Bottom, cx, bottomY + margins.Bottom / 2);
        }
        finally
        {
            _syncingMarginGuideUi = false;
        }
    }

    private void PlaceMarginInput(MarginGuideEdge edge, double value, double anchorX, double anchorY)
    {
        if (!_marginInputBoxes.TryGetValue(edge, out var box))
            return;

        box.Visibility = Visibility.Visible;

        var display = ((int)Math.Round(Math.Max(0, value))).ToString();
        if (box.FocusState == FocusState.Unfocused)
            box.Text = display;

        box.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var w = box.DesiredSize.Width;
        var h = box.DesiredSize.Height;

        double left;
        double top;
        switch (edge)
        {
            case MarginGuideEdge.Top:
            case MarginGuideEdge.Bottom:
                // 縦ガイド線の右側に表示（線と数字が重ならない）
                left = anchorX + MarginGuideVerticalLabelOffset;
                top = anchorY - h / 2;
                break;
            default:
                // 横ガイド線の上に表示
                left = anchorX - w / 2;
                top = anchorY - h - 6;
                break;
        }

        Canvas.SetLeft(box, left);
        Canvas.SetTop(box, top);
    }

    private static TextBox CreateMarginGuideTextBox(MarginGuideEdge edge)
    {
        var box = new TextBox
        {
            MinWidth = 56,
            MinHeight = 36,
            FontSize = MarginGuideLabelFontSize,
            FontWeight = Microsoft.UI.Text.FontWeights.Bold,
            Foreground = MarginGuideBrush,
            Background = MarginGuideInputTransparentBrush,
            BorderBrush = MarginGuideInputTransparentBrush,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(6, 4, 6, 4),
            HorizontalTextAlignment = TextAlignment.Center,
            SelectionHighlightColor = new SolidColorBrush(Color.FromArgb(120, 251, 191, 36)),
            Tag = edge,
            IsHitTestVisible = true
        };

        box.Resources["TextControlBackground"] = MarginGuideInputTransparentBrush;
        box.Resources["TextControlBackgroundPointerOver"] = MarginGuideInputTransparentBrush;
        box.Resources["TextControlBackgroundFocused"] = MarginGuideInputTransparentBrush;
        box.Resources["TextControlBackgroundDisabled"] = MarginGuideInputTransparentBrush;
        box.Resources["TextControlBorderBrush"] = MarginGuideInputTransparentBrush;
        box.Resources["TextControlBorderBrushPointerOver"] = MarginGuideInputTransparentBrush;
        box.Resources["TextControlBorderBrushFocused"] = MarginGuideInputTransparentBrush;
        box.Resources["TextControlForeground"] = MarginGuideBrush;
        box.Resources["TextControlForegroundPointerOver"] = MarginGuideBrush;
        box.Resources["TextControlForegroundFocused"] = MarginGuideBrush;
        box.Resources["TextControlForegroundDisabled"] = MarginGuideBrush;
        box.Resources["CaretBrush"] = MarginGuideBrush;

        return box;
    }

    private static void OnMarginGuideTextGotFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox box)
            box.SelectAll();
    }

    private static void OnMarginGuideTextPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (sender is not TextBox box)
            return;

        e.Handled = true;
        box.Focus(FocusState.Pointer);
    }

    private void HideMarginGuides()
    {
        MarginGuideLinesCanvas?.Children.Clear();
        if (MarginGuideLinesCanvas is not null)
            MarginGuideLinesCanvas.Visibility = Visibility.Collapsed;
        if (MarginGuideInputsCanvas is not null)
            MarginGuideInputsCanvas.Visibility = Visibility.Collapsed;
    }

    private void AddHorizontalMarginGuideLines(double x1, double x2, double y)
    {
        if (x2 - x1 < MarginGuideMinLength)
            return;

        MarginGuideLinesCanvas.Children.Add(new Line
        {
            X1 = x1,
            Y1 = y,
            X2 = x2,
            Y2 = y,
            Stroke = MarginGuideBrush,
            StrokeThickness = 2,
            StrokeStartLineCap = PenLineCap.Triangle,
            StrokeEndLineCap = PenLineCap.Triangle,
            IsHitTestVisible = false
        });

        AddMarginTick(x1, y, vertical: true);
        AddMarginTick(x2, y, vertical: true);
    }

    private void AddVerticalMarginGuideLines(double y1, double y2, double x)
    {
        if (y2 - y1 < MarginGuideMinLength)
            return;

        MarginGuideLinesCanvas.Children.Add(new Line
        {
            X1 = x,
            Y1 = y1,
            X2 = x,
            Y2 = y2,
            Stroke = MarginGuideBrush,
            StrokeThickness = 2,
            StrokeStartLineCap = PenLineCap.Triangle,
            StrokeEndLineCap = PenLineCap.Triangle,
            IsHitTestVisible = false
        });

        AddMarginTick(x, y1, vertical: false);
        AddMarginTick(x, y2, vertical: false);
    }

    private void AddMarginTick(double x, double y, bool vertical)
    {
        const double half = 8;
        if (vertical)
        {
            MarginGuideLinesCanvas.Children.Add(new Line
            {
                X1 = x,
                Y1 = y - half,
                X2 = x,
                Y2 = y + half,
                Stroke = MarginGuideBrush,
                StrokeThickness = 1,
                IsHitTestVisible = false
            });
        }
        else
        {
            MarginGuideLinesCanvas.Children.Add(new Line
            {
                X1 = x - half,
                Y1 = y,
                X2 = x + half,
                Y2 = y,
                Stroke = MarginGuideBrush,
                StrokeThickness = 1,
                IsHitTestVisible = false
            });
        }
    }

    private void OnMarginGuideTextLostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox box)
            CommitMarginGuideText(box);
    }

    private void OnMarginGuideTextKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key != VirtualKey.Enter || sender is not TextBox box)
            return;

        CommitMarginGuideText(box);
        e.Handled = true;
    }

    private void CommitMarginGuideText(TextBox box)
    {
        if (_syncingMarginGuideUi || box.Tag is not MarginGuideEdge edge)
            return;

        if (!double.TryParse(box.Text.Trim(), out var marginPx))
        {
            if (_selectedPanelId is not null &&
                _panels.FirstOrDefault(p => p.Id == _selectedPanelId) is { } panel)
            {
                var margins = ComputeMarginsPx(panel);
                var reset = edge switch
                {
                    MarginGuideEdge.Left => margins.Left,
                    MarginGuideEdge.Top => margins.Top,
                    MarginGuideEdge.Right => margins.Right,
                    MarginGuideEdge.Bottom => margins.Bottom,
                    _ => 0
                };
                box.Text = ((int)Math.Round(reset)).ToString();
            }

            return;
        }

        ApplyMarginGuideInput(edge, marginPx);
    }

    private void ApplyMarginGuideInput(MarginGuideEdge edge, double marginPx)
    {
        if (_syncingMarginGuideUi || _selectedPanelId is null)
            return;

        var panel = _panels.FirstOrDefault(p => p.Id == _selectedPanelId);
        if (panel is null || panel.IsLocked)
            return;

        marginPx = Math.Round(marginPx);
        marginPx = Math.Max(0, marginPx);

        var wPx = Math.Max(MinPanelPx, ToPxW(panel.W));
        var hPx = Math.Max(MinPanelPx, ToPxH(panel.H));
        var xPx = ToPxX(panel.X);
        var yPx = ToPxY(panel.Y);

        switch (edge)
        {
            case MarginGuideEdge.Left:
                xPx = marginPx;
                break;
            case MarginGuideEdge.Top:
                yPx = marginPx;
                break;
            case MarginGuideEdge.Right:
                xPx = RefW - wPx - marginPx;
                break;
            case MarginGuideEdge.Bottom:
                yPx = RefH - hPx - marginPx;
                break;
        }

        _syncingMarginGuideUi = true;
        try
        {
            ApplyPixelGeometry(panel, xPx, yPx, wPx, hPx);
            UpdatePanelVisual(panel);
            SyncGeometryControls(panel);

            if (_marginInputBoxes.TryGetValue(edge, out var box))
            {
                var margins = ComputeMarginsPx(panel);
                var actual = edge switch
                {
                    MarginGuideEdge.Left => margins.Left,
                    MarginGuideEdge.Top => margins.Top,
                    MarginGuideEdge.Right => margins.Right,
                    MarginGuideEdge.Bottom => margins.Bottom,
                    _ => marginPx
                };
                box.Text = ((int)Math.Round(actual)).ToString();
            }
        }
        finally
        {
            _syncingMarginGuideUi = false;
            RefreshMarginGuideLines();
            UpdateMarginInputs();
        }
    }

    private void UpdateLinkAspectRatio(EditorPanel panel)
    {
        var wPx = Math.Max(1, ToPxW(panel.W));
        var hPx = Math.Max(1, ToPxH(panel.H));
        _linkAspectRatio = wPx / hPx;
    }

    private EditorPanel? GetSelectedPanel() =>
        _selectedPanelId is int id ? _panels.FirstOrDefault(p => p.Id == id) : null;

    private static int? GetPanelIdFromTag(object? tag) => tag switch
    {
        int id => id,
        null => null,
        _ => null
    };

    private static string FormatPanelSelectLabel(EditorPanel panel)
    {
        var idLabel = panel.SeatId is >= 1 and <= 10 ? $"ID {panel.SeatId}" : "空";
        var lockMark = panel.IsLocked ? " 🔒" : "";
        return $"#{panel.Id}（{idLabel}）{lockMark}";
    }

    private void RebuildPanelSelectCombo()
    {
        if (PanelSelectComboBox is null)
            return;

        _syncingPanelListUi = true;
        try
        {
            PanelSelectComboBox.Items.Clear();
            PanelSelectComboBox.Items.Add(new ComboBoxItem { Content = "（なし）", Tag = null });

            foreach (var panel in _panels.OrderBy(p => p.ZIndex).ThenBy(p => p.Id))
            {
                PanelSelectComboBox.Items.Add(new ComboBoxItem
                {
                    Content = FormatPanelSelectLabel(panel),
                    Tag = panel.Id
                });
            }

            ApplyPanelSelectComboIndex(_selectedPanelId);
        }
        finally
        {
            _syncingPanelListUi = false;
        }
    }

    private void SyncPanelSelectComboIndex(int? selectedId = null)
    {
        if (PanelSelectComboBox is null || PanelSelectComboBox.Items.Count == 0)
            return;

        _syncingPanelListUi = true;
        try
        {
            ApplyPanelSelectComboIndex(selectedId ?? _selectedPanelId);
        }
        finally
        {
            _syncingPanelListUi = false;
        }
    }

    private void ApplyPanelSelectComboIndex(int? selectedId)
    {
        if (PanelSelectComboBox is null || PanelSelectComboBox.Items.Count == 0)
            return;

        if (selectedId is null)
        {
            if (PanelSelectComboBox.SelectedIndex != 0)
                PanelSelectComboBox.SelectedIndex = 0;
            return;
        }

        for (var i = 0; i < PanelSelectComboBox.Items.Count; i++)
        {
            if (PanelSelectComboBox.Items[i] is ComboBoxItem item &&
                GetPanelIdFromTag(item.Tag) == selectedId)
            {
                if (PanelSelectComboBox.SelectedIndex != i)
                    PanelSelectComboBox.SelectedIndex = i;
                return;
            }
        }

        if (PanelSelectComboBox.SelectedIndex != 0)
            PanelSelectComboBox.SelectedIndex = 0;
    }

    private void RefreshPanelSelectCombo() => RebuildPanelSelectCombo();

    private void SetGeometryControlsEnabled(bool enabled)
    {
        WSlider.IsEnabled = enabled;
        HSlider.IsEnabled = enabled;
        XSlider.IsEnabled = enabled;
        YSlider.IsEnabled = enabled;
        WBox.IsEnabled = enabled;
        HBox.IsEnabled = enabled;
        XBox.IsEnabled = enabled;
        YBox.IsEnabled = enabled;

        foreach (var box in _marginInputBoxes.Values)
            box.IsEnabled = enabled;
    }

    private void SyncPanelLockUi(EditorPanel? panel)
    {
        if (PanelLockCheckBox is null)
            return;

        _syncingPanelLockUi = true;
        try
        {
            if (panel is null)
            {
                PanelLockCheckBox.IsEnabled = false;
                PanelLockCheckBox.IsChecked = false;
                SetGeometryControlsEnabled(false);
                return;
            }

            PanelLockCheckBox.IsEnabled = true;
            PanelLockCheckBox.IsChecked = panel.IsLocked;
            SetGeometryControlsEnabled(!panel.IsLocked);
        }
        finally
        {
            _syncingPanelLockUi = false;
        }
    }

    private void OnPanelSelectComboChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_syncingPanelListUi || PanelSelectComboBox.SelectedItem is not ComboBoxItem item)
            return;

        var panelId = GetPanelIdFromTag(item.Tag);
        if (_selectedPanelId == panelId)
            return;

        SelectPanel(panelId);
    }

    private void OnPanelLockChanged(object sender, RoutedEventArgs e)
    {
        if (_syncingPanelLockUi)
            return;

        var panel = GetSelectedPanel();
        if (panel is null)
            return;

        panel.IsLocked = PanelLockCheckBox.IsChecked == true;
        UpdatePanelVisual(panel);
        SyncPanelLockUi(panel);
        RefreshPanelSelectCombo();
    }

    private void SelectPanel(int? panelId)
    {
        _selectedPanelId = panelId;
        foreach (var panel in _panels)
            UpdatePanelVisual(panel);

        _syncingUi = true;
        try
        {
            if (panelId is null)
            {
                SelectionHintText.Text = "パネルを選択してください";
                SeatComboBox.SelectedIndex = 0;
                WSlider.Value = 480;
                HSlider.Value = 270;
                XSlider.Value = 0;
                YSlider.Value = 0;
                WBox.Value = 480;
                HBox.Value = 270;
                XBox.Value = 0;
                YBox.Value = 0;
                SyncPanelColorUi(null);
                SyncPanelZOrderUi(null);
                SyncPanelLockUi(null);
                SyncPanelSelectComboIndex(null);
                return;
            }

            var panel = _panels.FirstOrDefault(p => p.Id == panelId);
            if (panel is null) return;

            SelectionHintText.Text = panel.IsLocked
                ? $"パネル #{panel.Id}（ロック中）"
                : $"パネル #{panel.Id}";
            SeatComboBox.SelectedIndex = panel.SeatId is >= 1 and <= 10 ? panel.SeatId.Value : 0;
            UpdateLinkAspectRatio(panel);
            SetGeometryControlsFromPanel(panel);
            SyncPanelColorUi(panel);
            SyncPanelZOrderUi(panel);
            SyncPanelLockUi(panel);
            SyncPanelSelectComboIndex(panelId);
            EditorCanvas.Focus(FocusState.Programmatic);
        }
        finally
        {
            _syncingUi = false;
            RefreshMarginGuides();
        }
    }

    private void SetGeometryControlsFromPanel(EditorPanel panel)
    {
        var wPx = Math.Round(ToPxW(panel.W));
        var hPx = Math.Round(ToPxH(panel.H));
        var xPx = Math.Round(ToPxX(panel.X));
        var yPx = Math.Round(ToPxY(panel.Y));

        WSlider.Value = wPx;
        HSlider.Value = hPx;
        XSlider.Value = xPx;
        YSlider.Value = yPx;
        WBox.Value = wPx;
        HBox.Value = hPx;
        XBox.Value = xPx;
        YBox.Value = yPx;
    }

    private void OnRootPreviewKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (ShouldIgnoreKeyboardNudge(e.OriginalSource))
            return;

        var (dx, dy) = e.Key switch
        {
            VirtualKey.Left => (-1, 0),
            VirtualKey.Right => (1, 0),
            VirtualKey.Up => (0, -1),
            VirtualKey.Down => (0, 1),
            _ => (0, 0)
        };

        if (dx == 0 && dy == 0)
            return;

        if (!TryNudgeSelectedPanel(dx, dy))
            return;

        e.Handled = true;
    }

    private static bool ShouldIgnoreKeyboardNudge(object? source)
    {
        for (var el = source as DependencyObject; el is not null; el = VisualTreeHelper.GetParent(el))
        {
            if (el is TextBox or NumberBox)
                return true;
            if (el is ComboBox { IsDropDownOpen: true })
                return true;
        }

        return false;
    }

    private bool TryNudgeSelectedPanel(int dxPx, int dyPx)
    {
        var panel = GetSelectedPanel();
        if (panel is null || panel.IsLocked)
            return false;

        var xPx = ToPxX(panel.X);
        var yPx = ToPxY(panel.Y);
        var wPx = Math.Max(MinPanelPx, ToPxW(panel.W));
        var hPx = Math.Max(MinPanelPx, ToPxH(panel.H));

        ApplyPixelGeometry(panel, xPx + dxPx, yPx + dyPx, wPx, hPx, applySnap: false);
        UpdatePanelVisual(panel);
        SyncGeometryControls(panel);
        return true;
    }

    private void OnPanelPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (sender is not Border border || border.Tag is not int id)
            return;

        if (IsResizeHandle(e.OriginalSource))
            return;

        SelectPanel(id);
        EditorCanvas.Focus(FocusState.Programmatic);
        var panel = _panels.FirstOrDefault(p => p.Id == id);
        if (panel is null || panel.IsLocked)
            return;

        _dragPanel = panel;

        _dragPointerId = e.Pointer.PointerId;
        _dragStart = e.GetCurrentPoint(EditorCanvas).Position;
        _dragStartX = _dragPanel.X;
        _dragStartY = _dragPanel.Y;
        EditorCanvas.CapturePointer(e.Pointer);
        e.Handled = true;
    }

    private void OnResizePointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (GetSelectedPanel() is not { } resizePanel || resizePanel.IsLocked)
            return;

        _resizePanel = resizePanel;

        _dragPanel = null;
        _dragPointerId = null;
        _resizePointerId = e.Pointer.PointerId;
        _resizeStart = e.GetCurrentPoint(EditorCanvas).Position;
        _resizeStartWPx = ToPxW(_resizePanel.W);
        _resizeStartHPx = ToPxH(_resizePanel.H);
        _resizeAspectRatio = _resizeStartWPx / Math.Max(1, _resizeStartHPx);
        EditorCanvas.CapturePointer(e.Pointer);
        e.Handled = true;
    }

    private void OnCanvasPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (_resizePanel is not null && _resizePointerId == e.Pointer.PointerId)
        {
            var pos = e.GetCurrentPoint(EditorCanvas).Position;
            var dwPx = pos.X - _resizeStart.X;
            var dhPx = pos.Y - _resizeStart.Y;

            double newWPx;
            double newHPx;
            if (IsWhLinked())
            {
                if (Math.Abs(dwPx) >= Math.Abs(dhPx * _resizeAspectRatio))
                {
                    newWPx = _resizeStartWPx + dwPx;
                    newHPx = newWPx / _resizeAspectRatio;
                }
                else
                {
                    newHPx = _resizeStartHPx + dhPx;
                    newWPx = newHPx * _resizeAspectRatio;
                }
            }
            else
            {
                newWPx = _resizeStartWPx + dwPx;
                newHPx = _resizeStartHPx + dhPx;
            }

            ApplyPixelGeometry(_resizePanel, ToPxX(_resizePanel.X), ToPxY(_resizePanel.Y), newWPx, newHPx);
            UpdatePanelVisual(_resizePanel);
            SyncGeometryControls(_resizePanel);
            e.Handled = true;
            return;
        }

        if (_dragPanel is null || _dragPointerId != e.Pointer.PointerId)
            return;

        var dragPos = e.GetCurrentPoint(EditorCanvas).Position;
        var dxPct = (dragPos.X - _dragStart.X) / RefW * 100.0;
        var dyPct = (dragPos.Y - _dragStart.Y) / RefH * 100.0;
        _dragPanel.X = Clamp(_dragStartX + dxPct, 0, 100 - _dragPanel.W);
        _dragPanel.Y = Clamp(_dragStartY + dyPct, 0, 100 - _dragPanel.H);
        UpdatePanelVisual(_dragPanel);
        SyncGeometryControls(_dragPanel);
        e.Handled = true;
    }

    private void OnCanvasPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (_resizePointerId == e.Pointer.PointerId)
        {
            EndResize(e.Pointer);
            e.Handled = true;
            return;
        }

        if (_dragPointerId == e.Pointer.PointerId)
        {
            EndDrag(e.Pointer);
            e.Handled = true;
        }
    }

    private void OnCanvasPointerCaptureLost(object sender, PointerRoutedEventArgs e)
    {
        if (_resizePointerId == e.Pointer.PointerId)
            EndResize(e.Pointer);
        else if (_dragPointerId == e.Pointer.PointerId)
            EndDrag(e.Pointer);
    }

    private void EndDrag(Pointer pointer)
    {
        EditorCanvas.ReleasePointerCapture(pointer);

        if (_dragPanel is not null)
        {
            SnapPanelGeometry(_dragPanel);
            UpdatePanelVisual(_dragPanel);
            SyncGeometryControls(_dragPanel);
        }

        _dragPanel = null;
        _dragPointerId = null;
    }

    private void EndResize(Pointer pointer)
    {
        EditorCanvas.ReleasePointerCapture(pointer);

        if (_resizePanel is not null)
        {
            UpdatePanelVisual(_resizePanel);
            SyncGeometryControls(_resizePanel);
            UpdateLinkAspectRatio(_resizePanel);
        }

        _resizePanel = null;
        _resizePointerId = null;
    }

    private bool IsWhLinked() => LinkWhCheckBox.IsChecked == true;

    private void SyncGeometryControls(EditorPanel panel)
    {
        if (_syncingUi) return;
        _syncingUi = true;
        try
        {
            SetGeometryControlsFromPanel(panel);
        }
        finally
        {
            _syncingUi = false;
        }
    }

    private void ApplyPixelGeometry(EditorPanel panel, double xPx, double yPx, double wPx, double hPx, bool applySnap = true)
    {
        if (applySnap && IsSnapToGridEnabled())
        {
            var step = GetGridStepPx();
            xPx = SnapPx(xPx, step);
            yPx = SnapPx(yPx, step);
        }

        wPx = Clamp(wPx, MinPanelPx, RefW);
        hPx = Clamp(hPx, MinPanelPx, RefH);
        xPx = Clamp(xPx, 0, RefW - wPx);
        yPx = Clamp(yPx, 0, RefH - hPx);

        panel.X = ToPctX(xPx);
        panel.Y = ToPctY(yPx);
        panel.W = ToPctW(wPx);
        panel.H = ToPctH(hPx);
    }

    private void ApplyGeometryFromControls(object? changedControl)
    {
        if (_syncingUi || _selectedPanelId is null)
            return;

        var panel = GetSelectedPanel();
        if (panel is null || panel.IsLocked)
            return;

        var wPx = WSlider.Value;
        var hPx = HSlider.Value;
        var xPx = XSlider.Value;
        var yPx = YSlider.Value;

        if (IsWhLinked())
        {
            if (ReferenceEquals(changedControl, WSlider) || ReferenceEquals(changedControl, WBox))
                hPx = wPx / _linkAspectRatio;
            else if (ReferenceEquals(changedControl, HSlider) || ReferenceEquals(changedControl, HBox))
                wPx = hPx * _linkAspectRatio;
        }

        ApplyPixelGeometry(panel, xPx, yPx, wPx, hPx);
        UpdatePanelVisual(panel);
        UpdateLinkAspectRatio(panel);
        SyncGeometryControls(panel);
    }

    private void OnGeometrySliderChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_syncingUi || _selectedPanelId is null)
            return;

        if (double.IsNaN(e.NewValue))
            return;

        ApplyGeometryFromControls(sender);
    }

    private void OnGeometryNumberLostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is NumberBox box)
            CommitGeometryNumber(box);
    }

    private void OnGeometryNumberKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key != VirtualKey.Enter || sender is not NumberBox box)
            return;

        CommitGeometryNumber(box);
        e.Handled = true;
    }

    private void CommitGeometryNumber(NumberBox sender)
    {
        if (_syncingUi || _selectedPanelId is null)
            return;

        if (double.IsNaN(sender.Value))
            return;

        _syncingUi = true;
        try
        {
            if (ReferenceEquals(sender, WBox)) WSlider.Value = WBox.Value;
            else if (ReferenceEquals(sender, HBox)) HSlider.Value = HBox.Value;
            else if (ReferenceEquals(sender, XBox)) XSlider.Value = XBox.Value;
            else if (ReferenceEquals(sender, YBox)) YSlider.Value = YBox.Value;
        }
        finally
        {
            _syncingUi = false;
        }

        ApplyGeometryFromControls(sender);
    }

    private void OnLinkWhChanged(object sender, RoutedEventArgs e)
    {
        if (_selectedPanelId is null) return;
        var panel = _panels.FirstOrDefault(p => p.Id == _selectedPanelId);
        if (panel is null) return;
        UpdateLinkAspectRatio(panel);
    }

    private void OnSeatComboChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_syncingUi || _selectedPanelId is null) return;
        var panel = _panels.FirstOrDefault(p => p.Id == _selectedPanelId);
        if (panel is null) return;
        panel.SeatId = SeatComboBox.SelectedItem is ComboBoxItem item ? item.Tag as int? : null;
        panel.FillColorArgb = HostDisplayPanelColors.GetDefaultForSeat(panel.SeatId);
        UpdatePanelVisual(panel);
        SyncPanelColorUi(panel);
        RefreshPanelSelectCombo();
    }

    private async void OnBgComboChanged(object sender, SelectionChangedEventArgs e)
    {
        _backgroundUrl = BgComboBox.SelectedItem is ComboBoxItem item ? item.Tag as string : null;
        await UpdateEditorBackgroundAsync();
    }

    private async Task UpdateEditorBackgroundAsync()
    {
        if (string.IsNullOrWhiteSpace(_backgroundUrl))
        {
            EditorBgImage.Source = null;
            EditorBgImage.Visibility = Visibility.Collapsed;
            return;
        }

        var thumb = await _images.LoadThumbnailAsync(_backgroundUrl, 960);
        EditorBgImage.Source = thumb;
        EditorBgImage.Visibility = thumb is null ? Visibility.Collapsed : Visibility.Visible;
    }

    private void OnAddPanelClick(object sender, RoutedEventArgs e)
    {
        var panel = new EditorPanel
        {
            Id = _nextPanelId++,
            SeatId = null,
            X = ToPctX(720),
            Y = ToPctY(405),
            W = ToPctW(480),
            H = ToPctH(270),
            FillColorArgb = HostDisplayPanelColors.EmptySeatColor,
            ZIndex = _panels.Count > 0 ? _panels.Max(p => p.ZIndex) + 1 : 0
        };
        _panels.Add(panel);
        SnapPanelGeometry(panel);
        var border = CreatePanelBorder(panel);
        _panelBorders[panel.Id] = border;
        EditorCanvas.Children.Add(border);
        UpdatePanelVisual(panel);
        RebuildPanelSelectCombo();
        SelectPanel(panel.Id);
    }

    private async void OnDeletePanelClick(object sender, RoutedEventArgs e)
    {
        if (_selectedPanelId is null) return;
        var panel = _panels.FirstOrDefault(p => p.Id == _selectedPanelId);
        if (panel is null) return;

        if (panel.IsLocked)
        {
            await ShowDialogAsync("削除できません", "ロック中のパネルは削除できません。ロックを解除してから削除してください。");
            return;
        }

        _panels.Remove(panel);
        if (_panelBorders.TryGetValue(panel.Id, out var border))
        {
            EditorCanvas.Children.Remove(border);
            _panelBorders.Remove(panel.Id);
        }

        RebuildPanelSelectCombo();
        SelectPanel(null);
    }

    private void OnApplyClick(object sender, RoutedEventArgs e)
    {
        var layout = BuildCurrentLayout();
        HostDisplayLayoutStore.Save(layout);
        AppHostContext.DisplayOutput.ApplyLayout(layout);
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();

    private static bool IsResizeHandle(object? source)
    {
        for (var node = source as DependencyObject; node is not null; node = VisualTreeHelper.GetParent(node))
        {
            if (node is FrameworkElement fe && Equals(fe.Tag, ResizeTag))
                return true;
        }

        return false;
    }

    private static double Clamp(double value, double min, double max) =>
        Math.Max(min, Math.Min(max, value));
}
