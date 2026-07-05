using Microsoft.UI.Xaml;
using Windows.Graphics;

namespace KakiMoni_Layout;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        AppWindow.SetIcon("Assets/AppIcon.ico");
        Title = "KakiMoni レイアウト専用機";
        AppTitleBar.Title = "KakiMoni レイアウト専用機";
        try
        {
            AppWindow.Resize(new SizeInt32(1000, 620));
        }
        catch { }

        RootFrame.Navigate(typeof(MainPage));
    }
}
