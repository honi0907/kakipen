using System.Diagnostics;
using KakiMoni.Core.Models;
using KakiMoni.Core.Updates;
using KakiMoni_Host.Services;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;

namespace KakiMoni_Host.SaveViewer;

public sealed partial class SaveViewerWindow : Window
{
    private const double FeaturedThumbMultiplier = 2.0;
    private const double ThumbGridGap = 6;

    private sealed class SeatThumbGridState
    {
        public required IReadOnlyList<SaveGalleryEntry> Items { get; init; }
        public int ThumbSize { get; init; }
        public bool ShowFileName { get; init; }
        public bool ShowUpdated { get; init; }
        public required string LatestKey { get; init; }
    }

    private readonly SaveViewerSettings _settings;
    private readonly CheckBox[] _seatChecks = new CheckBox[10];
    private readonly DispatcherTimer _seatPollTimer = new();
    private readonly DispatcherTimer _autoRefreshTimer = new();
    private readonly HashSet<int> _connectedSeatIds = new();

    private CancellationTokenSource? _liveCts;
    private DispatcherQueueTimer? _pendingRefreshTimer;
    private DispatcherQueueTimer? _reloadDebounceTimer;
    private SaveGalleryResult? _cachedResult;
    private (List<int> Top, List<int> Bottom, List<int> Visible) _cachedRowLayout;
    private string _cachedLatestKey = string.Empty;
    private int _loadGeneration;
    private bool _loading;
    private bool _closed;
    private bool _suppressSettingsSave;
    private bool _onlineUpdateBusy;

    public SaveViewerWindow()
    {
        InitializeComponent();
        _settings = SaveViewerSettingsStore.Load();
        if (!string.IsNullOrWhiteSpace(App.SaveViewerServerUrl))
            _settings.ServerUrl = App.SaveViewerServerUrl.Trim();

        BuildIdChips();
        ApplySettingsToUi();
        VersionText.Text = AppVersionDisplay.Label;
        UpdateServerUrlPanelVisibility();
        Closed += OnClosed;

        _seatPollTimer.Interval = TimeSpan.FromSeconds(3);
        _seatPollTimer.Tick += (_, _) => _ = RefreshConnectedSeatsAsync();

        _autoRefreshTimer.Interval = TimeSpan.FromSeconds(Math.Clamp(_settings.AutoRefreshIntervalSec, 2, 120));
        _autoRefreshTimer.Tick += (_, _) => _ = LoadGalleryAsync();

        AppWindow.Resize(new Windows.Graphics.SizeInt32(1400, 900));
        Activated += OnActivated;
    }

    private void OnActivated(object sender, WindowActivatedEventArgs args)
    {
        Activated -= OnActivated;
        SetupLiveListener();
        _seatPollTimer.Start();
        if (_settings.AutoRefreshEnabled)
            _autoRefreshTimer.Start();
        _ = RefreshConnectedSeatsAsync();
        _ = LoadGalleryAsync();
    }

    private void OnClosed(object sender, WindowEventArgs args)
    {
        _closed = true;
        _seatPollTimer.Stop();
        _autoRefreshTimer.Stop();
        _pendingRefreshTimer?.Stop();
        _reloadDebounceTimer?.Stop();
        StopLiveListener();
        PersistUiToSettings();
        SaveViewerSettingsStore.Save(_settings);
    }

    private void BuildIdChips()
    {
        var chipStyle = RootGrid.Resources["SvIdChipCheckBoxStyle"] as Style;
        var chipBorderStyle = RootGrid.Resources["SvIdChipBorderStyle"] as Style;
        for (var i = 0; i < 10; i++)
        {
            var seatId = i + 1;
            var check = new CheckBox
            {
                Content = $"ID{seatId:D2}",
                Style = chipStyle,
                Tag = seatId
            };
            check.Checked += OnSeatSelectionChanged;
            check.Unchecked += OnSeatSelectionChanged;

            var chip = new Border { Style = chipBorderStyle, Child = check };
            Grid.SetColumn(chip, i);
            _seatChecks[i] = check;
            IdChipGrid.Children.Add(chip);
        }
    }

