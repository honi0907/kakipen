using System.Diagnostics;
using KakiMoni.Core.Paths;
using KakiMoni.Core.Updates;
using KakiMoni_Host.Services;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace KakiMoni_Host;

public sealed partial class MainPage : Page
{
    private readonly Stopwatch _startup = Stopwatch.StartNew();
    private readonly DispatcherTimer _seatPollTimer = new();
    private readonly Border[] _seatCells = new Border[10];
    private readonly TextBlock[] _seatIdLabels = new TextBlock[10];
    private readonly TextBlock[] _seatStatusLabels = new TextBlock[10];
    private bool _seatPollInFlight;
    private bool _displayToggleBusy;

    public MainPage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        _seatPollTimer.Interval = TimeSpan.FromSeconds(3);
        _seatPollTimer.Tick += OnSeatPollTick;
        BuildSeatGrid();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        StartupText.Text = $"[Startup] MainPage Loaded {_startup.ElapsedMilliseconds}ms";
        VersionText.Text = AppVersionDisplay.Label;
        Debug.WriteLine(StartupText.Text);
        AppHostContext.Server.StateChanged += OnServerStateChanged;
        RefreshUi();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        AppHostContext.Server.StateChanged -= OnServerStateChanged;
        _seatPollTimer.Stop();
    }

    private void OnServerStateChanged(object? sender, EventArgs e) =>
        DispatcherQueue.TryEnqueue(RefreshUi);

    private void BuildSeatGrid()
    {
        for (var i = 0; i < 10; i++)
        {
            var seatId = i + 1;

            var idLabel = new TextBlock
            {
                Text = $"ID {seatId}",
                HorizontalAlignment = HorizontalAlignment.Center,
                FontSize = 14,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
            };
            _seatIdLabels[i] = idLabel;

            var statusLabel = new TextBlock
            {
                Text = "—",
                HorizontalAlignment = HorizontalAlignment.Center,
                FontSize = 12
            };
            _seatStatusLabels[i] = statusLabel;

            var stack = new StackPanel
            {
                Spacing = 2,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            stack.Children.Add(idLabel);
            stack.Children.Add(statusLabel);

            var cell = new Border
            {
                MinHeight = 56,
                Padding = new Thickness(6, 10, 6, 10),
                CornerRadius = new CornerRadius(4),
                Child = stack
            };
            ApplySeatCellStyle(cell, idLabel, statusLabel, connected: false);

            _seatCells[i] = cell;
            Grid.SetRow(cell, i / 5);
            Grid.SetColumn(cell, i % 5);
            SeatsGrid.Children.Add(cell);
        }
    }

    private void RefreshUi()
    {
        var running = AppHostContext.Server.IsRunning;
        StartButton.IsEnabled = !running;
        StopButton.IsEnabled = running;
        CompanelButton.IsEnabled = running;
        DisplayToggleButton.IsEnabled = running && !_displayToggleBusy;
        PortBox.IsEnabled = !running;

        SeatsPanel.Visibility = running ? Visibility.Visible : Visibility.Collapsed;
        EmptyHintText.Visibility = running ? Visibility.Collapsed : Visibility.Visible;

        if (!running)
        {
            var stopMessage = AppHostContext.Server.LastStopMessage;
            SetStatusInfoBar(
                string.IsNullOrWhiteSpace(stopMessage) ? InfoBarSeverity.Informational : InfoBarSeverity.Warning,
                string.IsNullOrWhiteSpace(stopMessage) ? "停止中" : "サーバー停止",
                stopMessage);
            ChildUrlText.Text = string.Empty;
            CompanelUrlText.Text = string.Empty;
            UrlPanel.Visibility = Visibility.Collapsed;
            DisplayStatusText.Visibility = Visibility.Collapsed;
            SeatsSummaryText.Text = string.Empty;
            SeatsUpdatedText.Text = string.Empty;
            _seatPollTimer.Stop();
            UpdateSeatCells(Array.Empty<SeatStatusEntry>());
            UpdateDisplayToggleLabel();
            return;
        }

        var url = AppHostContext.Server.BaseUrl;
        SetStatusInfoBar(InfoBarSeverity.Success, "サーバー稼働中", null);
        UrlPanel.Visibility = Visibility.Visible;
        ChildUrlText.Text = url;
        CompanelUrlText.Text = $"{url} (WinUI)";

        var displayStatus = AppHostContext.DisplayOutput.StatusText;
        if (string.IsNullOrWhiteSpace(displayStatus))
        {
            DisplayStatusText.Visibility = Visibility.Collapsed;
        }
        else
        {
            DisplayStatusText.Visibility = Visibility.Visible;
            DisplayStatusText.Text = displayStatus;
        }

        UpdateDisplayToggleLabel();
        _seatPollTimer.Start();
        _ = RefreshSeatsAsync();
    }

    private void SetStatusInfoBar(InfoBarSeverity severity, string title, string? message)
    {
        StatusInfoBar.Severity = severity;
        StatusInfoBar.Title = title;
        StatusInfoBar.Message = message ?? string.Empty;
        StatusInfoBar.IsOpen = true;
    }

    private void UpdateDisplayToggleLabel()
    {
        DisplayToggleButton.Content = AppHostContext.DisplayOutput.IsOpen ? "外部出力 OFF" : "外部出力 ON";
    }

    private async void OnDisplayToggleClick(object sender, RoutedEventArgs e)
    {
        if (_displayToggleBusy || !AppHostContext.Server.IsRunning)
            return;

        _displayToggleBusy = true;
        DisplayToggleButton.IsEnabled = false;
        try
        {
            if (AppHostContext.DisplayOutput.IsOpen)
                await AppHostContext.DisplayOutput.CloseAsync();
            else
                await AppHostContext.DisplayOutput.TryOpenAsync(DispatcherQueue);
        }
        catch (Exception ex)
        {
            DisplayStatusText.Visibility = Visibility.Visible;
            DisplayStatusText.Text = $"外部出力エラー: {ex.Message}";
        }
        finally
        {
            _displayToggleBusy = false;
            RefreshUi();
        }
    }

    private void OnSeatPollTick(object? sender, object e) => _ = RefreshSeatsAsync();

    private async Task RefreshSeatsAsync()
    {
        if (!AppHostContext.Server.IsRunning || _seatPollInFlight)
            return;

        _seatPollInFlight = true;
        try
        {
            var seats = await HostApiService.GetSeatsStatusAsync(AppHostContext.Server.BaseUrl);
            var connected = seats.Count(s => s.Connected);
            var summary = $"接続 {connected} / 10　（3秒ごとに更新）";
            var updated = $"最終更新: {DateTime.Now:HH:mm:ss}";
            DispatcherQueue.TryEnqueue(() =>
            {
                SeatsSummaryText.Text = summary;
                SeatsUpdatedText.Text = updated;
                UpdateSeatCells(seats);
            });
        }
        catch (Exception ex)
        {
            DispatcherQueue.TryEnqueue(() =>
                SeatsSummaryText.Text = $"席状態の取得に失敗: {ex.Message}");
        }
        finally
        {
            _seatPollInFlight = false;
        }
    }

    private void UpdateSeatCells(IReadOnlyList<SeatStatusEntry> seats)
    {
        for (var i = 0; i < 10; i++)
        {
            var seatId = i + 1;
            var seat = seats.FirstOrDefault(s => s.SeatId == seatId);
            var connected = seat?.Connected == true;
            ApplySeatCellStyle(_seatCells[i], _seatIdLabels[i], _seatStatusLabels[i], connected);
            _seatStatusLabels[i].Text = connected ? "接続" : "—";
        }
    }

    private static void ApplySeatCellStyle(
        Border cell,
        TextBlock idLabel,
        TextBlock statusLabel,
        bool connected)
    {
        if (connected)
        {
            var success = ThemeBrush("SystemFillColorSuccessBrush");
            cell.Background = ConnectedSeatBackgroundBrush;
            cell.BorderBrush = success;
            cell.BorderThickness = new Thickness(2);
            cell.Opacity = 1;
            idLabel.Foreground = ConnectedSeatTextBrush;
            idLabel.FontWeight = Microsoft.UI.Text.FontWeights.Bold;
            statusLabel.Foreground = success;
            statusLabel.FontWeight = Microsoft.UI.Text.FontWeights.SemiBold;
            return;
        }

        cell.Background = ThemeBrush("ControlFillColorDefaultBrush");
        cell.BorderBrush = ThemeBrush("ControlStrokeColorDefaultBrush");
        cell.BorderThickness = new Thickness(1);
        cell.Opacity = 0.72;
        idLabel.Foreground = ThemeBrush("TextFillColorSecondaryBrush");
        idLabel.FontWeight = Microsoft.UI.Text.FontWeights.SemiBold;
        statusLabel.Foreground = ThemeBrush("TextFillColorSecondaryBrush");
        statusLabel.FontWeight = Microsoft.UI.Text.FontWeights.Normal;
    }

    private static readonly SolidColorBrush ConnectedSeatBackgroundBrush =
        new(ColorHelper.FromArgb(255, 187, 247, 208));

    private static readonly SolidColorBrush ConnectedSeatTextBrush =
        new(ColorHelper.FromArgb(255, 20, 83, 45));

    private static SolidColorBrush ThemeBrush(string key) =>
        Application.Current.Resources[key] as SolidColorBrush
        ?? new SolidColorBrush(ColorHelper.FromArgb(255, 128, 128, 128));

    private async void OnStartClick(object sender, RoutedEventArgs e)
    {
        StartButton.IsEnabled = false;
        SetStatusInfoBar(InfoBarSeverity.Informational, "起動中...", null);
        try
        {
            var port = (int)PortBox.Value;
            var useSeatNames = HostSettingsStore.Load().UseSeatNameFile;
            await AppHostContext.Server.StartAsync(ContentRootResolver.Resolve(), port, useSeatNames);
            RefreshUi();
        }
        catch (Exception ex)
        {
            SetStatusInfoBar(InfoBarSeverity.Error, "起動失敗", ex.Message);
            StartButton.IsEnabled = true;
        }
    }

    private async void OnStopClick(object sender, RoutedEventArgs e)
    {
        StopButton.IsEnabled = false;
        try
        {
            if (AppHostContext.DisplayOutput.IsOpen)
                await AppHostContext.DisplayOutput.CloseAsync();

            if (App.HostWindow is not null)
                await App.HostWindow.StopServerWithOverlayAsync();
            else
                await AppHostContext.Server.StopAsync();
        }
        finally
        {
            RefreshUi();
        }
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
                AppUpdateKind.Host,
                status => UpdateStatusText.Text = status,
                BeforeHostUpdateExitAsync);
        }
        finally
        {
            _onlineUpdateBusy = false;
            OnlineUpdateButton.IsEnabled = true;
        }
    }

    private async Task BeforeHostUpdateExitAsync()
    {
        if (AppHostContext.DisplayOutput.IsOpen)
            await AppHostContext.DisplayOutput.CloseAsync();

        if (AppHostContext.Server.IsRunning)
            await AppHostContext.Server.StopAsync();
    }

    private async void OnCompanelClick(object sender, RoutedEventArgs e)
    {
        CompanelWindowHelper.EnsureCompanelWindowSize(App.HostWindow);
        if (App.HostWindow is not null)
            await App.HostWindow.ShowBusyOverlayAsync("サーバー接続中...");
        Frame?.Navigate(typeof(CompanelPage));
    }
}
