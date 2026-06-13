using System.Diagnostics;
using Microsoft.UI.Xaml;

namespace KakiMoni_Client;

public partial class App : Application
{
    private readonly Stopwatch _startup = Stopwatch.StartNew();
    private Window? _window;

    public static MainWindow? MainWindowInstance { get; private set; }

    public App()
    {
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        Debug.WriteLine($"[Startup] OnLaunched {_startup.ElapsedMilliseconds}ms");
        _window = new MainWindow();
        MainWindowInstance = (MainWindow)_window;
        _window.Activate();
    }
}
