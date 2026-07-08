using System.Diagnostics;
using KakiMoni.Core.Updates;
using KakiMoni_Client.Controls;
using KakiMoni_Client.Services;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Input;

namespace KakiMoni_Client;

public sealed partial class SetupPage : Page
{
    private readonly Stopwatch _startup = Stopwatch.StartNew();
    private readonly BackgroundImageService _images = AppServices.BackgroundImages;
    private ClientSettings _settings = new();
    private CancellationTokenSource? _bgCheckCts;
    private CancellationTokenSource? _logoCheckCts;
    private bool _connecting;
    private bool _launching;
    private bool _uiReady;
    private bool _suppressServerUrlEvents;

    public SetupPage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnNavSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (NavList.SelectedItem is not ListViewItem item || item.Tag is not string sectionId)
            return;

        ShowSection(sectionId);
    }

    private void ShowSection(string sectionId)
    {
        ConnectionSection.Visibility = sectionId == "connection" ? Visibility.Visible : Visibility.Collapsed;
        PenSection.Visibility = sectionId == "pen" ? Visibility.Visible : Visibility.Collapsed;
        AssetsSection.Visibility = sectionId == "assets" ? Visibility.Visible : Visibility.Collapsed;
        DisplaySection.Visibility = sectionId == "display" ? Visibility.Visible : Visibility.Collapsed;
        UpdateSection.Visibility = sectionId == "update" ? Visibility.Visible : Visibility.Collapsed;

        DetailTitleText.Text = sectionId switch
        {
            "connection" => "接続",
            "pen" => "ペン",
            "assets" => "アセット確認",
            "display" => "表示",
            "update" => "アプリ更新",
            _ => string.Empty
        };
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        StartupText.Text = $"[Startup] SetupPage Loaded {_startup.ElapsedMilliseconds}ms";
        VersionText.Text = AppVersionDisplay.Label;
        Debug.WriteLine(StartupText.Text);

        ReloadSettingsUi();
        NavList.SelectedIndex = 0;
        ShowSection("connection");
        _uiReady = true;
        SyncConnectionStateFromHub();
        _ = RefreshBgStatusAsync();
        _ = RefreshLogoStatusAsync();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _bgCheckCts?.Cancel();
        _bgCheckCts?.Dispose();
        _bgCheckCts = null;
        _logoCheckCts?.Cancel();
        _logoCheckCts?.Dispose();
        _logoCheckCts = null;
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (_uiReady)
        {
            ReloadSettingsUi();
            SyncConnectionStateFromHub();
            _ = RefreshBgStatusAsync();
            _ = RefreshLogoStatusAsync();
        }
    }

    private bool IsServerConnected =>
        AppServices.Hub is { IsConnected: true };

    private void SyncConnectionStateFromHub()
    {
        if (!_uiReady)
            return;

        UpdateConnectionUi();
    }

    private void UpdateConnectionUi()
    {
        var connected = IsServerConnected;
        ConnectionStatusBar.IsOpen = connected;
        LaunchWritingButton.IsEnabled = connected && !_connecting && !_launching;
        ServerUrlBox.IsEnabled = !connected && !_connecting;
        RemoveSavedServerUrlButton.IsEnabled = !connected && !_connecting
            && IsSavedServerUrl(ServerUrlBox.Text);
        SeatIdBox.IsEnabled = !connected && !_connecting;
        UpdateServerToggleButton();
    }

    private void UpdateServerToggleButton()
    {
        var connected = IsServerConnected;

        if (_connecting)
        {
            ServerToggleButton.Content = "接続中...";
            ServerToggleButton.IsEnabled = false;
            return;
        }

        if (_launching)
        {
            ServerToggleButton.IsEnabled = false;
            ServerToggleButton.Content = connected ? "サーバー OFF" : "サーバーへ接続";
            return;
        }

        ServerToggleButton.IsEnabled = true;
        ServerToggleButton.Content = connected ? "サーバー OFF" : "サーバーへ接続";
        if (connected)
        {
            ServerToggleButton.ClearValue(Button.StyleProperty);
            ServerToggleButton.Background = new SolidColorBrush(ColorHelper.FromArgb(255, 220, 38, 38));
            ServerToggleButton.Foreground = new SolidColorBrush(Colors.White);
        }
        else
        {
            ServerToggleButton.ClearValue(Button.BackgroundProperty);
            ServerToggleButton.ClearValue(Button.ForegroundProperty);
            ServerToggleButton.Style = (Style)Application.Current.Resources["AccentButtonStyle"];
        }
    }

    private void ReloadSettingsUi()
    {
        _settings = ClientSettingsStore.Load();
        RefreshServerUrlComboItems(_settings.ServerUrl ?? ClientApiService.DefaultServerUrl);
        SeatIdBox.Value = _settings.SeatId;
        PenSizeSlider.Value = _settings.PenSize;
        EraserSizeSlider.Value = _settings.EraserSize;
        EraserAutoPenSecondsBox.Value = _settings.EraserAutoPenSeconds;
        WritingFullscreenCheckBox.IsChecked = _settings.WritingFullscreen;
        ExternalOutputEnabledCheckBox.IsChecked = _settings.ExternalOutputEnabled;
        ExternalAutoPlacementCheckBox.IsChecked = _settings.ExternalAutoPlacement;
        ExternalFullscreenCheckBox.IsChecked = _settings.ExternalFullscreen;
        ShowConfirmButtonCheckBox.IsChecked = _settings.ShowConfirmButton;
        ShowClearButtonCheckBox.IsChecked = _settings.ShowClearButton;
        ShowEraserToolCheckBox.IsChecked = _settings.ShowEraserTool;
        UpdateExternalOutputOptionsEnabled();
        UpdateSizeLabels();
        RenderPalette();
    }

    private void UpdateExternalOutputOptionsEnabled()
    {
        var enabled = ExternalOutputEnabledCheckBox.IsChecked == true;
        ExternalAutoPlacementCheckBox.IsEnabled = enabled;
        ExternalFullscreenCheckBox.IsEnabled = enabled;
    }

    private void OnDisplaySettingsChanged(object sender, RoutedEventArgs e)
    {
        if (!_uiReady) return;

        _settings.WritingFullscreen = WritingFullscreenCheckBox.IsChecked == true;
        _settings.ExternalOutputEnabled = ExternalOutputEnabledCheckBox.IsChecked == true;
        _settings.ExternalAutoPlacement = ExternalAutoPlacementCheckBox.IsChecked == true;
        _settings.ExternalFullscreen = ExternalFullscreenCheckBox.IsChecked == true;
        UpdateExternalOutputOptionsEnabled();
        PersistSettings();
    }

    private void OnWritingToolSettingsChanged(object sender, RoutedEventArgs e)
    {
        if (!_uiReady) return;

        _settings.ShowConfirmButton = ShowConfirmButtonCheckBox.IsChecked == true;
        _settings.ShowClearButton = ShowClearButtonCheckBox.IsChecked == true;
        _settings.ShowEraserTool = ShowEraserToolCheckBox.IsChecked == true;
        PersistSettings();
    }

    private void RefreshServerUrlComboItems(string? currentText = null)
    {
        currentText ??= ServerUrlBox.Text;
        _suppressServerUrlEvents = true;
        try
        {
            ServerUrlBox.ItemsSource = null;
            ServerUrlBox.ItemsSource = _settings.SavedServerUrls.ToList();
            if (!string.IsNullOrWhiteSpace(currentText))
                ServerUrlBox.Text = currentText;
        }
        finally
        {
            _suppressServerUrlEvents = false;
        }

        UpdateRemoveSavedServerUrlButton();
    }

    private bool IsSavedServerUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return false;

        var normalized = ClientApiService.NormalizeServerUrl(url.Trim());
        return _settings.SavedServerUrls.Any(u =>
            string.Equals(u, normalized, StringComparison.OrdinalIgnoreCase));
    }

    private void RememberServerUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return;

        url = ClientApiService.NormalizeServerUrl(url.Trim());
        _settings.SavedServerUrls.RemoveAll(u => string.Equals(u, url, StringComparison.OrdinalIgnoreCase));
        _settings.SavedServerUrls.Insert(0, url);
        if (_settings.SavedServerUrls.Count > 10)
            _settings.SavedServerUrls.RemoveAt(_settings.SavedServerUrls.Count - 1);

        _settings.ServerUrl = url;
        RefreshServerUrlComboItems(url);
    }

    private void UpdateRemoveSavedServerUrlButton()
    {
        if (!_uiReady)
            return;

        var canEdit = !IsServerConnected && !_connecting;
        RemoveSavedServerUrlButton.IsEnabled = canEdit && IsSavedServerUrl(ServerUrlBox.Text);
    }

    private void OnSavedServerUrlSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_uiReady || _suppressServerUrlEvents || e.AddedItems.FirstOrDefault() is not string url)
            return;

        if (string.Equals(url, ServerUrlBox.Text, StringComparison.OrdinalIgnoreCase))
            return;

        ServerUrlBox.Text = url;
        OnServerUrlChanged();
    }

    private void OnServerUrlSubmitted(ComboBox sender, ComboBoxTextSubmittedEventArgs args)
    {
        if (!_uiReady)
            return;

        args.Handled = true;
        OnServerUrlChanged();
    }

    private void OnServerUrlLosingFocus(UIElement sender, LosingFocusEventArgs args)
    {
        if (!_uiReady)
            return;

        OnServerUrlChanged();
    }

    private void OnRemoveSavedServerUrlClick(object sender, RoutedEventArgs e)
    {
        if (!_uiReady || IsServerConnected || _connecting)
            return;

        var url = ClientApiService.NormalizeServerUrl(ServerUrlBox.Text.Trim());
        if (string.IsNullOrWhiteSpace(url))
            return;

        if (_settings.SavedServerUrls.RemoveAll(u =>
                string.Equals(u, url, StringComparison.OrdinalIgnoreCase)) == 0)
        {
            return;
        }

        PersistSettings();
        RefreshServerUrlComboItems(ServerUrlBox.Text);
    }

    private void OnServerUrlChanged()
    {
        if (!_uiReady) return;

        var normalized = ClientApiService.NormalizeServerUrl(ServerUrlBox.Text.Trim());
        if (!string.IsNullOrWhiteSpace(normalized) && !string.Equals(normalized, ServerUrlBox.Text, StringComparison.Ordinal))
            ServerUrlBox.Text = normalized;

        if (IsServerConnected)
            _ = DisconnectServerAsync(showError: false);
        UpdateRemoveSavedServerUrlButton();
        _ = RefreshBgStatusAsync();
        _ = RefreshLogoStatusAsync();
    }

    private void OnSeatIdChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (!_uiReady || double.IsNaN(args.NewValue)) return;
        if (IsServerConnected)
            _ = DisconnectServerAsync(showError: false);
        _ = RefreshBgStatusAsync();
    }

    private void OnRefreshBgClick(object sender, RoutedEventArgs e) => _ = RefreshBgStatusAsync();

    private void OnRefreshLogoClick(object sender, RoutedEventArgs e) => _ = RefreshLogoStatusAsync();

    private async Task RefreshBgStatusAsync()
    {
        _bgCheckCts?.Cancel();
        _bgCheckCts?.Dispose();
        _bgCheckCts = new CancellationTokenSource();
        var token = _bgCheckCts.Token;

        var serverUrl = ServerUrlBox.Text.Trim();
        var seatId = (int)SeatIdBox.Value;

        SetBgChecking(true);
        BgPreviewBorder.Visibility = Visibility.Collapsed;
        BgPreviewImage.Source = null;

        if (string.IsNullOrWhiteSpace(serverUrl))
        {
            BgStatusText.Text = "サーバー URL を入力してください。";
            SetBgChecking(false);
            return;
        }

        BgStatusText.Text = $"ID {seatId} の背景を確認中...";

        try
        {
            var normalizedUrl = ClientApiService.NormalizeServerUrl(serverUrl);
            if (!string.Equals(normalizedUrl, serverUrl, StringComparison.Ordinal))
            {
                serverUrl = normalizedUrl;
                ServerUrlBox.Text = normalizedUrl;
            }

            var info = await ClientApiService.GetSeatBackgroundAsync(serverUrl, seatId, token);
            if (token.IsCancellationRequested) return;

            if (info is null)
            {
                await _images.WarmCacheFromServerAsync(serverUrl, token);
                BgStatusText.Text =
                    $"未検出: BG_ID{seatId} / BG_ID{seatId:D2}（assets/backgrounds に配置されているか確認してください）";
                SetBgChecking(false);
                return;
            }

            await _images.RefreshBackgroundsForSeatAsync(serverUrl, info.RelativeUrl, token);
            if (token.IsCancellationRequested) return;

            BgStatusText.Text = $"読み込み OK: {info.FileName}";

            if (BackgroundImageService.IsTiffFile(info.FileName))
            {
                BgStatusText.Text += "（TIFF はプレビュー非対応ですが、書き画面では表示を試みます）";
                SetBgChecking(false);
                return;
            }

            var absoluteUrl = BackgroundImageService.ToAbsoluteUrl(serverUrl, info.RelativeUrl);
            var thumb = await _images.CreateThumbnailAsync(absoluteUrl, token);
            if (token.IsCancellationRequested) return;

            if (thumb is null)
            {
                BgStatusText.Text = $"検出済み: {info.FileName}（画像の取得に失敗しました）";
                SetBgChecking(false);
                return;
            }

            BgPreviewImage.Source = thumb;
            BgPreviewBorder.Visibility = Visibility.Visible;
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception ex)
        {
            if (token.IsCancellationRequested) return;
            BgStatusText.Text = $"確認失敗: {ex.Message}";
        }
        finally
        {
            if (!token.IsCancellationRequested)
                SetBgChecking(false);
        }
    }

    private void SetBgChecking(bool checking)
    {
        RefreshBgButton.IsEnabled = !checking;
        BgCheckRing.Visibility = checking ? Visibility.Visible : Visibility.Collapsed;
        BgCheckRing.IsActive = checking;
    }

    private async Task RefreshLogoStatusAsync()
    {
        _logoCheckCts?.Cancel();
        _logoCheckCts?.Dispose();
        _logoCheckCts = new CancellationTokenSource();
        var token = _logoCheckCts.Token;

        var serverUrl = ServerUrlBox.Text.Trim();

        SetLogoChecking(true);
        LogoPreviewBorder.Visibility = Visibility.Collapsed;
        LogoPreviewImage.Source = null;

        if (string.IsNullOrWhiteSpace(serverUrl))
        {
            LogoStatusText.Text = "サーバー URL を入力してください。";
            SetLogoChecking(false);
            return;
        }

        LogoStatusText.Text = "ロゴを確認中...";

        try
        {
            var normalizedUrl = ClientApiService.NormalizeServerUrl(serverUrl);
            if (!string.Equals(normalizedUrl, serverUrl, StringComparison.Ordinal))
            {
                serverUrl = normalizedUrl;
                ServerUrlBox.Text = normalizedUrl;
            }

            var logoUrl = await ClientApiService.GetLogoAsync(serverUrl, token);
            if (token.IsCancellationRequested) return;

            if (string.IsNullOrWhiteSpace(logoUrl))
            {
                AppState.LogoImageUrl = null;
                LogoStatusText.Text = "未検出: assets/logo に PNG などを配置してください（なしの場合 cover は暗幕のみ）";
                SetLogoChecking(false);
                return;
            }

            var warm = await _images.WarmLogoFromServerAsync(serverUrl, token);
            if (token.IsCancellationRequested) return;

            AppState.LogoImageUrl = warm.RelativeUrl;
            var fileName = warm.FileName ?? Path.GetFileName(logoUrl.Trim('/').Split('/').LastOrDefault() ?? "logo");
            LogoStatusText.Text = $"読み込み OK: {fileName}";

            var absoluteUrl = BackgroundImageService.ToAbsoluteUrl(serverUrl, logoUrl);
            var thumb = await _images.CreateThumbnailAsync(absoluteUrl, token);
            if (token.IsCancellationRequested) return;

            if (thumb is null)
            {
                LogoStatusText.Text = $"検出済み: {fileName}（画像の取得に失敗しました）";
                SetLogoChecking(false);
                return;
            }

            LogoPreviewImage.Source = thumb;
            LogoPreviewBorder.Visibility = Visibility.Visible;
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception ex)
        {
            if (token.IsCancellationRequested) return;
            LogoStatusText.Text = $"確認失敗: {ex.Message}";
        }
        finally
        {
            if (!token.IsCancellationRequested)
                SetLogoChecking(false);
        }
    }

    private void SetLogoChecking(bool checking)
    {
        RefreshLogoButton.IsEnabled = !checking;
        LogoCheckRing.Visibility = checking ? Visibility.Visible : Visibility.Collapsed;
        LogoCheckRing.IsActive = checking;
    }

    private void RenderPalette() =>
        ColorPaletteUi.Render(
            PalettePanel,
            _settings.Palette,
            _settings.PenColor,
            _ => { },
            OnAddColorClick,
            OnRemoveColorClick,
            OnEditColorClick);

    private async void OnEditColorClick(int index)
    {
        if (index < 0 || index >= _settings.Palette.Count) return;

        var oldColor = _settings.Palette[index];
        var picked = await ColorPaletteUi.PickColorAsync(XamlRoot, oldColor, "色を変更");
        if (picked is null) return;

        _settings.Palette[index] = picked;
        if (string.Equals(_settings.PenColor, oldColor, StringComparison.OrdinalIgnoreCase))
            _settings.PenColor = picked;

        PersistSettings();
        RenderPalette();
    }

    private async void OnAddColorClick()
    {
        var picked = await ColorPaletteUi.PickColorAsync(XamlRoot, _settings.PenColor);
        if (picked is null) return;
        if (_settings.Palette.Contains(picked, StringComparer.OrdinalIgnoreCase))
        {
            _settings.PenColor = picked;
        }
        else
        {
            _settings.Palette.Add(picked);
            _settings.PenColor = picked;
        }
        PersistSettings();
        RenderPalette();
    }

    private void OnRemoveColorClick(int index)
    {
        if (_settings.Palette.Count <= 1 || index < 0 || index >= _settings.Palette.Count) return;

        var removed = _settings.Palette[index];
        _settings.Palette.RemoveAt(index);
        if (string.Equals(_settings.PenColor, removed, StringComparison.OrdinalIgnoreCase))
            _settings.PenColor = _settings.Palette[Math.Min(index, _settings.Palette.Count - 1)];

        PersistSettings();
        RenderPalette();
    }

    private void OnPenSizeChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (!_uiReady) return;
        _settings.PenSize = e.NewValue;
        UpdateSizeLabels();
        PersistSettings();
    }

    private void OnEraserAutoPenSecondsChanged(NumberBox sender, NumberBoxValueChangedEventArgs e)
    {
        if (!_uiReady) return;
        if (double.IsNaN(e.NewValue))
            return;

        _settings.EraserAutoPenSeconds = (int)Math.Clamp(e.NewValue, 1, 60);
        PersistSettings();
    }

    private void OnEraserSizeChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (!_uiReady) return;
        _settings.EraserSize = e.NewValue;
        UpdateSizeLabels();
        PersistSettings();
    }

    private void UpdateSizeLabels()
    {
        PenSizeLabel.Text = $"{(int)_settings.PenSize} px";
        EraserSizeLabel.Text = $"{(int)_settings.EraserSize} px";
    }

    private void PersistSettings()
    {
        var serverUrl = ClientApiService.NormalizeServerUrl(ServerUrlBox.Text.Trim());
        RememberServerUrl(serverUrl);
        _settings.SeatId = (int)SeatIdBox.Value;
        _settings.WritingFullscreen = WritingFullscreenCheckBox.IsChecked == true;
        _settings.ExternalOutputEnabled = ExternalOutputEnabledCheckBox.IsChecked == true;
        _settings.ExternalAutoPlacement = ExternalAutoPlacementCheckBox.IsChecked == true;
        _settings.ExternalFullscreen = ExternalFullscreenCheckBox.IsChecked == true;
        _settings.ShowConfirmButton = ShowConfirmButtonCheckBox.IsChecked == true;
        _settings.ShowClearButton = ShowClearButtonCheckBox.IsChecked == true;
        _settings.ShowEraserTool = ShowEraserToolCheckBox.IsChecked == true;
        _settings.EraserAutoPenSeconds = (int)Math.Clamp(EraserAutoPenSecondsBox.Value, 1, 60);
        ClientSettingsStore.Save(_settings);
    }

    private bool _onlineUpdateBusy;

    private async void OnOnlineUpdateClick(object sender, RoutedEventArgs e)
    {
        if (_onlineUpdateBusy)
            return;

        _onlineUpdateBusy = true;
        OnlineUpdateButton.IsEnabled = false;
        try
        {
            await OnlineUpdateUiHelper.RunAsync(
                XamlRoot,
                AppUpdateKind.Client,
                status => UpdateStatusText.Text = status);
        }
        finally
        {
            _onlineUpdateBusy = false;
            OnlineUpdateButton.IsEnabled = true;
        }
    }

    private async void OnServerToggleClick(object sender, RoutedEventArgs e)
    {
        if (_connecting || _launching)
            return;

        if (IsServerConnected)
            await DisconnectServerAsync(showError: true);
        else
            await ConnectServerAsync();
    }

    private async void OnLaunchWritingClick(object sender, RoutedEventArgs e) =>
        await LaunchWritingAsync();

    private async Task ConnectServerAsync()
    {
        if (_connecting || IsServerConnected)
            return;

        _connecting = true;
        ErrorBar.IsOpen = false;
        UpdateConnectionUi();

        var progress = new LaunchProgress();
        progress.BindDispatcher(DispatcherQueue);
        progress.Changed += () => UpdateLaunchOverlay(progress);
        ShowLaunchOverlay(progress);

        using var launchTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));

        try
        {
            var serverUrl = ClientApiService.NormalizeServerUrl(ServerUrlBox.Text);
            var seatId = (int)SeatIdBox.Value;

            progress.Report("サーバー到達確認中...");
            var logoWarm = await Task.Run(async () =>
            {
                await ClientConnectionErrors.EnsureServerReachableAsync(serverUrl, launchTimeout.Token);
                progress.Report("背景を先読み中...");
                await _images.WarmCacheFromServerAsync(serverUrl, launchTimeout.Token);
                await _images.WarmChoicesFromServerAsync(serverUrl, launchTimeout.Token);
                progress.Report("ロゴを先読み中...");
                return await _images.WarmLogoFromServerAsync(serverUrl, launchTimeout.Token);
            }, launchTimeout.Token);

            AppState.LogoImageUrl = logoWarm.RelativeUrl;

            progress.Report("サーバーに接続中...");

            ApplySettingsFromUi(serverUrl, seatId);
            RememberServerUrl(serverUrl);
            ClientSettingsStore.Save(_settings);
            AppState.ApplySettings(_settings);
            AppState.ApplyStartupPen();

            var hub = new ClientHubService();
            await UiThread.RunAsync(DispatcherQueue, () =>
                hub.ConnectAndRegisterAsync(serverUrl, seatId, null, progress, launchTimeout.Token));

            hub.DetachedFromDispose = true;
            AppServices.Hub = hub;

            var restoredBg = hub.GetRestoredBackgroundUrl();
            if (!string.IsNullOrWhiteSpace(restoredBg))
            {
                _settings.BgImageUrl = restoredBg;
                AppState.BgImageUrl = restoredBg;
                ClientSettingsStore.Save(_settings);
            }

            ConnectionStatusBar.Message = $"サーバー接続済み（席 {seatId}）。書き画面を起動できます。";
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Setup] Connect failed: {ex}");
            var message = ClientConnectionErrors.Describe(ex);
            var hubToDispose = AppServices.Hub;
            AppServices.Hub = null;
            await EnqueueAsync(() =>
            {
                ErrorBar.Message = message;
                ErrorBar.IsOpen = true;
                if (hubToDispose is not null)
                    _ = hubToDispose.DisposeAsync();
            });
        }
        finally
        {
            await EnqueueAsync(() =>
            {
                HideLaunchOverlay();
                _connecting = false;
                UpdateConnectionUi();
            });
        }
    }

    private async Task LaunchWritingAsync()
    {
        if (_launching || !IsServerConnected || AppServices.Hub is null)
            return;

        _launching = true;
        ErrorBar.IsOpen = false;
        UpdateConnectionUi();

        using var launchTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));

        try
        {
            var serverUrl = ClientApiService.NormalizeServerUrl(ServerUrlBox.Text);
            var seatId = (int)SeatIdBox.Value;
            ApplySettingsFromUi(serverUrl, seatId);
            RememberServerUrl(serverUrl);
            ClientSettingsStore.Save(_settings);
            AppState.ApplySettings(_settings);
            AppState.ApplyStartupPen();

            var hub = AppServices.Hub;
            hub.DetachedFromDispose = true;

            var restoredBg = hub.GetRestoredBackgroundUrl();
            if (!string.IsNullOrWhiteSpace(restoredBg))
            {
                _settings.BgImageUrl = restoredBg;
                AppState.BgImageUrl = restoredBg;
                ClientSettingsStore.Save(_settings);
            }

            var progress = new LaunchProgress();
            progress.BindDispatcher(DispatcherQueue);
            AppServices.LaunchProgress = progress;
            AppServices.BeginWritingPageLaunch();

            await EnqueueAsync(() =>
            {
                Frame?.Navigate(typeof(WritingPage), null, new SuppressNavigationTransitionInfo());
            });

            await AppServices.WaitForWritingPageReadyAsync(launchTimeout.Token);
            AppServices.LaunchProgress = null;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Setup] Launch writing failed: {ex}");
            AppServices.LaunchProgress = null;
            await EnqueueAsync(() =>
            {
                ErrorBar.Message = ClientConnectionErrors.Describe(ex);
                ErrorBar.IsOpen = true;
            });
        }
        finally
        {
            await EnqueueAsync(() =>
            {
                _launching = false;
                UpdateConnectionUi();
            });
        }
    }

    private async Task DisconnectServerAsync(bool showError)
    {
        if (AppServices.Hub is not { } hub)
        {
            UpdateConnectionUi();
            return;
        }

        try
        {
            hub.DetachedFromDispose = false;
            await hub.DisconnectAsync();
            await hub.DisposeAsync();
        }
        catch (Exception ex)
        {
            if (showError)
            {
                ErrorBar.Message = $"切断に失敗しました: {ex.Message}";
                ErrorBar.IsOpen = true;
            }
        }
        finally
        {
            AppServices.Hub = null;
            await EnqueueAsync(UpdateConnectionUi);
        }
    }

    private void ApplySettingsFromUi(string serverUrl, int seatId)
    {
        _settings.ServerUrl = serverUrl;
        _settings.SeatId = seatId;
        _settings.BgImageUrl = null;
        _settings.WritingFullscreen = WritingFullscreenCheckBox.IsChecked == true;
        _settings.ExternalOutputEnabled = ExternalOutputEnabledCheckBox.IsChecked == true;
        _settings.ExternalAutoPlacement = ExternalAutoPlacementCheckBox.IsChecked == true;
        _settings.ExternalFullscreen = ExternalFullscreenCheckBox.IsChecked == true;
        if (_settings.Palette.Count > 0)
            _settings.PenColor = _settings.Palette[0];
    }

    private Task EnqueueAsync(Action action)
    {
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!DispatcherQueue.TryEnqueue(() =>
        {
            try
            {
                action();
                tcs.TrySetResult();
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }
        }))
        {
            tcs.TrySetException(new InvalidOperationException("UI dispatcher is unavailable."));
        }

        return tcs.Task;
    }

    private void ShowLaunchOverlay(LaunchProgress progress)
    {
        UpdateLaunchOverlay(progress);
        LaunchProgressRing.IsActive = true;
        LaunchOverlay.Visibility = Visibility.Visible;
    }

    private void HideLaunchOverlay()
    {
        LaunchProgressRing.IsActive = false;
        LaunchOverlay.Visibility = Visibility.Collapsed;
    }

    private void UpdateLaunchOverlay(LaunchProgress progress) =>
        LaunchStatusText.Text = progress.Message;
}
