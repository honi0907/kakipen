using KakiMoni.Core.Drawing;
using KakiMoni.Core.Models;
using KakiMoni_Host.Controls;
using KakiMoni_Host.Layout;
using KakiMoni_Host.SaveViewer;
using KakiMoni_Host.Services;
using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.ApplicationModel.DataTransfer;
using Windows.UI;

namespace KakiMoni_Host;

public sealed partial class CompanelPage : Page
{
    private readonly HostHubService _hub = new();
    private readonly HostImageLoader _images = new();
    private readonly SeatSnapshotRenderer _snapshotRenderer = new();
    private readonly Dictionary<int, SeatCardModel> _seats = Enumerable.Range(1, 10)
        .ToDictionary(i => i, i => new SeatCardModel { SeatId = i });

    private string? _selectedChoiceRelativeUrl;
    private bool _syncingChoiceSelection;
    private bool _syncingSaveUi;
    private int _saveSession = 1;
    private int _saveCounter;
    private readonly HashSet<int> _pendingJudgeSaves = new();
    private readonly SemaphoreSlim[] _blackoutClickGates = Enumerable.Range(0, 11)
        .Select(_ => new SemaphoreSlim(1, 1))
        .ToArray();
    private readonly Dictionary<int, TaskCompletionSource<bool>> _blackoutConfirmations = new();
    private bool _networkUiReady;

