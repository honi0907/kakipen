using System.Diagnostics;
using KakiMoni_Host.SaveViewer;
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
        if (!App.LaunchSaveViewerOnly)
            return;

        e.Handled = true;
        try
        {
            var text = $"予期しないエラーが発生しました。\n\n{e.Exception.Message}";
            SaveViewerWindowHelper.ShowStandaloneErrorMessage(text);
        }
        catch
        {
            Debug.WriteLine(e.Exception);
        }
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        Debug.WriteLine($"[Startup] OnLaunched {_startup.ElapsedMilliseconds}ms");
        LaunchSaveViewerOnly = Environment.GetCommandLineArgs()
            .Any(arg => string.Equals(arg, "--save-viewer", StringComparison.OrdinalIgnoreCase));
        SaveViewerServerUrl = ParseServerUrlFromArgs(Environment.GetCommandLineArgs());

        if (LaunchSaveViewerOnly)
        {
            SaveViewerWindowHelper.LaunchStandalone();
            return;
        }

        var window = new MainWindow();
        HostWindow = window;
        _window = window;
        window.Activate();
    }

    public static MainWindow? HostWindow { get; private set; }

    public static bool LaunchSaveViewerOnly { get; private set; }

    public static string? SaveViewerServerUrl { get; private set; }

    private static string? ParseServerUrlFromArgs(string[] args)
    {
        for (var i = 0; i < args.Length; i++)
        {
            if (string.Equals(args[i], "--server", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                return args[i + 1];
        }

        return null;
    }
}
