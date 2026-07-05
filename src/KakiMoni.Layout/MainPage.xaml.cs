using KakiMoni.Core.Models;
using KakiMoni.Core.Updates;
using KakiMoni_Layout.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace KakiMoni_Layout;

public sealed partial class MainPage : Page
{
    private readonly record struct SlotRowTheme(Color Accent, Color OpenBackground, Color OpenButton);

    private static readonly SlotRowTheme[] SlotThemes =
    [
        new(Color.FromArgb(255, 217, 119, 6), Color.FromArgb(48, 251, 191, 36), Color.FromArgb(255, 180, 83, 9)),
        new(Color.FromArgb(255, 124, 58, 237), Color.FromArgb(48, 167, 139, 250), Color.FromArgb(255, 109, 40, 217)),
        new(Color.FromArgb(255, 8, 145, 178), Color.FromArgb(48, 103, 232, 249), Color.FromArgb(255, 14, 116, 144)),
    ];

    private readonly LayoutLauncherSettings _settings = LayoutLauncherSettingsStore.Load();
    private readonly ComboBox[] _monitorBoxes;
    private readonly Button[] _slotButtons;
    private readonly Border[] _slotRows;
    private readonly TextBlock[] _slotLabels;
    private readonly bool[] _slotOpen = [false, false, false];
    private CancellationTokenSource? _autoConnectCts;

    private const int AutoConnectMaxAttempts = 10;
    private static readonly TimeSpan AutoConnectInterval = TimeSpan.FromSeconds(3);

    public MainPage()
    {
        InitializeComponent();
        _monitorBoxes = [Monitor0Box, Monitor1Box, Monitor2Box];
        _slotButtons = [Slot0Button, Slot1Button, Slot2Button];
        _slotRows = [Slot0Row, Slot1Row, Slot2Row];
        _slotLabels = [Slot0Label, Slot1Label, Slot2Label];
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        AppLayoutContext.Hub.ConnectionChanged += OnHubConnectionChanged;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e) => CancelAutoConnect();

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        VersionText.Text = AppVersionDisplay.Label;
        ServerUrlBox.Text = _settings.ServerUrl;
        PopulateMonitors();
        SelectMonitor(Monitor0Box, _settings.Display0Monitor);
        SelectMonitor(Monitor1Box, _settings.Display1Monitor);
        SelectMonitor(Monitor2Box, _settings.Display2Monitor);
        UpdateConnectionUi(AppLayoutContext.Hub.IsConnected, false);
        LoadLocalSlotLayouts();
        RefreshAllSlotVisuals();

