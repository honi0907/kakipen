using System.Diagnostics;
using KakiMoni_Host.Services;
using Microsoft.UI.Xaml;

namespace KakiMoni_Host;

public partial class App : Application
{
    private readonly Stopwatch _startup = Stopwatch.StartNew();
    private Window? _window;

    public App()
    {
        InitializeComponent();
        UnhandledException += OnUnhandledException;
    }

    private void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        Debug.WriteLine($"[Unhandled] {e.Exception}");
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        Debug.WriteLine($"[Startup] OnLaunched {_startup.ElapsedMilliseconds}ms");
        var window = new MainWindow();
        HostWindow = window;
        _window = window;
        window.Activate();
    }

    public static MainWindow? HostWindow { get; private set; }
}