    private void ApplySettingsToUi()
    {
        _suppressSettingsSave = true;
        try
        {
            MaxPerSeatBox.Value = Math.Clamp(_settings.MaxPerSeat, 1, 500);
            ThumbSizeBox.Value = Math.Clamp(_settings.ThumbSize, 60, 220);
            LiveUpdateCheckBox.IsChecked = _settings.LiveUpdateEnabled;
            HideDisconnectedCheckBox.IsChecked = _settings.HideDisconnected;
            ShowFileNameCheckBox.IsChecked = _settings.ShowFileName;
            ShowUpdatedCheckBox.IsChecked = _settings.ShowUpdated;
            LayoutTopBox.Text = _settings.LayoutTop;
            LayoutBottomBox.Text = _settings.LayoutBottom;
            ServerUrlBox.Text = string.IsNullOrWhiteSpace(_settings.ServerUrl)
                ? "http://localhost:3000"
                : _settings.ServerUrl;

            var selected = new HashSet<int>(_settings.SelectedSeatIds);
            for (var i = 0; i < 10; i++)
                _seatChecks[i].IsChecked = selected.Contains(i + 1);

            ApplyToolbarVisibility(_settings.ToolbarVisible);
        }
        finally
        {
            _suppressSettingsSave = false;
        }
    }

    private void ApplyToolbarVisibility(bool visible)
    {
        ToolbarPanel.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        ShowToolbarFab.Visibility = visible ? Visibility.Collapsed : Visibility.Visible;
        _settings.ToolbarVisible = visible;
    }

    private void PersistUiToSettings()
    {
        _settings.MaxPerSeat = ReadNumberBoxValue(MaxPerSeatBox, 120, 1, 500);
        _settings.ThumbSize = ReadNumberBoxValue(ThumbSizeBox, 96, 60, 220);
        _settings.LiveUpdateEnabled = LiveUpdateCheckBox.IsChecked == true;
        _settings.HideDisconnected = HideDisconnectedCheckBox.IsChecked == true;
        _settings.ShowFileName = ShowFileNameCheckBox.IsChecked == true;
        _settings.ShowUpdated = ShowUpdatedCheckBox.IsChecked == true;
        _settings.LayoutTop = LayoutTopBox.Text;
        _settings.LayoutBottom = LayoutBottomBox.Text;
        _settings.ServerUrl = SaveGalleryApiService.NormalizeServerUrl(ServerUrlBox.Text);
        _settings.SelectedSeatIds = GetSelectedSeatIds();
    }

    private void UpdateServerUrlPanelVisibility()
    {
        ServerUrlPanel.Visibility = AppHostContext.Server.IsRunning
            ? Visibility.Collapsed
            : Visibility.Visible;
    }

    private string? ResolveServerBaseUrl()
    {
        if (!DispatcherQueue.HasThreadAccess)
            throw new InvalidOperationException("ResolveServerBaseUrl must run on the UI thread.");

        if (AppHostContext.Server.IsRunning)
            return AppHostContext.Server.BaseUrl.TrimEnd('/');

        var url = SaveGalleryApiService.NormalizeServerUrl(ServerUrlBox.Text);
        if (string.IsNullOrEmpty(url))
            url = SaveGalleryApiService.NormalizeServerUrl(_settings.ServerUrl);

        return string.IsNullOrEmpty(url) ? null : url;
    }

    private List<int> GetSelectedSeatIds() =>
        _seatChecks
            .Where(c => c.IsChecked == true)
            .Select(c => (int)c.Tag!)
            .OrderBy(id => id)
            .ToList();

    private List<int> GetEffectiveSeatIds()
    {
        var selected = GetSelectedSeatIds();
        if (HideDisconnectedCheckBox.IsChecked != true)
            return selected;

        return selected.Where(id => _connectedSeatIds.Contains(id)).ToList();
    }

