using System.Net.Http;
using System.Runtime.InteropServices;

namespace KakiMoni_Client.Services;

public static class ClientConnectionErrors
{
    public static async Task EnsureServerReachableAsync(string serverUrl, CancellationToken cancellationToken = default)
    {
        var baseUrl = ClientApiService.NormalizeServerUrl(serverUrl);
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(4) };
        using var response = await client.GetAsync($"{baseUrl}/api/backgrounds", cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }

    public static string Describe(Exception ex)
    {
        if (ContainsMessage(ex, "duplicate-seat"))
            return "同じ席 ID が既に接続されています。";

        if (ex is OperationCanceledException)
            return "接続がタイムアウトしました。親機が起動しているか確認してください。";

        if (ContainsMessage(ex, "サーバーに接続できません")
            || ContainsMessage(ex, "サーバーからの登録応答がありません"))
            return ex.Message;

        var root = Unwrap(ex);
        if (root is HttpRequestException or System.Net.Sockets.SocketException)
        {
            return "サーバーに接続できません。親機アプリを起動し、「サーバー開始」を押してから接続してください。";
        }

        if (root is COMException com)
        {
            if (com.HResult == unchecked((int)0x8001010E))
                return "接続処理で内部エラーが発生しました。子機を再起動してもう一度お試しください。";

            if (string.IsNullOrWhiteSpace(root.Message))
                return $"接続失敗 (0x{com.HResult:X8})。子機を再起動してもう一度お試しください。";
        }

        var detail = FirstNonEmptyMessage(ex);
        return string.IsNullOrWhiteSpace(detail)
            ? "接続失敗。親機が起動しているか、同じ席 ID が使われていないか確認してください。"
            : $"接続失敗: {detail}";
    }

    private static bool ContainsMessage(Exception ex, string text)
    {
        for (var current = ex; current is not null; current = current.InnerException)
        {
            if (current.Message.Contains(text, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static Exception Unwrap(Exception ex)
    {
        while (ex.InnerException is not null)
            ex = ex.InnerException;
        return ex;
    }

    private static string FirstNonEmptyMessage(Exception ex)
    {
        for (var current = ex; current is not null; current = current.InnerException)
        {
            if (!string.IsNullOrWhiteSpace(current.Message))
                return current.Message.Trim();
        }

        return string.Empty;
    }
}
