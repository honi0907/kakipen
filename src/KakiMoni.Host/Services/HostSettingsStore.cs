using System.Text.Json;

using KakiMoni.Core.Models;



namespace KakiMoni_Host.Services;



public static class HostSettingsStore

{

    private static readonly string Path = System.IO.Path.Combine(

        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),

        "KakiMoni",

        "host-settings.json");



    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };



    public static HostSettings Load() => ReadFromDisk();



    public static void Save(HostSettings settings)

    {

        settings.LockOverlayOpacityPercent = Math.Clamp(settings.LockOverlayOpacityPercent, 0, 100);

        settings.SeatNameOverlay ??= new SeatNameOverlayConfig();

        settings.SeatNameOverlay.Normalize();

        settings.ManualNetworkAddress = string.IsNullOrWhiteSpace(settings.ManualNetworkAddress)

            ? null

            : settings.ManualNetworkAddress.Trim();

        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);

        File.WriteAllText(Path, JsonSerializer.Serialize(settings, JsonOptions));

    }



    private static HostSettings ReadFromDisk()

    {

        try

        {

            if (File.Exists(Path))

            {

                var data = JsonSerializer.Deserialize<HostSettings>(File.ReadAllText(Path));

                if (data is not null)

                {

                    data.LockOverlayOpacityPercent = Math.Clamp(data.LockOverlayOpacityPercent, 0, 100);

                    data.SeatNameOverlay ??= new SeatNameOverlayConfig();

                    MigrateSeatNameOverlayDefaults(data.SeatNameOverlay.Base);

                    data.SeatNameOverlay.Normalize();

                    return data;

                }

            }

        }

        catch { }



        return new HostSettings();

    }



    /// <summary>初期リリースの左上デフォルトを、背景の名前プレート位置に合わせた下中央へ移行。</summary>

    private static void MigrateSeatNameOverlayDefaults(SeatNameOverlayStyle overlay)

    {

        if (overlay.Anchor == SeatNameOverlayAnchor.TopLeft

            && overlay.MarginX == 24

            && overlay.MarginY == 24)

        {

            overlay.Anchor = SeatNameOverlayAnchor.BottomCenter;

            overlay.MarginX = 0;

            overlay.MarginY = 64;

        }

    }

}