    private static List<int> ParseRowIds(string? value)
    {
        return (value ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(token => int.TryParse(token, out var id) ? id : -1)
            .Where(id => id is >= 1 and <= 10)
            .Distinct()
            .ToList();
    }

    private (List<int> Top, List<int> Bottom, List<int> Visible) GetRowLayout(IReadOnlyList<int> selectedIds)
    {
        var selectedSet = new HashSet<int>(selectedIds);
        var top = ParseRowIds(LayoutTopBox.Text).Where(selectedSet.Contains).Take(5).ToList();
        var used = new HashSet<int>(top);
        var bottomManual = ParseRowIds(LayoutBottomBox.Text)
            .Where(id => selectedSet.Contains(id) && !used.Contains(id))
            .ToList();
        var rest = selectedIds.Where(id => !used.Contains(id) && !bottomManual.Contains(id)).ToList();
        var bottom = bottomManual.Concat(rest).Take(5).ToList();
        return (top, bottom, top.Concat(bottom).ToList());
    }

    private void SetupLiveListener()
    {
        StopLiveListener();
        if (LiveUpdateCheckBox.IsChecked != true)
            return;

        var baseUrl = ResolveServerBaseUrl();
        if (string.IsNullOrEmpty(baseUrl))
            return;

        _liveCts = new CancellationTokenSource();
        var token = _liveCts.Token;
        _ = Task.Run(async () =>
        {
            try
            {
                await SaveGalleryApiService.RunLiveStreamAsync(
                    baseUrl,
                    () => ScheduleRefresh(),
                    token);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SaveViewerWindow] live stream failed: {ex.Message}");
            }
        }, token);
    }

    private void StopLiveListener()
    {
        if (_liveCts is null)
            return;

        try
        {
            _liveCts.Cancel();
            _liveCts.Dispose();
        }
        catch { }

        _liveCts = null;
    }

    private void ScheduleRefresh()
    {
        InvokeOnUi(() =>
        {
            if (_closed)
                return;

            _pendingRefreshTimer ??= DispatcherQueue.CreateTimer();
            _pendingRefreshTimer.Interval = TimeSpan.FromMilliseconds(120);
            _pendingRefreshTimer.IsRepeating = false;
            _pendingRefreshTimer.Tick -= OnPendingRefreshTick;
            _pendingRefreshTimer.Tick += OnPendingRefreshTick;
            _pendingRefreshTimer.Start();
        });
    }

    private void OnPendingRefreshTick(DispatcherQueueTimer sender, object args)
    {
        sender.Tick -= OnPendingRefreshTick;
        _ = LoadGalleryAsync();
    }

    private Task InvokeOnUiAsync(Action action)
    {
        if (_closed)
            return Task.CompletedTask;

        if (DispatcherQueue.HasThreadAccess)
        {
            action();
            return Task.CompletedTask;
        }

        var tcs = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!DispatcherQueue.TryEnqueue(() =>
        {
            if (_closed)
            {
                tcs.SetResult(null);
                return;
            }

            try
            {
                action();
                tcs.SetResult(null);
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        }))
        {
            tcs.SetException(new InvalidOperationException("UI キューに投入できませんでした"));
        }

        return tcs.Task;
    }

