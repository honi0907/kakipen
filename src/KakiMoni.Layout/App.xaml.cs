using Microsoft.UI.Xaml;

namespace KakiMoni_Layout;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        var window = new MainWindow();
        MainWindowInstance = window;
        window.Activate();
    }

    public static MainWindow? MainWindowInstance { get; private set; }
}