        if (!AppLayoutContext.Hub.IsConnected)
            _ = StartAutoConnectAsync();
    }

    private void LoadLocalSlotLayouts()
    {
        foreach (var slot in LayoutDisplaySlots.All)
        {
            var layout = LayoutDisplayLayoutStore.LoadForSlot(slot);
            AppLayoutContext.SetSlotLayout(slot, layout);
        }
    }

    private void PopulateMonitors()
    {
        var count = Math.Max(1, MonitorHelper.MonitorCount);
        foreach (var box in _monitorBoxes)
        {
            box.Items.Clear();
            for (var i = 0; i < count; i++)
                box.Items.Add(new ComboBoxItem { Content = MonitorHelper.FormatMonitorLabel(i), Tag = i });
            box.SelectedIndex = Math.Min(box.SelectedIndex >= 0 ? box.SelectedIndex : 0, count - 1);
        }
    }

    private static void SelectMonitor(ComboBox box, int index)
    {
        for (var i = 0; i < box.Items.Count; i++)
        {
            if (box.Items[i] is ComboBoxItem item && item.Tag is int tag && tag == index)
            {
                box.SelectedIndex = i;
                return;
            }
        }

        if (box.Items.Count > 0)
            box.SelectedIndex = Math.Clamp(index, 0, box.Items.Count - 1);
    }

    private static int GetSelectedMonitorIndex(ComboBox box) =>
        box.SelectedItem is ComboBoxItem item && item.Tag is int index ? index : 0;

    private void OnServerUrlLostFocus(object sender, RoutedEventArgs e)
    {
        var url = ServerUrlBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(url))
            return;
        _settings.ServerUrl = url;
        LayoutLauncherSettingsStore.Save(_settings);
    }

    private async void OnConnectClick(object sender, RoutedEventArgs e)
    {
        CancelAutoConnect();
        ErrorBar.IsOpen = false;
        var url = ServerUrlBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(url))
        {
            ShowError("サーバー URL を入力してください。");
            return;
        }

        ConnectButton.IsEnabled = false;
        ConnectOverlay.Visibility = Visibility.Visible;
        try
        {
            await TryConnectCoreAsync(url);
        }
        catch (Exception ex)
        {
            ShowError($"接続に失敗しました: {ex.Message}");
        }
        finally
        {
            ConnectOverlay.Visibility = Visibility.Collapsed;
            ConnectButton.IsEnabled = true;
        }
    }

    private async Task StartAutoConnectAsync()
    {
        if (AppLayoutContext.Hub.IsConnected)
            return;

        var url = ServerUrlBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(url))
            return;

        CancelAutoConnect();
        _autoConnectCts = new CancellationTokenSource();
        var token = _autoConnectCts.Token;

        try
        {
            for (var attempt = 1; attempt <= AutoConnectMaxAttempts; attempt++)
            {
                if (token.IsCancellationRequested || AppLayoutContext.Hub.IsConnected)
                    return;

                UpdateAutoConnectStatus(attempt);

                try
                {
                    if (await TryConnectCoreAsync(url, token))
                        return;
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch
                {
                    // 次の試行へ
                }

                if (attempt >= AutoConnectMaxAttempts || token.IsCancellationRequested)
                    break;

                await Task.Delay(AutoConnectInterval, token);
            }

            if (!token.IsCancellationRequested && !AppLayoutContext.Hub.IsConnected)
            {
                StatusInfoBar.Severity = InfoBarSeverity.Warning;
                StatusInfoBar.Title = "自動接続できませんでした";
                StatusInfoBar.Message = "「接続」を押して手動で接続してください。";
            }
        }
        catch (OperationCanceledException)
        {
            // 手動接続などでキャンセル
        }
    }

    private void CancelAutoConnect()
    {
        if (_autoConnectCts is null)
            return;

        _autoConnectCts.Cancel();
        _autoConnectCts.Dispose();
        _autoConnectCts = null;
    }

    private void UpdateAutoConnectStatus(int attempt)
    {
        StatusInfoBar.Severity = InfoBarSeverity.Informational;
        StatusInfoBar.Title = "自動接続中";
        StatusInfoBar.Message = $"{attempt}/{AutoConnectMaxAttempts} 回目…（{AutoConnectInterval.TotalSeconds:0} 秒ごと）";
    }

    private async Task<bool> TryConnectCoreAsync(string url, CancellationToken cancellationToken = default)
    {
        _settings.ServerUrl = url;
        LayoutLauncherSettingsStore.Save(_settings);
        await AppLayoutContext.Hub.ConnectAsync(url, cancellationToken);
        AppLayoutContext.DisplayOutput.BindSeats(AppLayoutContext.Seats.All);
        return true;
    }

    private void ShowError(string message)
    {
        ErrorBar.Title = "エラー";
        ErrorBar.Message = message;
        ErrorBar.IsOpen = true;
    }

    private void OnHubConnectionChanged(bool connected, bool reconnecting)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            if (connected)
                CancelAutoConnect();
            UpdateConnectionUi(connected, reconnecting);
        });
    }

    private void UpdateConnectionUi(bool connected, bool reconnecting)
    {
        if (connected)
        {
            StatusInfoBar.Severity = InfoBarSeverity.Success;
            StatusInfoBar.Title = "接続中";
            StatusInfoBar.Message = ServerUrlBox.Text.Trim();
            ErrorBar.IsOpen = false;
            return;
        }

        if (reconnecting)
        {
            StatusInfoBar.Severity = InfoBarSeverity.Warning;
            StatusInfoBar.Title = "再接続中";
            StatusInfoBar.Message = "サーバーとの接続を再確立しています…";
            return;
        }

        StatusInfoBar.Severity = InfoBarSeverity.Informational;
        StatusInfoBar.Title = "未接続";
        StatusInfoBar.Message = "親機のサーバー URL を入力して接続してください。";
    }

    private async void OnSlot0Click(object sender, RoutedEventArgs e) => await ToggleSlotAsync(0);
    private async void OnSlot1Click(object sender, RoutedEventArgs e) => await ToggleSlotAsync(1);
    private async void OnSlot2Click(object sender, RoutedEventArgs e) => await ToggleSlotAsync(2);

    private async Task ToggleSlotAsync(int slotIndex)
    {
        if (!AppLayoutContext.Hub.IsConnected)
        {
            ShowError("先にサーバーへ接続してください。");
            return;
        }

        ErrorBar.IsOpen = false;
        SaveMonitorSettings();
        var monitorIndex = GetSelectedMonitorIndex(_monitorBoxes[slotIndex]);
        await AppLayoutContext.DisplayOutput.ToggleSlotAsync(slotIndex, monitorIndex, DispatcherQueue);
        _slotOpen[slotIndex] = AppLayoutContext.DisplayOutput.IsSlotOpen(slotIndex);
        UpdateSlotVisual(slotIndex);
    }

    private void RefreshAllSlotVisuals()
    {
        for (var i = 0; i < 3; i++)
        {
            _slotOpen[i] = AppLayoutContext.DisplayOutput.IsSlotOpen(i);
            UpdateSlotVisual(i);
        }
    }

    private void UpdateSlotVisual(int slotIndex)
    {
        if (slotIndex is < 0 or > 2)
            return;

        var isOpen = _slotOpen[slotIndex];
        var theme = SlotThemes[slotIndex];
        var row = _slotRows[slotIndex];
        var label = _slotLabels[slotIndex];
        var button = _slotButtons[slotIndex];

        if (isOpen)
        {
            row.BorderBrush = new SolidColorBrush(theme.Accent);
            row.Background = new SolidColorBrush(theme.OpenBackground);
            label.Foreground = new SolidColorBrush(theme.Accent);
            button.Content = "閉じる";
            button.Background = new SolidColorBrush(theme.OpenButton);
            button.Foreground = new SolidColorBrush(Microsoft.UI.Colors.White);
            button.BorderBrush = new SolidColorBrush(theme.OpenButton);
        }
        else
        {
            row.BorderBrush = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
            row.Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
            label.Foreground = new SolidColorBrush(theme.Accent);
            button.Content = "開く";
            button.ClearValue(Button.BackgroundProperty);
            button.ClearValue(Button.ForegroundProperty);
            button.ClearValue(Button.BorderBrushProperty);
        }
    }

    private void SaveMonitorSettings()
    {
        _settings.Display0Monitor = GetSelectedMonitorIndex(Monitor0Box);
        _settings.Display1Monitor = GetSelectedMonitorIndex(Monitor1Box);
        _settings.Display2Monitor = GetSelectedMonitorIndex(Monitor2Box);
        _settings.ServerUrl = ServerUrlBox.Text.Trim();
        LayoutLauncherSettingsStore.Save(_settings);
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
                AppUpdateKind.Layout,
                status => UpdateStatusText.Text = status,
                BeforeLayoutUpdateExitAsync);
        }
        finally
        {
            _onlineUpdateBusy = false;
            OnlineUpdateButton.IsEnabled = true;
        }
    }

    private async Task BeforeLayoutUpdateExitAsync()
    {
        for (var i = 0; i < 3; i++)
        {
            if (AppLayoutContext.DisplayOutput.IsSlotOpen(i))
                await AppLayoutContext.DisplayOutput.ToggleSlotAsync(i, 0, DispatcherQueue);
        }

        if (AppLayoutContext.Hub.IsConnected)
            await AppLayoutContext.Hub.DisconnectAsync();
    }

    private void OnOpenLayoutEditorClick(object sender, RoutedEventArgs e)
    {
        if (!AppLayoutContext.Hub.IsConnected)
        {
            ShowError("先にサーバーへ接続してください。");
            return;
        }

        ErrorBar.IsOpen = false;
        var slot = EditSlotBox.SelectedItem is ComboBoxItem item && item.Tag is string s
            ? s
            : LayoutDisplaySlots.Slot1;
        LayoutEditorWindowHelper.OpenOrActivate(slot);
    }
}
