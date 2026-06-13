using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace KakiMoni_Client.Controls;

public static class ColorPaletteUi
{
    public static void Render(
        Panel host,
        IReadOnlyList<string> palette,
        string selectedColor,
        Action<string> onSelected,
        Action? onAddColor = null,
        Action<int>? onRemoveColor = null,
        Action<int>? onEditColor = null,
        int maxColors = 8,
        int minColors = 1,
        bool highlightSelection = true)
    {
        host.Children.Clear();
        var canRemove = onRemoveColor is not null && palette.Count > minColors;

        for (var index = 0; index < palette.Count; index++)
        {
            var color = palette[index];
            var isSelected = highlightSelection
                && string.Equals(color, selectedColor, StringComparison.OrdinalIgnoreCase);
            var swatchIndex = index;
            var swatchColor = color;
            var swatch = new Border
            {
                Width = 36,
                Height = 36,
                CornerRadius = new CornerRadius(18),
                BorderThickness = new Thickness(2),
                BorderBrush = new SolidColorBrush(Color.FromArgb(255, 30, 41, 59)),
                Background = new SolidColorBrush(AppState.ParseColor(color)),
                Tag = swatchIndex
            };
            swatch.Tapped += (_, e) =>
            {
                if (onEditColor is not null)
                    onEditColor(swatchIndex);
                else
                    onSelected(swatchColor);
                e.Handled = true;
            };

            var ring = new Border
            {
                Width = isSelected ? 46 : 40,
                Height = isSelected ? 46 : 40,
                CornerRadius = new CornerRadius(isSelected ? 23 : 20),
                BorderThickness = new Thickness(isSelected ? 3 : 0),
                BorderBrush = new SolidColorBrush(Color.FromArgb(255, 34, 211, 238)),
                Background = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0)),
                Margin = new Thickness(0, 0, 4, 0),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Child = swatch
            };
            swatch.HorizontalAlignment = HorizontalAlignment.Center;
            swatch.VerticalAlignment = VerticalAlignment.Center;

            if (!canRemove)
            {
                host.Children.Add(ring);
                continue;
            }

            var remove = new Button
            {
                Content = "×",
                Width = 18,
                Height = 18,
                Padding = new Thickness(0),
                FontSize = 12,
                FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                CornerRadius = new CornerRadius(9),
                BorderThickness = new Thickness(0),
                Background = new SolidColorBrush(Color.FromArgb(220, 15, 23, 42)),
                Foreground = new SolidColorBrush(Color.FromArgb(255, 248, 250, 252)),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, -4, -4, 0),
                Tag = index
            };
            remove.Click += (_, _) => onRemoveColor!((int)remove.Tag);

            var item = new Grid { Width = isSelected ? 46 : 40, Height = isSelected ? 46 : 40, Margin = new Thickness(0, 0, 4, 0) };
            item.Children.Add(ring);
            item.Children.Add(remove);
            host.Children.Add(item);
        }

        if (onAddColor is not null && palette.Count < maxColors)
        {
            var add = new Button
            {
                Content = "＋",
                Width = 36,
                Height = 36,
                CornerRadius = new CornerRadius(18)
            };
            add.Click += (_, _) => onAddColor();
            host.Children.Add(add);
        }
    }

    public static async Task<string?> PickColorAsync(XamlRoot xamlRoot, string initialHex, string title = "色を選択")
    {
        var picker = new ColorPicker { Color = AppState.ParseColor(initialHex), IsColorChannelTextInputVisible = true };
        var dialog = new ContentDialog
        {
            Title = title,
            Content = picker,
            PrimaryButtonText = "OK",
            CloseButtonText = "キャンセル",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = xamlRoot
        };
        return await dialog.ShowAsync() == ContentDialogResult.Primary
            ? ToHex(picker.Color)
            : null;
    }

    private static string ToHex(Color color) =>
        $"#{color.R:x2}{color.G:x2}{color.B:x2}";
}
