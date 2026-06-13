using Microsoft.UI.Dispatching;

namespace KakiMoni_Client.Services;

public static class UiThread
{
    public static Task RunAsync(DispatcherQueue dispatcher, Func<Task> action)
    {
        if (dispatcher.HasThreadAccess)
            return action();

        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!dispatcher.TryEnqueue(() => RunEnqueued(action, tcs)))
            tcs.TrySetException(new InvalidOperationException("UI dispatcher is unavailable."));

        return tcs.Task;
    }

    public static Task RunAsync(DispatcherQueue dispatcher, Action action) =>
        RunAsync(dispatcher, () =>
        {
            action();
            return Task.CompletedTask;
        });

    private static async void RunEnqueued(Func<Task> action, TaskCompletionSource tcs)
    {
        try
        {
            await action();
            tcs.TrySetResult();
        }
        catch (Exception ex)
        {
            tcs.TrySetException(ex);
        }
    }
}
