namespace KakiMoni_Host.Services;

public sealed class HostAssetCatalogRefresher
{
    public async Task RefreshAsync(
        HostHubService hub,
        Func<Task> refreshChoiceListAsync,
        Func<Task> refreshChoiceThumbAsync)
    {
        await refreshChoiceListAsync();
        await refreshChoiceThumbAsync();

        if (!hub.IsConnected)
            return;

        await hub.HostSetUseSeatNameFileAsync(HostSettingsStore.Load().UseSeatNameFile);
    }
}