    private void InvokeOnUi(Action action)
    {
        if (_closed)
            return;

        if (DispatcherQueue.HasThreadAccess)
        {
            action();
            return;
        }

        DispatcherQueue.TryEnqueue(() =>
        {
            if (_closed)
                return;

            try
            {
                action();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SaveViewerWindow] UI action failed: {ex}");
            }
        });
    }

    private void ScheduleDebouncedReload(int delayMs = 280)
    {
        InvokeOnUi(() =>
        {
            if (_closed)
                return;

            _reloadDebounceTimer ??= DispatcherQueue.CreateTimer();
            _reloadDebounceTimer.Interval = TimeSpan.FromMilliseconds(delayMs);
            _reloadDebounceTimer.IsRepeating = false;
            _reloadDebounceTimer.Tick -= OnReloadDebounceTick;
            _reloadDebounceTimer.Tick += OnReloadDebounceTick;
            _reloadDebounceTimer.Start();
        });
    }

    private void OnReloadDebounceTick(DispatcherQueueTimer sender, object args)
    {
        sender.Tick -= OnReloadDebounceTick;
        _ = LoadGalleryAsync();
    }

    private void RefreshGalleryFromCache()
    {
        if (_closed || _cachedResult is null)
        {
            _ = LoadGalleryAsync();
            return;
        }

        try
        {
            RenderGallery(_cachedResult, _cachedRowLayout, _cachedLatestKey);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SaveViewerWindow] RefreshGalleryFromCache failed: {ex}");
            _ = LoadGalleryAsync();
        }
    }

    private static int ReadNumberBoxValue(NumberBox box, int fallback, int min, int max)
    {
        var value = box.Value;
        if (double.IsNaN(value))
            return fallback;

        return (int)Math.Clamp(value, min, max);
    }

    private async Task RefreshConnectedSeatsAsync()
    {
        if (_closed)
            return;

        var baseUrl = ResolveServerBaseUrl();
        if (string.IsNullOrEmpty(baseUrl))
        {
            await InvokeOnUiAsync(() => _connectedSeatIds.Clear());
            return;
        }

        try
        {
            var seats = await HostApiService.GetSeatsStatusAsync(baseUrl).ConfigureAwait(false);
            var connectedIds = seats.Where(s => s.Connected).Select(s => s.SeatId).ToList();
            var shouldReload = false;
            await InvokeOnUiAsync(() =>
            {
                _connectedSeatIds.Clear();
                foreach (var seatId in connectedIds)
                    _connectedSeatIds.Add(seatId);
                shouldReload = HideDisconnectedCheckBox.IsChecked == true;
            });

            if (shouldReload)
                await LoadGalleryAsync();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SaveViewerWindow] seat poll failed: {ex.Message}");
        }
    }

    private Task EnsureUiAsync()
    {
        if (_closed || DispatcherQueue.HasThreadAccess)
            return Task.CompletedTask;

        var tcs = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!DispatcherQueue.TryEnqueue(() => tcs.SetResult(null)))
            tcs.SetException(new InvalidOperationException("UI キューに投入できませんでした"));

        return tcs.Task;
    }

    private async Task LoadGalleryAsync()
    {
        if (_closed)
            return;

        await EnsureUiAsync();
        if (_closed)
            return;

        var generation = ++_loadGeneration;
        _loading = true;
        try
        {
            var baseUrl = ResolveServerBaseUrl();
            var seatIds = GetEffectiveSeatIds();
            var maxPerSeat = ReadNumberBoxValue(MaxPerSeatBox, 120, 1, 500);

            if (string.IsNullOrEmpty(baseUrl))
            {
                _cachedResult = null;
                GalleryHost.Children.Clear();
                GalleryHost.RowDefinitions.Clear();
                GalleryHost.Children.Add(CreateEmptyText("サーバー URL を入力して「接続」を押してください"));
                SummaryText.Text = "合計表示件数: 0";
                StatusText.Text = "サーバー未設定";
                return;
            }

            if (seatIds.Count == 0)
            {
                _cachedResult = null;
                GalleryHost.Children.Clear();
                GalleryHost.RowDefinitions.Clear();
                GalleryHost.Children.Add(CreateEmptyText(
                    HideDisconnectedCheckBox.IsChecked == true
                        ? "接続中のIDがありません"
                        : "IDを1つ以上選択してください"));
                SummaryText.Text = "合計表示件数: 0";
                StatusText.Text = "ID未選択";
                return;
            }

            StatusText.Text = "読み込み中...";
            var rowLayout = GetRowLayout(seatIds);
            var seatIdsSnapshot = seatIds.ToList();
            var serverUrl = baseUrl;

            var result = await SaveGalleryApiService
                .GetGalleryAsync(serverUrl, seatIdsSnapshot, maxPerSeat)
                .ConfigureAwait(false);
            if (_closed || generation != _loadGeneration)
                return;

            var latestKey = FindLatestEntryKey(result);
            await InvokeOnUiAsync(() =>
            {
                if (_closed || generation != _loadGeneration)
                    return;

                _cachedResult = result;
                _cachedRowLayout = rowLayout;
                _cachedLatestKey = latestKey;
                RenderGallery(result, rowLayout, latestKey);
                SummaryText.Text = $"合計表示件数: {result.Total} / 表示ID: {string.Join(',', rowLayout.Visible)}";
                StatusText.Text = $"更新: {DateTime.Now:HH:mm:ss}（{serverUrl}）";
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SaveViewerWindow] LoadGallery failed: {ex}");
            if (generation != _loadGeneration)
                return;

            await InvokeOnUiAsync(() =>
            {
                if (generation != _loadGeneration)
                    return;

                _cachedResult = null;
                GalleryHost.Children.Clear();
                GalleryHost.RowDefinitions.Clear();
                GalleryHost.Children.Add(CreateEmptyText("一覧の取得に失敗しました"));
                SummaryText.Text = "合計表示件数: 0";
                StatusText.Text = $"取得失敗: {ex.Message}";
            });
        }
        finally
        {
            InvokeOnUi(() =>
            {
                if (generation == _loadGeneration)
                    _loading = false;
            });
        }
    }

    private static string FindLatestEntryKey(SaveGalleryResult result)
    {
        SaveGalleryEntry? latest = null;
        foreach (var bucket in result.BySeat.Values)
        {
            foreach (var item in bucket.Items)
            {
                if (latest is null || item.UpdatedAtMs > latest.UpdatedAtMs)
                    latest = item;
            }
        }

        return latest is null ? string.Empty : BuildEntryKey(latest);
    }

    private static string BuildEntryKey(SaveGalleryEntry entry) => $"{entry.SeatId}|{entry.FileName}";

    private void RenderGallery(
        SaveGalleryResult result,
        (List<int> Top, List<int> Bottom, List<int> Visible) rowLayout,
        string latestKey)
    {
        if (_closed)
            return;

        GalleryHost.Children.Clear();
        GalleryHost.RowDefinitions.Clear();

        var rows = new[] { rowLayout.Top, rowLayout.Bottom }.Where(ids => ids.Count > 0).ToList();
        if (rows.Count == 0)
        {
            GalleryHost.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            GalleryHost.Children.Add(CreateEmptyText("表示できるIDがありません"));
            return;
        }

        var thumbSize = ReadNumberBoxValue(ThumbSizeBox, 96, 60, 220);
        var showFileName = ShowFileNameCheckBox.IsChecked == true;
        var showUpdated = ShowUpdatedCheckBox.IsChecked == true;

        for (var i = 0; i < rows.Count; i++)
            GalleryHost.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            var rowIds = rows[rowIndex];
            var rowGrid = new Grid
            {
                ColumnSpacing = 8,
                VerticalAlignment = VerticalAlignment.Stretch
            };
            Grid.SetRow(rowGrid, rowIndex);

            for (var i = 0; i < rowIds.Count; i++)
                rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            for (var col = 0; col < rowIds.Count; col++)
            {
                var seatId = rowIds[col];
                result.BySeat.TryGetValue(seatId, out var bucket);
                bucket ??= new SaveGallerySeatBucket { SeatId = seatId };
                var seatPanel = BuildSeatPanel(bucket, thumbSize, showFileName, showUpdated, latestKey);
                seatPanel.HorizontalAlignment = HorizontalAlignment.Stretch;
                seatPanel.VerticalAlignment = VerticalAlignment.Stretch;
                Grid.SetColumn(seatPanel, col);
                rowGrid.Children.Add(seatPanel);
            }

            GalleryHost.Children.Add(rowGrid);
        }
    }

    private Border BuildSeatPanel(
        SaveGallerySeatBucket bucket,
        int thumbSize,
        bool showFileName,
        bool showUpdated,
        string latestKey)
    {
        var border = new Border
        {
            Padding = new Thickness(8),
            Background = new SolidColorBrush(Windows.UI.Color.FromArgb(224, 9, 17, 31)),
            BorderBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 39, 65, 95)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };

        var root = new Grid { RowSpacing = 6 };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var header = new Grid();
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.Children.Add(new TextBlock
        {
            Text = $"ID{bucket.SeatId:D2}",
            Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 203, 224, 255)),
            FontSize = 14,
            FontWeight = Microsoft.UI.Text.FontWeights.Bold
        });
        var meta = new TextBlock
        {
            Text = $"表示 {bucket.Items.Count}件 / 全{bucket.TotalCount}件",
            Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 150, 169, 198)),
            FontSize = 11,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        Grid.SetColumn(meta, 1);
        header.Children.Add(meta);
        Grid.SetRow(header, 0);
        root.Children.Add(header);

        if (bucket.Items.Count == 0)
        {
            var empty = CreateEmptyText("保存データがありません");
            Grid.SetRow(empty, 1);
            root.Children.Add(empty);
            border.Child = root;
            return border;
        }

        var thumbGrid = new Grid
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            ColumnSpacing = ThumbGridGap,
            RowSpacing = ThumbGridGap,
            Tag = new SeatThumbGridState
            {
                Items = bucket.Items,
                ThumbSize = thumbSize,
                ShowFileName = showFileName,
                ShowUpdated = showUpdated,
                LatestKey = latestKey
            }
        };

        var scroll = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalAlignment = VerticalAlignment.Stretch,
            Content = thumbGrid
        };
        scroll.SizeChanged += (_, e) =>
        {
            if (e.NewSize.Width > 0)
                LayoutThumbGrid(thumbGrid, e.NewSize.Width);
        };
        scroll.Loaded += (_, _) =>
        {
            if (scroll.ActualWidth > 0)
                LayoutThumbGrid(thumbGrid, scroll.ActualWidth);
        };

        Grid.SetRow(scroll, 1);
        root.Children.Add(scroll);
        border.Child = root;
        return border;
    }

    private void LayoutThumbGrid(Grid grid, double availableWidth)
    {
        if (_closed || grid.Parent is null || grid.Tag is not SeatThumbGridState state || state.Items.Count == 0)
            return;

        try
        {
            grid.Children.Clear();
            grid.RowDefinitions.Clear();
            grid.ColumnDefinitions.Clear();

            var minCell = state.ThumbSize + 8;
            var columns = Math.Max(1, (int)Math.Floor((availableWidth + ThumbGridGap) / (minCell + ThumbGridGap)));

            for (var c = 0; c < columns; c++)
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // 各席の最新（先頭）を上段に2マス分の大きさで配置
            var latestItem = state.Items[0];
            var isGlobalLatest = string.Equals(BuildEntryKey(latestItem), state.LatestKey, StringComparison.Ordinal);
            var featuredSpan = Math.Min(2, columns);

            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var cellWidth = columns > 0
                ? (availableWidth - ThumbGridGap * (columns - 1)) / columns
                : availableWidth;
            var featuredWidth = cellWidth * featuredSpan + ThumbGridGap * (featuredSpan - 1);

            var featuredHost = new Grid { HorizontalAlignment = HorizontalAlignment.Stretch };
            Grid.SetRow(featuredHost, 0);
            Grid.SetColumn(featuredHost, 0);
            Grid.SetColumnSpan(featuredHost, columns);

            var featuredCard = CreateThumbCard(
                latestItem,
                state.ThumbSize,
                state.ShowFileName,
                state.ShowUpdated,
                isGlobalLatest,
                featured: true);
            featuredCard.HorizontalAlignment = HorizontalAlignment.Center;
            featuredCard.MaxWidth = featuredWidth;
            featuredHost.Children.Add(featuredCard);
            grid.Children.Add(featuredHost);

            var restItems = state.Items.Skip(1).ToList();
            if (restItems.Count == 0)
                return;

            var restRows = (int)Math.Ceiling(restItems.Count / (double)columns);
            for (var r = 0; r < restRows; r++)
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            for (var i = 0; i < restItems.Count; i++)
            {
                var item = restItems[i];
                var highlight = string.Equals(BuildEntryKey(item), state.LatestKey, StringComparison.Ordinal);
                var card = CreateThumbCard(item, state.ThumbSize, state.ShowFileName, state.ShowUpdated, highlight, featured: false);
                Grid.SetRow(card, 1 + i / columns);
                Grid.SetColumn(card, i % columns);
                grid.Children.Add(card);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SaveViewerWindow] LayoutThumbGrid failed: {ex}");
        }
    }

    private Border CreateThumbCard(
        SaveGalleryEntry item,
        int thumbSize,
        bool showFileName,
        bool showUpdated,
        bool highlight,
        bool featured)
    {
        var sizeFactor = featured ? FeaturedThumbMultiplier : 1.0;
        var imageHeight = thumbSize * sizeFactor * 9.0 / 16.0;
        var decodeWidth = (int)Math.Round(thumbSize * sizeFactor * 2);

        var card = new Border
        {
            Padding = new Thickness(featured ? 6 : 4),
            Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 19, 32, 58)),
            BorderBrush = new SolidColorBrush(highlight || featured
                ? Windows.UI.Color.FromArgb(255, 90, 167, 255)
                : Windows.UI.Color.FromArgb(255, 26, 45, 69)),
            BorderThickness = new Thickness(highlight || featured ? 2 : 1),
            CornerRadius = new CornerRadius(10),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Tag = item.FilePath
        };

        var stack = new StackPanel { Spacing = 4 };
        var image = new Image
        {
            Height = imageHeight,
            Stretch = Stretch.Uniform,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };

        try
        {
            if (!string.IsNullOrWhiteSpace(item.FilePath)
                && Uri.TryCreate(item.FilePath, UriKind.Absolute, out var imageUri)
                && (imageUri.Scheme == Uri.UriSchemeHttp || imageUri.Scheme == Uri.UriSchemeHttps))
            {
                var bitmap = new BitmapImage { DecodePixelWidth = decodeWidth };
                bitmap.UriSource = imageUri;
                image.Source = bitmap;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SaveViewerWindow] thumb load failed: {ex.Message}");
        }

        image.PointerPressed += async (_, e) =>
        {
            if (!e.GetCurrentPoint(image).Properties.IsLeftButtonPressed)
                return;

            if (!Uri.TryCreate(item.FilePath, UriKind.Absolute, out var openUri))
                return;

            try
            {
                await Windows.System.Launcher.LaunchUriAsync(openUri);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SaveViewerWindow] open image failed: {ex.Message}");
            }
        };

        stack.Children.Add(image);

        if (showFileName || showUpdated)
        {
            var body = new StackPanel { Spacing = 2 };
            body.Children.Add(new TextBlock
            {
                Text = $"S{item.Session:D2}-C{item.Counter:D3} {item.Type}",
                Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 207, 225, 251)),
                FontSize = featured ? 12 : 10,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                TextTrimming = TextTrimming.CharacterEllipsis
            });

            if (showFileName)
            {
                body.Children.Add(new TextBlock
                {
                    Text = item.FileName,
                    Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 229, 238, 252)),
                    FontSize = featured ? 10 : 9,
                    TextTrimming = TextTrimming.CharacterEllipsis
                });
            }

            if (showUpdated)
            {
                body.Children.Add(new TextBlock
                {
                    Text = item.UpdatedAt.ToLocalTime().ToString("yyyy/MM/dd HH:mm:ss"),
                    Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 150, 169, 198)),
                    FontSize = featured ? 10 : 9,
                    TextTrimming = TextTrimming.CharacterEllipsis
                });
            }

            stack.Children.Add(body);
        }

        card.Child = stack;
        return card;
    }

    private static TextBlock CreateEmptyText(string message) =>
        new()
        {
            Text = message,
            Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 150, 169, 198)),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 24, 0, 24),
            TextAlignment = TextAlignment.Center
        };

    private async void OnOnlineUpdateClick(object sender, RoutedEventArgs e)
    {
        if (_onlineUpdateBusy)
            return;

        _onlineUpdateBusy = true;
        OnlineUpdateButton.IsEnabled = false;
        try
        {
            if (Content is not FrameworkElement root)
                return;

            await OnlineUpdateUiHelper.RunAsync(
                root.XamlRoot,
                AppUpdateKind.SaveViewer,
                status => UpdateStatusText.Text = status);
        }
        finally
        {
            _onlineUpdateBusy = false;
            OnlineUpdateButton.IsEnabled = true;
        }
    }

    private void OnRefreshClick(object sender, RoutedEventArgs e) => _ = LoadGalleryAsync();

    private void OnHideToolbarClick(object sender, RoutedEventArgs e) => ApplyToolbarVisibility(false);

    private void OnShowToolbarClick(object sender, RoutedEventArgs e) => ApplyToolbarVisibility(true);

    private void OnSelectAllClick(object sender, RoutedEventArgs e)
    {
        _suppressSettingsSave = true;
        foreach (var check in _seatChecks)
            check.IsChecked = true;
        _suppressSettingsSave = false;
        _ = LoadGalleryAsync();
    }

    private void OnSelectNoneClick(object sender, RoutedEventArgs e)
    {
        _suppressSettingsSave = true;
        foreach (var check in _seatChecks)
            check.IsChecked = false;
        _suppressSettingsSave = false;
        _ = LoadGalleryAsync();
    }

    private void OnSeatSelectionChanged(object sender, RoutedEventArgs e)
    {
        if (_suppressSettingsSave)
            return;

        _ = LoadGalleryAsync();
    }

    private void OnMaxPerSeatChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (_suppressSettingsSave || double.IsNaN(args.NewValue))
            return;

        ScheduleDebouncedReload();
    }

    private void OnThumbSizeChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (_suppressSettingsSave || double.IsNaN(args.NewValue))
            return;

        RefreshGalleryFromCache();
    }

    private void OnLiveUpdateChanged(object sender, RoutedEventArgs e)
    {
        SetupLiveListener();
    }

    private void OnServerConnectClick(object sender, RoutedEventArgs e)
    {
        _settings.ServerUrl = SaveGalleryApiService.NormalizeServerUrl(ServerUrlBox.Text);
        ServerUrlBox.Text = _settings.ServerUrl;
        SaveViewerSettingsStore.Save(_settings);
        SetupLiveListener();
        _ = RefreshConnectedSeatsAsync();
        _ = LoadGalleryAsync();
    }

    private void OnServerUrlKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter)
        {
            e.Handled = true;
            OnServerConnectClick(sender, e);
        }
    }

    private void OnHideDisconnectedChanged(object sender, RoutedEventArgs e) => _ = LoadGalleryAsync();

    private void OnMetaVisibilityChanged(object sender, RoutedEventArgs e)
    {
        if (_suppressSettingsSave)
            return;

        RefreshGalleryFromCache();
    }

    private void OnLayoutTextChanged(object sender, TextChangedEventArgs e) { }

    private void OnApplyLayoutClick(object sender, RoutedEventArgs e) => _ = LoadGalleryAsync();
}
