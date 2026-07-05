using KakiMoni_Host.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace KakiMoni_Host.Controls;

public static class HostSettingsDialog
{
    public static async Task<bool> ShowAsync(XamlRoot xamlRoot)
    {
        var settings = HostSettingsStore.Load();

        var standbyCheck = new CheckBox
        {
            Content = "スタンバイ時にロックも解除する",
            IsChecked = settings.StandbyUnlockAll
        };
        var standbyDesc = new TextBlock
        {
            Text = "ON のとき、スタンバイを押すと全ロック解除も同時に実行します。",
            Opacity = 0.75,
            TextWrapping = TextWrapping.WrapWholeWords,
            Margin = new Thickness(0, 8, 0, 0)
        };

        var judgeColorCheck = new CheckBox
        {
            Content = "判定 GO 時に描画色を反転する",
            IsChecked = settings.JudgeColorMode,
            Margin = new Thickness(0, 16, 0, 0)
        };
        var judgeColorDesc = new TextBlock
        {
            Text = "fill 型判定画像表示中、パレット色を反転（補色）して再描画します。子機・外部出力・コンパネプレビューに適用。",
            Opacity = 0.75,
            TextWrapping = TextWrapping.WrapWholeWords,
            Margin = new Thickness(0, 8, 0, 0)
        };

        var lockOpacityLabel = new TextBlock
        {
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Margin = new Thickness(0, 16, 0, 0)
        };
        var lockOpacitySlider = new Slider
        {
            Minimum = 0,
            Maximum = 100,
            StepFrequency = 5,
            Value = settings.LockOverlayOpacityPercent
        };
        var lockOpacityDesc = new TextBlock
        {
            Text = "子機がロックされたとき、画面を暗くするオーバーレイの濃さです（0＝暗くしない、100＝最も暗い）。",
            Opacity = 0.75,
            TextWrapping = TextWrapping.WrapWholeWords,
            Margin = new Thickness(0, 8, 0, 0)
        };
        void UpdateLockOpacityLabel() =>
            lockOpacityLabel.Text = $"子機ロック時の暗さ: {(int)lockOpacitySlider.Value}%";
        lockOpacitySlider.ValueChanged += (_, _) => UpdateLockOpacityLabel();
        UpdateLockOpacityLabel();

        var seatNameCheck = new CheckBox
        {
            Content = "テキストネームを読み込む",
            IsChecked = settings.UseSeatNameFile,
            Margin = new Thickness(0, 16, 0, 0)
        };
        var seatNameDesc = new TextBlock
        {
            Text = "ON のとき assets/seat-names.txt の1行目が席1…10行目が席10になります。OFF のときは ID 表示のみです。",
            Opacity = 0.75,
            TextWrapping = TextWrapping.WrapWholeWords,
            Margin = new Thickness(0, 8, 0, 0)
        };

        var panel = new StackPanel { Spacing = 4 };
        panel.Children.Add(standbyCheck);
        panel.Children.Add(standbyDesc);
        panel.Children.Add(judgeColorCheck);
        panel.Children.Add(judgeColorDesc);
        panel.Children.Add(seatNameCheck);
        panel.Children.Add(seatNameDesc);
        panel.Children.Add(lockOpacityLabel);
        panel.Children.Add(lockOpacitySlider);
        panel.Children.Add(lockOpacityDesc);

        var dialog = new ContentDialog
        {
            Title = "⚙ システム設定",
            Content = panel,
            PrimaryButtonText = "保存",
            CloseButtonText = "キャンセル",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = xamlRoot
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
            return false;

        HostSettingsStore.Save(new HostSettings
        {
            StandbyUnlockAll = standbyCheck.IsChecked == true,
            JudgeColorMode = judgeColorCheck.IsChecked == true,
            UseSeatNameFile = seatNameCheck.IsChecked == true,
            LockOverlayOpacityPercent = (int)lockOpacitySlider.Value
        });
        return true;
    }
}
