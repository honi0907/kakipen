using KakiMoni_Client.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Graphics;

namespace KakiMoni_Client;

public sealed partial class MainWindow : Window
{
    private const int LauncherWidth = 900;
    private const int LauncherHeight = 700;

    public MainWindow()
    {
        InitializeComponent();
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        AppWindow.SetIcon("Assets/AppIcon.ico");
        Title = "KakiMoni 子機";
        AppTitleBar.Title = "KakiMoni 子機";
        ApplyLauncherWindowSize();
        RootFrame.Navigate(typeof(SetupPage));
    }

    private void ApplyLauncherWindowSize()
    {
        try
        {
            var appWindow = ClientWindowLayout.GetAppWindow(this);
            appWindow.Resize(new SizeInt32(LauncherWidth, LauncherHeight));
        }
        catch
        {
            // 起動時サイズ設定はベストエフォート
        }
    }

    public void ApplyWritingFullscreenChrome(bool fullscreen)
    {
        if (fullscreen)
        {
            AppTitleBar.Visibility = Visibility.Collapsed;
            ExtendsContentIntoTitleBar = false;
            SetTitleBar(null);
            Grid.SetRow(RootFrame, 0);
            Grid.SetRowSpan(RootFrame, 2);
            return;
        }

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        AppTitleBar.Visibility = Visibility.Visible;
        Grid.SetRow(RootFrame, 1);
        Grid.SetRowSpan(RootFrame, 1);
    }
}