    public CompanelPage()
    {
        InitializeComponent();
        SeatRepeater.ItemsSource = _seats.Values.OrderBy(s => s.SeatId).ToList();
        SeatRepeater.ElementPrepared += OnSeatElementPrepared;
        foreach (var model in _seats.Values)
        {
            model.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(SeatCardModel.IsSelected))
                {
                    RefreshSelectedBadge();
                    RefreshSelectAllButton();
                }
            };
        }
        WireActionBoard();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        InitializeNetworkUi();
        UpdateUrlText();
        UpdateSaveUi();
    }

    private void InitializeNetworkUi()
    {
        var settings = HostSettingsStore.Load();
        _networkUiReady = false;
        HostNetworkUiHelper.BindAdapterCombo(NetworkAdapterCombo, settings);
        _networkUiReady = true;
        NetworkAdapterCombo.IsEnabled = !AppHostContext.Server.IsRunning;
    }

    private void OnNetworkSettingChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_networkUiReady || AppHostContext.Server.IsRunning)
            return;

        var settings = HostSettingsStore.Load();
        HostNetworkUiHelper.ApplyComboSelectionToSettings(NetworkAdapterCombo, settings);
        HostSettingsStore.Save(settings);
        UpdateUrlText();
    }

    private void OnPageSizeChanged(object sender, SizeChangedEventArgs e) =>
        UpdateSeatGridMetrics();

    private void OnSeatsHostSizeChanged(object sender, SizeChangedEventArgs e) =>
        UpdateSeatGridMetrics();

    private void UpdateSeatGridMetrics()
    {
        var w = SeatsHost.ActualWidth;
        var h = SeatsHost.ActualHeight;
        if (w <= 0 || h <= 0) return;

        var (cellW, cellH) = CompanelLayoutMetrics.ComputeSeatCellSize(w, h);
        SeatGridLayout.MinItemWidth = cellW;
        SeatGridLayout.MinItemHeight = cellH;
    }

    private void WireActionBoard()
    {
        ActionBoard.BindSeats(_seats);
        ActionBoard.SeatSelectClicked += OnBoardSeatSelectClicked;
        ActionBoard.SeatLockClicked += OnBoardSeatLockClicked;
        ActionBoard.SeatBlackoutClicked += OnBoardSeatBlackoutClicked;
        ActionBoard.SelectAllToggleClicked += OnBoardSelectAllToggleClicked;
        ActionBoard.LockAllClicked += OnBoardLockAllClicked;
    }

    private void OnSeatElementPrepared(ItemsRepeater sender, ItemsRepeaterElementPreparedEventArgs args)
    {
        if (args.Element is not SeatCardView view) return;
        view.ClearClicked -= OnSeatClearClicked;
        view.ClearClicked += OnSeatClearClicked;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        CompanelWindowHelper.EnsureCompanelWindowSize(App.HostWindow);
        InitializeNetworkUi();
        UpdateUrlText();
        SetGlobalControlsEnabled(AppHostContext.Server.IsRunning);
        UpdateSeatGridMetrics();

        try
        {
            if (!AppHostContext.Server.IsRunning)
            {
                UpdateHubConnectionDot(false, false);
                ConnBadgeText.Text = "未起動";
                ConnText.Text = "サーバー未起動";
                return;
            }

            _hub.FullStateReceived += OnFullState;
            _hub.StrokeStartReceived += OnStrokeStart;
            _hub.StrokePointReceived += OnStrokePoint;
            _hub.StrokeEndReceived += OnStrokeEnd;
            _hub.SeatLockedReceived += OnSeatLocked;
            _hub.SeatUnlockedReceived += OnSeatUnlocked;
            _hub.AllLockedReceived += OnAllLocked;
            _hub.AllUnlockedReceived += OnAllUnlocked;
            _hub.ClientDisconnectedReceived += OnClientDisconnected;
            _hub.ClientRegisteredReceived += OnClientRegistered;
            _hub.CanvasClearedReceived += OnCanvasCleared;
            _hub.ChoiceChangedReceived += OnChoiceChanged;
            _hub.SeatRevealedReceived += OnSeatRevealed;
            _hub.SeatHiddenReceived += OnSeatHidden;
            _hub.JudgeResultReceived += OnJudgeResult;
            _hub.SeatWritingBlackoutReceived += OnSeatWritingBlackout;
            _hub.ConnectionChanged += OnHubConnectionChanged;

            await _hub.ConnectAsync(AppHostContext.Server.LocalBaseUrl);
            await SyncJudgeColorModeAsync();
            await SyncLockOverlayOpacityAsync();
            await SyncUseSeatNameFileAsync();
            AppHostContext.DisplayOutput.BindSeats(_seats);
            ConnBadgeText.Text = "Hub接続済";
            ConnText.Text = string.Empty;
            RefreshLockAllButton();
            RefreshSelectAllButton();
            await RefreshChoiceListAsync();
            await LoadSaveStateAsync();
            DispatcherQueue.TryEnqueue(UpdateSeatGridMetrics);
        }
        catch (Exception ex)
        {
            UpdateHubConnectionDot(false, false);
            ConnBadgeText.Text = "Hub切断";
            ConnText.Text = $"接続失敗: {ex.Message}";
        }
        finally
        {
            App.HostWindow?.HideBusyOverlay();
        }
    }

    private async void OnUnloaded(object sender, RoutedEventArgs e)
    {
        App.HostWindow?.HideBusyOverlay();
        _hub.FullStateReceived -= OnFullState;
        _hub.StrokeStartReceived -= OnStrokeStart;
        _hub.StrokePointReceived -= OnStrokePoint;
        _hub.StrokeEndReceived -= OnStrokeEnd;
        _hub.SeatLockedReceived -= OnSeatLocked;
        _hub.SeatUnlockedReceived -= OnSeatUnlocked;
        _hub.AllLockedReceived -= OnAllLocked;
        _hub.AllUnlockedReceived -= OnAllUnlocked;
        _hub.ClientDisconnectedReceived -= OnClientDisconnected;
        _hub.ClientRegisteredReceived -= OnClientRegistered;
        _hub.CanvasClearedReceived -= OnCanvasCleared;
        _hub.ChoiceChangedReceived -= OnChoiceChanged;
        _hub.SeatRevealedReceived -= OnSeatRevealed;
        _hub.SeatHiddenReceived -= OnSeatHidden;
        _hub.JudgeResultReceived -= OnJudgeResult;
        _hub.SeatWritingBlackoutReceived -= OnSeatWritingBlackout;
        _hub.ConnectionChanged -= OnHubConnectionChanged;
        await _hub.DisposeAsync();
    }

    private void SetGlobalControlsEnabled(bool enabled)
    {
        GoButton.IsEnabled = enabled;
        JudgeGoButton.IsEnabled = enabled;
        ClearJudgeButton.IsEnabled = enabled;
        StandbyButton.IsEnabled = enabled;
        ChoiceComboBox.IsEnabled = enabled;
        SendChoiceButton.IsEnabled = enabled;
        SettingsButton.IsEnabled = enabled;
        LayoutButton.IsEnabled = enabled;
        LauncherButton.IsEnabled = true;
    }

    private void UpdateUrlText()
    {
        if (!AppHostContext.Server.IsRunning)
        {
            UrlText.Text = string.Empty;
            CopyChildUrlButton.IsEnabled = false;
            NetworkAdapterCombo.IsEnabled = true;
            return;
        }

        var childUrl = AppHostContext.Server.ChildBaseUrl;
        UrlText.Text = string.IsNullOrWhiteSpace(childUrl) ? "（LAN IP 未検出）" : childUrl;
        CopyChildUrlButton.IsEnabled = !string.IsNullOrWhiteSpace(childUrl);
        NetworkAdapterCombo.IsEnabled = false;
    }

    private void OnCopyChildUrlClick(object sender, RoutedEventArgs e)
    {
        var text = UrlText.Text?.Trim();
        if (string.IsNullOrWhiteSpace(text) || text.StartsWith('（'))
            return;

        var package = new DataPackage { RequestedOperation = DataPackageOperation.Copy };
        package.SetText(text);
        Clipboard.SetContent(package);
    }

    private void OnHubConnectionChanged(bool connected, bool reconnecting) =>
        DispatcherQueue.TryEnqueue(() =>
        {
            UpdateHubConnectionDot(connected, reconnecting);
            ConnBadgeText.Text = connected ? "Hub接続済" : reconnecting ? "Hub再接続中" : "Hub切断";
            if (connected)
                ConnText.Text = string.Empty;
            else if (reconnecting)
                ConnText.Text = "再接続中...";
        });

    private void UpdateHubConnectionDot(bool connected, bool reconnecting)
    {
        HubConnDot.Fill = new SolidColorBrush(
            connected ? Color.FromArgb(255, 74, 222, 128)
            : reconnecting ? Color.FromArgb(255, 251, 191, 36)
            : Color.FromArgb(255, 100, 116, 139));
        ConnBadge.Background = new SolidColorBrush(
            connected ? Color.FromArgb(255, 22, 101, 52)
            : reconnecting ? Color.FromArgb(255, 120, 53, 15)
            : Color.FromArgb(255, 55, 65, 81));
    }

    private void OnClientRegistered(int seatId) =>
        DispatcherQueue.TryEnqueue(() =>
        {
            if (_seats.TryGetValue(seatId, out var model))
                model.IsReconnecting = false;
        });

    private void OnClientDisconnected(int seatId) =>
        DispatcherQueue.TryEnqueue(() =>
        {
            if (_seats.TryGetValue(seatId, out var model))
            {
                model.IsReconnecting = true;
                model.IsConnected = false;
            }
            RefreshLockAllButton();
            RefreshSelectAllButton();
            ActionBoard.RefreshAll();
        });

    private void OnCanvasCleared(int seatId) =>
        DispatcherQueue.TryEnqueue(() =>
        {
            if (_seats.TryGetValue(seatId, out var model))
                model.ClearStrokes();
        });

    private void OnFullState(IReadOnlyList<SeatClientState> seats)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            foreach (var seat in seats)
            {
                if (!_seats.TryGetValue(seat.SeatId, out var model)) continue;
                model.ApplyState(seat);
            }

            RefreshLockAllButton();
            RefreshSelectAllButton();
            ActionBoard.RefreshAll();
        });
    }

    private void OnStrokeStart(int seatId, StrokeData stroke)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            if (_seats.TryGetValue(seatId, out var model))
                model.BeginStroke(stroke);
        });
    }

    private void OnStrokePoint(int seatId, StrokePoint point)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            if (_seats.TryGetValue(seatId, out var model))
                model.AddPoint(point);
        });
    }

    private void OnStrokeEnd(int seatId)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            if (_seats.TryGetValue(seatId, out var model))
                model.EndStroke();
        });
    }

    private void OnSeatLocked(int seatId) =>
        DispatcherQueue.TryEnqueue(() => SetSeatLocked(seatId, true));

    private void OnSeatUnlocked(int seatId) =>
        DispatcherQueue.TryEnqueue(() => SetSeatLocked(seatId, false));

    private void OnAllLocked() =>
        DispatcherQueue.TryEnqueue(() =>
        {
            foreach (var model in _seats.Values)
                model.IsLocked = true;
            RefreshLockAllButton();
            ActionBoard.RefreshAll();
        });

    private void OnAllUnlocked() =>
        DispatcherQueue.TryEnqueue(() =>
        {
            foreach (var model in _seats.Values)
                model.IsLocked = false;
            RefreshLockAllButton();
            ActionBoard.RefreshAll();
        });

    private void SetSeatLocked(int seatId, bool locked)
    {
        if (_seats.TryGetValue(seatId, out var model))
            model.IsLocked = locked;
        RefreshLockAllButton();
        ActionBoard.RefreshSeat(seatId);
    }

    private void OnSeatWritingBlackout(int seatId, bool enabled) =>
        DispatcherQueue.TryEnqueue(() => SetSeatWritingBlackout(seatId, enabled));

    private void SetSeatWritingBlackout(int seatId, bool enabled)
    {
        if (_seats.TryGetValue(seatId, out var model))
            model.WritingBlackout = enabled;
        ActionBoard.RefreshSeat(seatId);

        if (_blackoutConfirmations.Remove(seatId, out var tcs))
            tcs.TrySetResult(enabled);
    }

    private void RefreshLockAllButton()
    {
        var connected = _seats.Values.Where(s => s.IsConnected).ToList();
        var allLocked = connected.Count > 0 && connected.All(s => s.IsLocked);
        ActionBoard.RefreshLockAllLabel(allLocked);
    }

    private void RefreshSelectAllButton()
    {
        var connected = _seats.Values.Where(s => s.IsConnected).ToList();
        var allSelected = connected.Count > 0 && connected.All(s => s.IsSelected);
        ActionBoard.RefreshSelectAllLabel(allSelected);
    }

    private void RefreshSelectedBadge()
    {
        var count = _seats.Values.Count(s => s.IsConnected && s.IsSelected);
        if (count > 0)
        {
            SelectedBadge.Visibility = Visibility.Visible;
            SelectedBadgeText.Text = $"選択中: {count}席";
        }
        else
        {
            SelectedBadge.Visibility = Visibility.Collapsed;
        }
    }

    private void OnLauncherClick(object sender, RoutedEventArgs e)
    {
        CompanelWindowHelper.EnsureLauncherWindowSize(App.HostWindow);
        if (Frame?.CanGoBack == true)
            Frame.GoBack();
        else
            Frame?.Navigate(typeof(MainPage));
    }

    private void OnBoardSeatSelectClicked(object? sender, int seatId)
    {
        if (!_seats.TryGetValue(seatId, out var model) || !model.IsConnected) return;
        model.IsSelected = !model.IsSelected;
        RefreshSelectedBadge();
        RefreshSelectAllButton();
        ActionBoard.RefreshSeat(seatId);
    }

    private async void OnBoardSeatLockClicked(object? sender, int seatId)
    {
        if (!_seats.TryGetValue(seatId, out var model) || !model.IsConnected) return;
        if (model.IsLocked)
            await _hub.HostUnlockAsync(seatId);
        else
            await _hub.HostLockAsync(seatId);
    }

    private async void OnBoardSeatBlackoutClicked(object? sender, int seatId)
    {
        if (seatId is < 1 or > 10) return;
        if (!_seats.TryGetValue(seatId, out var model) || !model.IsConnected) return;

        await _blackoutClickGates[seatId].WaitAsync();
        try
        {
            if (!_seats.TryGetValue(seatId, out model) || !model.IsConnected) return;

            var next = !model.WritingBlackout;
            var confirmation = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            _blackoutConfirmations[seatId] = confirmation;

            await _hub.HostSetWritingBlackoutAsync(seatId, next);

            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            try
            {
                await confirmation.Task.WaitAsync(timeout.Token);
            }
            catch (OperationCanceledException)
            {
                _blackoutConfirmations.Remove(seatId);
            }
        }
        finally
        {
            _blackoutClickGates[seatId].Release();
        }
    }

    private void OnBoardSelectAllToggleClicked(object? sender, EventArgs e)
    {
        var connected = _seats.Values.Where(s => s.IsConnected).ToList();
        if (connected.Count == 0) return;

        if (connected.All(s => s.IsSelected))
        {
            foreach (var model in _seats.Values)
                model.IsSelected = false;
        }
        else
        {
            foreach (var model in connected)
                model.IsSelected = true;
        }

        RefreshSelectedBadge();
        RefreshSelectAllButton();
        ActionBoard.RefreshAll();
    }

    private async void OnBoardLockAllClicked(object? sender, EventArgs e)
    {
        var connected = _seats.Values.Where(s => s.IsConnected).ToList();
        if (connected.Count == 0) return;

        if (connected.All(s => s.IsLocked))
            await _hub.HostUnlockAllAsync();
        else
            await _hub.HostLockAllAsync();
    }

    private async void OnSeatClearClicked(object sender, SeatCardModel model)
    {
        await _hub.HostClearStrokesOnlyAsync(model.SeatId);
        model.ClearStrokes();
    }

    private async Task RefreshChoiceListAsync()
    {
        if (!AppHostContext.Server.IsRunning) return;

        try
        {
            var entries = await HostApiService.GetChoicesAsync(AppHostContext.Server.LocalBaseUrl);
            var selectedUrl = _selectedChoiceRelativeUrl;
            ChoiceComboBox.ItemsSource = entries;

            if (!string.IsNullOrWhiteSpace(selectedUrl))
            {
                var match = entries.FirstOrDefault(e =>
                    string.Equals(e.RelativeUrl, selectedUrl, StringComparison.OrdinalIgnoreCase));
                ChoiceComboBox.SelectedItem = match;
            }
        }
        catch (Exception ex)
        {
            ConnText.Text = $"選択肢一覧取得失敗: {ex.Message}";
        }
    }

    private void OnChoiceSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_syncingChoiceSelection) return;

        if (ChoiceComboBox.SelectedItem is BackgroundFileEntry entry)
        {
            _selectedChoiceRelativeUrl = entry.RelativeUrl;
            _ = UpdateChoiceThumbPreviewAsync();
            return;
        }

        _selectedChoiceRelativeUrl = null;
        ChoiceThumbImage.Source = null;
        ChoiceThumbImage.Visibility = Visibility.Collapsed;
    }

    private async Task UpdateChoiceThumbPreviewAsync()
    {
        if (string.IsNullOrWhiteSpace(_selectedChoiceRelativeUrl))
        {
            ChoiceThumbImage.Source = null;
            ChoiceThumbImage.Visibility = Visibility.Collapsed;
            return;
        }

        var thumb = await _images.LoadThumbnailAsync(_selectedChoiceRelativeUrl, 256);
        if (thumb is null)
        {
            ChoiceThumbImage.Source = null;
            ChoiceThumbImage.Visibility = Visibility.Collapsed;
            return;
        }

        ChoiceThumbImage.Source = thumb;
        ChoiceThumbImage.Visibility = Visibility.Visible;
    }

    private async void OnSendChoiceClick(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_selectedChoiceRelativeUrl))
        {
            ConnText.Text = "先に選択肢画像を選んでください。";
            return;
        }

        SendChoiceButton.IsEnabled = false;
        try
        {
            await _hub.HostShowChoiceAsync(_selectedChoiceRelativeUrl);
        }
        finally
        {
            SendChoiceButton.IsEnabled = true;
        }
    }

    private void OnChoiceChanged(string relativeUrl) =>
        DispatcherQueue.TryEnqueue(() =>
        {
            _syncingChoiceSelection = true;
            try
            {
                if (string.IsNullOrWhiteSpace(relativeUrl))
                {
                    _selectedChoiceRelativeUrl = null;
                    ChoiceComboBox.SelectedItem = null;
                    ChoiceThumbImage.Source = null;
                    ChoiceThumbImage.Visibility = Visibility.Collapsed;
                }
                else
                {
                    _selectedChoiceRelativeUrl = relativeUrl;
                    if (ChoiceComboBox.ItemsSource is IEnumerable<BackgroundFileEntry> entries)
                    {
                        ChoiceComboBox.SelectedItem = entries.FirstOrDefault(e =>
                            string.Equals(e.RelativeUrl, relativeUrl, StringComparison.OrdinalIgnoreCase));
                    }

                    _ = UpdateChoiceThumbPreviewAsync();
                }
            }
            finally
            {
                _syncingChoiceSelection = false;
            }

            foreach (var model in _seats.Values)
                model.ChoiceImageUrl = string.IsNullOrWhiteSpace(relativeUrl) ? null : relativeUrl;
        });

    private void OnJudgeResult(int seatId, string url) =>
        DispatcherQueue.TryEnqueue(async () =>
        {
            if (_seats.TryGetValue(seatId, out var model))
                model.OverlayImageUrl = string.IsNullOrWhiteSpace(url) ? null : url;

            ActionBoard.RefreshSeat(seatId);

            if (_pendingJudgeSaves.Remove(seatId))
                await SaveSeatSnapshotAsync(seatId, "JUDGE", url);
        });

    private void OnSeatRevealed(int seatId, string anim) =>
        DispatcherQueue.TryEnqueue(() =>
        {
            if (_seats.TryGetValue(seatId, out var model))
            {
                model.Revealed = true;
                ConnText.Text = $"GO: 席 {seatId} を表示";
            }
            ActionBoard.RefreshSeat(seatId);
        });

    private void OnSeatHidden(int seatId) =>
        DispatcherQueue.TryEnqueue(() =>
        {
            if (_seats.TryGetValue(seatId, out var model))
            {
                model.Revealed = false;
                ConnText.Text = $"非表示: 席 {seatId}";
            }
            ActionBoard.RefreshSeat(seatId);
        });

    private async void OnGoClick(object sender, RoutedEventArgs e)
    {
        var connected = _seats.Values.Where(s => s.IsConnected).ToList();
        if (connected.Count == 0)
        {
            ConnText.Text = "GO: 接続中の席がありません";
            return;
        }

        var revealTargets = connected.Where(s => s.IsSelected).ToList();
        if (revealTargets.Count == 0)
            revealTargets = connected;

        GoButton.IsEnabled = false;
        try
        {
            if (!_hub.IsConnected)
            {
                ConnText.Text = "GO: ホスト未接続";
                return;
            }

            foreach (var model in connected)
            {
                if (revealTargets.Contains(model))
                    await _hub.HostRevealAsync(model.SeatId, "cut");
                else
                    await _hub.HostHideAsync(model.SeatId);
            }

            try
            {
                var state = await HostSaveApiService.NextCounterAsync(AppHostContext.Server.LocalBaseUrl);
                ApplySaveState(state);
                foreach (var model in revealTargets)
                    await SaveSeatSnapshotAsync(model.SeatId, "GO");
            }
            catch (Exception saveEx)
            {
                ConnText.Text = $"GO 保存失敗: {saveEx.Message}";
                return;
            }

            ConnText.Text = $"GO: {string.Join(", ", revealTargets.Select(s => s.SeatId))} 番席 (S{_saveSession}_#{_saveCounter:D3})";
            ActionBoard.RefreshAll();
        }
        catch (Exception ex)
        {
            ConnText.Text = $"GO 失敗: {ex.Message}";
        }
        finally
        {
            GoButton.IsEnabled = true;
        }
    }

    /// <summary>
    /// 判定GO 対象席。選択席を優先し、未選択時は GO 済み（Revealed）席へフォールバック。
    /// GO ボタンと同様、選択なし GO で全席表示したケースでも判定できるようにする。
    /// </summary>
    private List<SeatCardModel> ResolveJudgeTargets()
    {
        var connected = _seats.Values.Where(s => s.IsConnected).ToList();
        var selected = connected.Where(s => s.IsSelected).ToList();
        if (selected.Count > 0)
            return selected;

        return connected.Where(s => s.Revealed).ToList();
    }

    private async void OnJudgeGoClick(object sender, RoutedEventArgs e)
    {
        var targets = ResolveJudgeTargets();
        if (targets.Count == 0)
        {
            ConnText.Text = "判定対象の席がありません（席を選択するか GO 後に実行）。";
            return;
        }

        JudgeGoButton.IsEnabled = false;
        try
        {
            if (!_hub.IsConnected)
            {
                ConnText.Text = "判定GO: ホスト未接続";
                return;
            }

            foreach (var model in targets)
            {
                _pendingJudgeSaves.Add(model.SeatId);
                await _hub.HostJudgeAsync(model.SeatId, model.JudgeKind);
            }

            ConnText.Text = $"判定GO: {string.Join(", ", targets.Select(s => s.SeatId))} 番席";
            ActionBoard.RefreshAll();
        }
        catch (Exception ex)
        {
            ConnText.Text = $"判定GO 失敗: {ex.Message}";
        }
        finally
        {
            JudgeGoButton.IsEnabled = true;
        }
    }

    private async void OnClearJudgeClick(object sender, RoutedEventArgs e)
    {
        ClearJudgeButton.IsEnabled = false;
        try
        {
            await _hub.HostClearOverlayAsync();
            _pendingJudgeSaves.Clear();
            foreach (var model in _seats.Values)
                model.OverlayImageUrl = null;
            ActionBoard.RefreshAll();
        }
        finally
        {
            ClearJudgeButton.IsEnabled = true;
        }
    }

    private async void OnStandbyClick(object sender, RoutedEventArgs e)
    {
        var dialog = new ContentDialog
        {
            Title = "スタンバイ",
            Content = "全席の描画・判定・選択肢をクリアし、外部出力をロゴに戻しますか？",
            PrimaryButtonText = "クリア",
            CloseButtonText = "キャンセル",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
            return;

        StandbyButton.IsEnabled = false;
        try
        {
            await _hub.HostStandbyAsync();
            if (HostSettingsStore.Load().StandbyUnlockAll)
                await _hub.HostUnlockAllAsync();
            ApplyStandbyLocalState();
            ConnText.Text = HostSettingsStore.Load().StandbyUnlockAll
                ? "スタンバイ: 全席クリア・ロック解除"
                : "スタンバイ: 全席クリア";
        }
        catch (Exception ex)
        {
            ConnText.Text = $"スタンバイ失敗: {ex.Message}";
        }
        finally
        {
            StandbyButton.IsEnabled = true;
        }
    }

    private async void OnSettingsClick(object sender, RoutedEventArgs e)
    {
        if (!await HostSettingsDialog.ShowAsync(XamlRoot))
            return;

        await SyncJudgeColorModeAsync();
        await SyncLockOverlayOpacityAsync();
        await SyncUseSeatNameFileAsync();
        RefreshAllSeatPreviews();
    }

    private void OnLayoutClick(object sender, RoutedEventArgs e) =>
        LayoutEditorWindowHelper.ShowOrActivate();

    private void OnSaveViewerClick(object sender, RoutedEventArgs e) =>
        SaveViewerWindowHelper.ShowOrActivate();

    private async Task SyncUseSeatNameFileAsync()
    {
        if (!_hub.IsConnected)
            return;

        await _hub.HostSetUseSeatNameFileAsync(HostSettingsStore.Load().UseSeatNameFile);
    }

    private async Task SyncJudgeColorModeAsync()
    {
        if (!_hub.IsConnected)
            return;

        await _hub.HostSetJudgeColorModeAsync(HostSettingsStore.Load().JudgeColorMode);
    }

    private async Task SyncLockOverlayOpacityAsync()
    {
        if (!_hub.IsConnected)
            return;

        await _hub.HostSetLockOverlayOpacityAsync(HostSettingsStore.Load().LockOverlayOpacityPercent);
    }

    private void RefreshAllSeatPreviews()
    {
        for (var i = 0; i < SeatRepeater.ItemsSourceView.Count; i++)
        {
            if (SeatRepeater.TryGetElement(i) is SeatCardView view)
                view.InvalidatePreview();
        }
    }

    private void ApplyStandbyLocalState()
    {
        _pendingJudgeSaves.Clear();

        foreach (var model in _seats.Values)
        {
            model.ClearStrokes();
            model.OverlayImageUrl = null;
            model.ChoiceImageUrl = null;
            model.IsSelected = false;
            model.Revealed = false;
        }

        _syncingChoiceSelection = true;
        try
        {
            _selectedChoiceRelativeUrl = null;
            ChoiceComboBox.SelectedItem = null;
            ChoiceThumbImage.Source = null;
            ChoiceThumbImage.Visibility = Visibility.Collapsed;
        }
        finally
        {
            _syncingChoiceSelection = false;
        }

        RefreshSelectedBadge();
        RefreshSelectAllButton();
        RefreshLockAllButton();
        ActionBoard.RefreshAll();
    }

    private async Task LoadSaveStateAsync()
    {
        if (!AppHostContext.Server.IsRunning) return;

        try
        {
            var state = await HostSaveApiService.GetStateAsync(AppHostContext.Server.LocalBaseUrl);
            ApplySaveState(state);
        }
        catch (Exception ex)
        {
            ConnText.Text = $"Save 状態取得失敗: {ex.Message}";
        }
    }

    private void ApplySaveState(KakiMoni.Core.Models.SaveStateDto state)
    {
        _saveSession = state.Session;
        _saveCounter = state.Counter;
        UpdateSaveUi();
    }

    private void UpdateSaveUi()
    {
        _syncingSaveUi = true;
        try
        {
            if (SaveSessionBox.FocusState == FocusState.Unfocused)
                SaveSessionBox.Value = _saveSession;
            if (SaveCounterBox.FocusState == FocusState.Unfocused)
                SaveCounterBox.Value = _saveCounter;
        }
        finally
        {
            _syncingSaveUi = false;
        }
    }

    private void OnSaveSessionLostFocus(object sender, RoutedEventArgs e)
    {
        if (_syncingSaveUi || !AppHostContext.Server.IsRunning)
            return;

        if (double.IsNaN(SaveSessionBox.Value))
        {
            DispatcherQueue.TryEnqueue(UpdateSaveUi);
            return;
        }

        var next = Math.Clamp((int)SaveSessionBox.Value, 1, 99);
        if (next == _saveSession)
            return;

        var previous = _saveSession;
        DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () => _ = ApplySessionChangeAsync(next, previous));
    }

    private async Task ApplySessionChangeAsync(int next, int previous)
    {
        if (!AppHostContext.Server.IsRunning)
            return;

        _syncingSaveUi = true;
        SaveSessionBox.Value = previous;
        _syncingSaveUi = false;

        try
        {
            var confirm = new ContentDialog
            {
                Title = "Session 変更",
                Content = $"Session を {previous} → {next} に変更します。連番は 000 にリセットされます。",
                PrimaryButtonText = "変更",
                CloseButtonText = "キャンセル",
                XamlRoot = XamlRoot
            };

            if (await confirm.ShowAsync() != ContentDialogResult.Primary)
                return;

            var state = await HostSaveApiService.SetSessionAsync(AppHostContext.Server.LocalBaseUrl, next);
            ApplySaveState(state);
        }
        catch (Exception ex)
        {
            ConnText.Text = $"Session 変更失敗: {ex.Message}";
            UpdateSaveUi();
        }
    }

    private void OnSaveCounterLostFocus(object sender, RoutedEventArgs e)
    {
        if (_syncingSaveUi || !AppHostContext.Server.IsRunning)
            return;

        if (double.IsNaN(SaveCounterBox.Value))
        {
            DispatcherQueue.TryEnqueue(UpdateSaveUi);
            return;
        }

        var next = Math.Clamp((int)SaveCounterBox.Value, 0, 9999);
        if (next == _saveCounter)
            return;

        DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () => _ = ApplyCounterChangeAsync(next));
    }

    private async Task ApplyCounterChangeAsync(int next)
    {
        if (!AppHostContext.Server.IsRunning)
            return;

        try
        {
            var state = await HostSaveApiService.SetCounterAsync(AppHostContext.Server.LocalBaseUrl, next);
            ApplySaveState(state);
        }
        catch (Exception ex)
        {
            ConnText.Text = $"連番変更失敗: {ex.Message}";
            UpdateSaveUi();
        }
    }

    private async Task SaveSeatSnapshotAsync(int seatId, string saveType, string? overlayUrl = null)
    {
        if (!_seats.TryGetValue(seatId, out var model) || !AppHostContext.Server.IsRunning)
            return;

        var settings = HostSettingsStore.Load();
        var overlay = overlayUrl ?? model.OverlayImageUrl;
        var invertJudge = saveType.Equals("JUDGE", StringComparison.OrdinalIgnoreCase)
            && settings.JudgeColorMode
            && ColorInvertHelper.IsFillOverlayUrl(overlay);

        var png = await _snapshotRenderer.RenderPngAsync(
            model,
            _selectedChoiceRelativeUrl,
            saveType,
            overlayUrl,
            AppHostContext.Server.LocalBaseUrl,
            invertJudge);

        var fileName = await HostSaveApiService.SaveSnapshotAsync(
            AppHostContext.Server.LocalBaseUrl,
            seatId,
            _saveSession,
            _saveCounter,
            saveType,
            png);

        ConnText.Text = $"保存: {fileName}";
    }
}
