using KakiMoni_Host.Controls;
using KakiMoni_Host.SaveViewer;
using KakiMoni_Host.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace KakiMoni_Host.Settings;

public sealed partial class HostSettingsWindow : Window
{
    private sealed class SettingsNavItem
    {
        public required string Id { get; init; }
        public required string Title { get; init; }
        public required UIElement Panel { get; init; }
    }

    private readonly HostSettings _settings;
    private readonly Func<Task>? _onLiveApplyAsync;
    private readonly Func<Task>? _onRefreshAssetsAsync;
    private readonly SemaphoreSlim _applyGate = new(1, 1);
    private readonly List<SettingsNavItem> _sections = [];

    private readonly CheckBox _standbyCheck;
    private readonly CheckBox _judgeColorCheck;
    private readonly CheckBox _seatNameCheck;
    private readonly Slider _lockOpacitySlider;
    private readonly TextBlock _lockOpacityLabel;
    private readonly SeatNameOverlaySettingsPanel _overlayPanel;
    private readonly Button _refreshAssetsButton;
    private readonly TextBlock _refreshAssetsStatus;

    public HostSettingsWindow(Func<Task>? onLiveApplyAsync, Func<Task>? onRefreshAssetsAsync = null)
    {
        _onLiveApplyAsync = onLiveApplyAsync;
        _onRefreshAssetsAsync = onRefreshAssetsAsync;
        _settings = HostSettingsStore.Load();

        InitializeComponent();
        AppWindow.Resize(new Windows.Graphics.SizeInt32(960, 640));
        HostDisplayWindowLayout.ConfigureNormalPresenter(AppWindow);
        HostDisplayWindowLayout.CenterOnPrimaryWorkArea(AppWindow, 960, 640);

        _standbyCheck = new CheckBox
        {
            Content = "スタンバイ時にロックも解除する",
            IsChecked = _settings.StandbyUnlockAll
        };
        _judgeColorCheck = new CheckBox
        {
            Content = "判定 GO 時に描画色を反転する",
            IsChecked = _settings.JudgeColorMode
        };
        _seatNameCheck = new CheckBox
        {
            Content = "テキストネームを読み込む",
            IsChecked = _settings.UseSeatNameFile
        };
        _lockOpacitySlider = new Slider
        {
            Minimum = 0,
            Maximum = 100,
            StepFrequency = 5,
            Value = _settings.LockOverlayOpacityPercent,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        _lockOpacityLabel = new TextBlock { FontWeight = Microsoft.UI.Text.FontWeights.SemiBold };
        _overlayPanel = new SeatNameOverlaySettingsPanel(_settings.SeatNameOverlay.Clone());
        _refreshAssetsButton = new Button
        {
            Content = "アセット再読み込み",
            HorizontalAlignment = HorizontalAlignment.Left,
            Style = RaisedToolbarButtonStyle
        };
        _refreshAssetsStatus = new TextBlock
        {
            Opacity = 0.85,
            TextWrapping = TextWrapping.WrapWholeWords
        };
        _refreshAssetsButton.Click += async (_, _) => await OnRefreshAssetsClickAsync();

        void UpdateLockOpacityLabel() =>
            _lockOpacityLabel.Text = $"子機ロック時の暗さ: {(int)_lockOpacitySlider.Value}%";
        _lockOpacitySlider.ValueChanged += (_, _) => UpdateLockOpacityLabel();
        UpdateLockOpacityLabel();

        BuildSections();
        WireLiveApply();

        NavList.ItemsSource = _sections;
        NavList.DisplayMemberPath = nameof(SettingsNavItem.Title);
        NavList.SelectedIndex = 0;
    }

    private void BuildSections()
    {
        _sections.Add(new SettingsNavItem
        {
            Id = "standby",
            Title = "スタンバイ",
            Panel = CreateStandbyPanel()
        });
        _sections.Add(new SettingsNavItem
        {
            Id = "judge",
            Title = "判定表示",
            Panel = CreateJudgePanel()
        });
        _sections.Add(new SettingsNavItem
        {
            Id = "seatname",
            Title = "テキストネーム",
            Panel = CreateSeatNamePanel()
        });
        _sections.Add(new SettingsNavItem
        {
            Id = "overlay",
            Title = "席名オーバーレイ",
            Panel = CreateOverlayPanel()
        });
        _sections.Add(new SettingsNavItem
        {
            Id = "lock",
            Title = "子機ロック",
            Panel = CreateLockPanel()
        });
        _sections.Add(new SettingsNavItem
        {
            Id = "assets",
            Title = "アセット",
            Panel = CreateAssetsPanel()
        });
        _sections.Add(new SettingsNavItem
        {
            Id = "saves",
            Title = "保存データ",
            Panel = CreateSavesPanel()
        });
    }

    private StackPanel CreateStandbyPanel()
    {
        var panel = new StackPanel { Spacing = 8, MaxWidth = 640 };
        panel.Children.Add(_standbyCheck);
        panel.Children.Add(Desc(
            "ON のとき、スタンバイを押すと全ロック解除も同時に実行します。"));
        return panel;
    }

    private StackPanel CreateJudgePanel()
    {
        var panel = new StackPanel { Spacing = 8, MaxWidth = 640 };
        panel.Children.Add(_judgeColorCheck);
        panel.Children.Add(Desc(
            "fill 型判定画像表示中、パレット色を反転（補色）して再描画します。子機・外部出力・コンパネプレビューに適用。"));
        return panel;
    }

    private StackPanel CreateSeatNamePanel()
    {
        var panel = new StackPanel { Spacing = 8, MaxWidth = 640 };
        panel.Children.Add(_seatNameCheck);
        panel.Children.Add(Desc(
            "ON のとき assets/seat-names.txt の1行目が席1…10行目が席10になります。OFF のときは ID 表示のみです。"));
        return panel;
    }

    private StackPanel CreateOverlayPanel()
    {
        var panel = new StackPanel { Spacing = 8 };
        panel.Children.Add(Desc(
            "キャンバス上に席名を表示。名前が空の席は表示しません。変更はすぐ反映・保存されます。"));
        _overlayPanel.DetailContent.HorizontalAlignment = HorizontalAlignment.Stretch;
        panel.Children.Add(_overlayPanel.DetailContent);
        return panel;
    }

    private StackPanel CreateLockPanel()
    {
        var panel = new StackPanel { Spacing = 8, MaxWidth = 640 };
        panel.Children.Add(_lockOpacityLabel);
        panel.Children.Add(_lockOpacitySlider);
        panel.Children.Add(Desc(
            "子機がロックされたとき、画面を暗くするオーバーレイの濃さです（0＝暗くしない、100＝最も暗い）。"));
        return panel;
    }

    private static Style RaisedToolbarButtonStyle =>
        (Style)Application.Current.Resources["RaisedToolbarButtonStyle"];

    private StackPanel CreateAssetsPanel()
    {
        var panel = new StackPanel { Spacing = 12, MaxWidth = 640 };
        panel.Children.Add(Desc(
            "背景・ロゴ・席名テキストなどの素材フォルダを開きます。"));
        var openFolderButton = new Button
        {
            Content = "アセットフォルダを開く",
            HorizontalAlignment = HorizontalAlignment.Left,
            Style = RaisedToolbarButtonStyle
        };
        openFolderButton.Click += (_, _) => HostAssetFolderLauncher.TryOpen(out _);
        panel.Children.Add(openFolderButton);

        panel.Children.Add(Desc(
            "背景・選択肢・オーバーレイ・席名テキストを再取得します。"));
        panel.Children.Add(_refreshAssetsButton);
        panel.Children.Add(_refreshAssetsStatus);
        UpdateRefreshAssetsButtonState();
        return panel;
    }

    private StackPanel CreateSavesPanel()
    {
        var panel = new StackPanel { Spacing = 12, MaxWidth = 640 };
        panel.Children.Add(Desc(
            "保存した書き・判定画像を別ウィンドウで一覧表示します。"));
        var openViewerButton = new Button
        {
            Content = "保存一覧を開く",
            HorizontalAlignment = HorizontalAlignment.Left,
            Style = RaisedToolbarButtonStyle
        };
        openViewerButton.Click += (_, _) => SaveViewerWindowHelper.ShowOrActivate();
        panel.Children.Add(openViewerButton);
        return panel;
    }

    private void UpdateRefreshAssetsButtonState()
    {
        _refreshAssetsButton.IsEnabled = _onRefreshAssetsAsync is not null;
        if (_onRefreshAssetsAsync is null)
            _refreshAssetsStatus.Text = "サーバー未起動のため再読み込みできません。";
    }

    private async Task OnRefreshAssetsClickAsync()
    {
        if (_onRefreshAssetsAsync is null)
        {
            _refreshAssetsStatus.Text = "サーバー未起動のため再読み込みできません。";
            return;
        }

        _refreshAssetsButton.IsEnabled = false;
        _refreshAssetsStatus.Text = "読み込み中…";
        try
        {
            await _onRefreshAssetsAsync();
            _refreshAssetsStatus.Text = "再読み込みが完了しました。";
        }
        catch (Exception ex)
        {
            _refreshAssetsStatus.Text = $"再読み込みに失敗しました: {ex.Message}";
        }
        finally
        {
            UpdateRefreshAssetsButtonState();
        }
    }

    private static TextBlock Desc(string text) => new()
    {
        Text = text,
        Opacity = 0.75,
        TextWrapping = TextWrapping.WrapWholeWords
    };

    private void WireLiveApply()
    {
        _standbyCheck.Checked += async (_, _) => await SaveAndApplyAsync();
        _standbyCheck.Unchecked += async (_, _) => await SaveAndApplyAsync();
        _judgeColorCheck.Checked += async (_, _) => await SaveAndApplyAsync();
        _judgeColorCheck.Unchecked += async (_, _) => await SaveAndApplyAsync();
        _seatNameCheck.Checked += async (_, _) => await SaveAndApplyAsync();
        _seatNameCheck.Unchecked += async (_, _) => await SaveAndApplyAsync();
        _lockOpacitySlider.ValueChanged += async (_, _) => await SaveAndApplyAsync();
        _overlayPanel.Changed += () => _ = SaveAndApplyAsync();
    }

    private void ReadSettingsFromUi()
    {
        _settings.StandbyUnlockAll = _standbyCheck.IsChecked == true;
        _settings.JudgeColorMode = _judgeColorCheck.IsChecked == true;
        _settings.UseSeatNameFile = _seatNameCheck.IsChecked == true;
        _settings.SeatNameOverlay = _overlayPanel.BuildConfig();
        _settings.LockOverlayOpacityPercent = (int)_lockOpacitySlider.Value;
    }

    private async Task SaveAndApplyAsync()
    {
        ReadSettingsFromUi();
        HostSettingsStore.Save(_settings);
        if (_onLiveApplyAsync is null)
            return;

        await _applyGate.WaitAsync();
        try
        {
            await _onLiveApplyAsync();
        }
        finally
        {
            _applyGate.Release();
        }
    }

    private void OnNavSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (NavList.SelectedItem is not SettingsNavItem item)
            return;

        DetailTitleText.Text = item.Title;
        DetailHost.Content = item.Panel;
        DetailScroll.ChangeView(0, 0, 1);
    }
}
