using KakiMoni.Core.Updates;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace KakiMoni_Client.Services;

public static class OnlineUpdateUiHelper
{
    private static readonly OnlineUpdateCoordinator Coordinator = new();
    private static int _busy;

    public static async Task RunAsync(
        XamlRoot xamlRoot,
        AppUpdateKind kind,
        Action<string> setStatus,
        Func<Task>? beforeExitAsync = null)
    {
        if (Interlocked.CompareExchange(ref _busy, 1, 0) != 0)
        {
            setStatus("更新処理が既に実行中です。");
            return;
        }

        try
        {
            var progress = new Progress<OnlineUpdateProgress>(p => setStatus(p.Message));
            var result = await Coordinator.RunAsync(
                kind,
                message => ConfirmAsync(xamlRoot, message),
                progress,
                beforeExitAsync,
                () => Application.Current.Exit());

            setStatus(result.Message);
        }
        finally
        {
            Interlocked.Exchange(ref _busy, 0);
        }
    }

    private static async Task<bool> ConfirmAsync(XamlRoot xamlRoot, string message)
    {
        var dialog = new ContentDialog
        {
            Title = "オンライン更新",
            Content = message,
            PrimaryButtonText = "更新する",
            CloseButtonText = "キャンセル",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = xamlRoot,
        };

        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }
}
