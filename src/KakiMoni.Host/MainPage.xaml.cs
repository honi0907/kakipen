using System.Diagnostics;
using KakiMoni.Core.Paths;
using KakiMoni_Host.Services;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;

namespace KakiMoni_Host;

public sealed partial class MainPage : Page
{
    private readonly Stopwatch _startup = Stopwatch.StartNew();
    private readonly DispatcherTimer _seatPollTimer = new();
    private readonly Border[] _seatCells = new Border[10];
    private readonly TextBlock[] _seatLabels = new TextBlock[10];
    private bool _seatPollInFlight;

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
            var label = new TextBlock
            {
                Text = $"ID {seatId}",
                HorizontalAlignment = HorizontalAlignment.Center,
                FontSize = 12,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
            };
            _seatLabels[i] = label;

            var cell = new Border
            {
                Padding = new Thickness(8, 6, 8, 6),
                CornerRadius = new CornerRadius(6),
                Background = SeatBrush(false),
                Child = label
            };
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
        PortBox.IsEnabled = !running;
        SeatsPanel.Visibility = running ? Visibility.Visible : Visibility.Collapsed;

        if (!running)
        {
            StatusText.Text = "停止中";
            UrlText.Text = string.Empty;
            SeatsSummaryText.Text = string.Empty;
            SeatsUpdatedText.Text = string.Empty;
            _seatPollTimer.Stop();
            UpdateSeatCells(Array.Empty<SeatStatusEntry>());
            return;
        }

        var url = AppHostContext.Server.BaseUrl;
        StatusText.Text = "サーバー稼働中";
        UrlText.Text = $"子機 URL: {url}\nコンパネ: {url} (WinUI)";
        _seatPollTimer.Start();
        _ = RefreshSeatsAsync();
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
            _seatCells[i].Background = SeatBrush(connected);
            _seatLabels[i].Text = connected ? $"ID {seatId}\n接続" : $"ID {seatId}\n—";
        }
    }

    private static SolidColorBrush SeatBrush(bool connected) =>
        new(connected
            ? ColorHelper.FromArgb(255, 22, 101, 52)
            : ColorHelper.FromArgb(255, 55, 65, 81));

    private async void OnStartClick(object sender, RoutedEventArgs e)
    {
        StartButton.IsEnabled = false;
        StatusText.Text = "起動中...";
        try
        {
            var port = (int)PortBox.Value;
            await AppHostContext.Server.StartAsync(ContentRootResolver.Resolve(), port);
        }
        catch (Exception ex)
        {
            StatusText.Text = $"起動失敗: {ex.Message}";
            StartButton.IsEnabled = true;
        }
    }

    private async void OnStopClick(object sender, RoutedEventArgs e)
    {
        StopButton.IsEnabled = false;
        try
        {
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

    private void OnCompanelClick(object sender, RoutedEventArgs e)
    {
        CompanelWindowHelper.EnsureCompanelWindowSize(App.HostWindow);
        Frame?.Navigate(typeof(CompanelPage));
    }
}
