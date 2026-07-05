using KakiMoni.Core.Network;
using Microsoft.UI.Xaml.Controls;

namespace KakiMoni_Host.Services;

public sealed class HostNetworkAdapterItem
{
    public bool IsAuto { get; init; }
    public string? Address { get; init; }
    public string Label { get; init; } = string.Empty;
}

public static class HostNetworkUiHelper
{
    public const string AutoTag = "__auto__";

    public static List<HostNetworkAdapterItem> BuildAdapterItems()
    {
        var items = new List<HostNetworkAdapterItem>
        {
            new() { IsAuto = true, Label = "自動（最初に検出）" }
        };

        foreach (var entry in LanAddressResolver.ListCandidates())
        {
            items.Add(new HostNetworkAdapterItem
            {
                IsAuto = false,
                Address = entry.Address,
                Label = entry.DisplayLabel
            });
        }

        return items;
    }

    public static void BindAdapterCombo(ComboBox combo, HostSettings settings)
    {
        combo.Items.Clear();
        var items = BuildAdapterItems();
        var selectedIndex = 0;

        for (var i = 0; i < items.Count; i++)
        {
            combo.Items.Add(items[i]);
            if (settings.NetworkMode == HostNetworkMode.Manual
                && !items[i].IsAuto
                && string.Equals(items[i].Address, settings.ManualNetworkAddress, StringComparison.OrdinalIgnoreCase))
            {
                selectedIndex = i;
            }
        }

        combo.SelectedIndex = selectedIndex;
    }

    public static void ApplyComboSelectionToSettings(ComboBox combo, HostSettings settings)
    {
        if (combo.SelectedItem is not HostNetworkAdapterItem item || item.IsAuto)
        {
            settings.NetworkMode = HostNetworkMode.Auto;
            settings.ManualNetworkAddress = null;
            return;
        }

        settings.NetworkMode = HostNetworkMode.Manual;
        settings.ManualNetworkAddress = item.Address;
    }

    public static void BindPreferenceCombo(ComboBox combo, HostSettings settings)
    {
        combo.Items.Clear();
        combo.Items.Add(new ComboBoxItem { Content = "最初に検出", Tag = LanAddressPreference.FirstFound });
        combo.Items.Add(new ComboBoxItem { Content = "Wi-Fi 優先", Tag = LanAddressPreference.WiFiFirst });
        combo.Items.Add(new ComboBoxItem { Content = "有線 優先", Tag = LanAddressPreference.EthernetFirst });

        var index = settings.NetworkPreference switch
        {
            LanAddressPreference.WiFiFirst => 1,
            LanAddressPreference.EthernetFirst => 2,
            _ => 0
        };
        combo.SelectedIndex = index;
    }

    public static void ApplyPreferenceComboToSettings(ComboBox combo, HostSettings settings)
    {
        if (combo.SelectedItem is ComboBoxItem { Tag: LanAddressPreference pref })
            settings.NetworkPreference = pref;
    }

    public static void SetNetworkControlsEnabled(
        bool serverRunning,
        ComboBox adapterCombo,
        ComboBox? preferenceCombo)
    {
        adapterCombo.IsEnabled = !serverRunning;
        if (preferenceCombo is not null)
            preferenceCombo.IsEnabled = !serverRunning;
    }
}
