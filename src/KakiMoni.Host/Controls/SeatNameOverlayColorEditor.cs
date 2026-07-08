using KakiMoni.Core.Display;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace KakiMoni_Host.Controls;

internal sealed class SeatNameOverlayColorEditor
{
    private readonly bool _includeAlpha;
    private readonly Border _swatch;
    private readonly TextBox _rBox;
    private readonly TextBox _gBox;
    private readonly TextBox _bBox;
    private readonly TextBox? _aBox;
    private readonly ColorPicker _picker;
    private readonly Flyout _flyout;
    private readonly Border _root;
    private bool _syncing;

    public Border Root => _root;

    public event EventHandler? Changed;

    public SeatNameOverlayColorEditor(string initialHex, bool includeAlpha, string label)
    {
        _includeAlpha = includeAlpha;
        var (a, r, g, b) = SeatNameOverlayColor.ParseArgb(initialHex, includeAlpha ? (byte)128 : (byte)255);

        _swatch = new Border
        {
            Width = 44,
            Height = 32,
            CornerRadius = new CornerRadius(4),
            BorderThickness = new Thickness(1),
            BorderBrush = new SolidColorBrush(Color.FromArgb(255, 148, 163, 184)),
            Background = CreateBrush(a, r, g, b)
        };

        _rBox = CreateChannelBox("R", r);
        _gBox = CreateChannelBox("G", g);
        _bBox = CreateChannelBox("B", b);
        _aBox = includeAlpha ? CreateChannelBox("A", a) : null;

        _picker = new ColorPicker
        {
            IsColorChannelTextInputVisible = true,
            IsAlphaEnabled = includeAlpha,
            IsColorSpectrumVisible = true,
            IsHexInputVisible = false,
            Color = Color.FromArgb(a, r, g, b)
        };
        _picker.ColorChanged += OnPickerColorChanged;

        _flyout = new Flyout
        {
            Placement = FlyoutPlacementMode.Bottom,
            Content = _picker
        };

        _swatch.Tapped += OnSwatchTapped;
        _swatch.IsHitTestVisible = true;

        var channelGrid = new Grid { ColumnSpacing = 6 };
        var columns = includeAlpha ? 4 : 3;
        for (var i = 0; i < columns; i++)
            channelGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var col = 0;
        if (_aBox is not null)
            AddChannelColumn(channelGrid, col++, _aBox);
        AddChannelColumn(channelGrid, col++, _rBox);
        AddChannelColumn(channelGrid, col++, _gBox);
        AddChannelColumn(channelGrid, col, _bBox);

        var row = new Grid
        {
            ColumnSpacing = 8,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        Grid.SetColumn(_swatch, 0);
        Grid.SetColumn(channelGrid, 1);
        row.Children.Add(_swatch);
        row.Children.Add(channelGrid);

        var panel = new StackPanel { Spacing = 4, HorizontalAlignment = HorizontalAlignment.Stretch };
        panel.Children.Add(new TextBlock
        {
            Text = label,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
        });
        panel.Children.Add(row);
        _root = new Border
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Child = panel
        };
    }

    public string GetHex()
    {
        var a = ReadChannel(_aBox, 255);
        var r = ReadChannel(_rBox, 0);
        var g = ReadChannel(_gBox, 0);
        var b = ReadChannel(_bBox, 0);
        return SeatNameOverlayColor.ToHex(a, r, g, b, _includeAlpha);
    }

    public void SetHex(string hex)
    {
        var (a, r, g, b) = SeatNameOverlayColor.ParseArgb(hex, _includeAlpha ? (byte)128 : (byte)255);
        _syncing = true;
        try
        {
            _swatch.Background = CreateBrush(a, r, g, b);
            SetChannelText(_aBox, a);
            SetChannelText(_rBox, r);
            SetChannelText(_gBox, g);
            SetChannelText(_bBox, b);
            _picker.Color = Color.FromArgb(a, r, g, b);
        }
        finally
        {
            _syncing = false;
        }
    }

    public void SetEnabled(bool enabled)
    {
        if (_aBox is not null)
            _aBox.IsEnabled = enabled;
        _rBox.IsEnabled = enabled;
        _gBox.IsEnabled = enabled;
        _bBox.IsEnabled = enabled;
        _swatch.IsHitTestVisible = enabled;
        _swatch.Opacity = enabled ? 1.0 : 0.45;
    }

    private void OnSwatchTapped(object sender, TappedRoutedEventArgs e)
    {
        if (!_swatch.IsHitTestVisible)
            return;

        _syncing = true;
        try
        {
            _picker.Color = ReadColor();
        }
        finally
        {
            _syncing = false;
        }

        _flyout.ShowAt(_swatch);
    }

    private void OnPickerColorChanged(ColorPicker sender, ColorChangedEventArgs args)
    {
        if (_syncing)
            return;

        ApplyColor(args.NewColor.A, args.NewColor.R, args.NewColor.G, args.NewColor.B, notify: true);
    }

    private void OnChannelTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_syncing)
            return;

        var a = ReadChannel(_aBox, 255);
        var r = ReadChannel(_rBox, 0);
        var g = ReadChannel(_gBox, 0);
        var b = ReadChannel(_bBox, 0);
        ApplyColor(a, r, g, b, notify: true);
    }

    private void ApplyColor(byte a, byte r, byte g, byte b, bool notify)
    {
        _syncing = true;
        try
        {
            _swatch.Background = CreateBrush(a, r, g, b);
            SetChannelText(_aBox, a);
            SetChannelText(_rBox, r);
            SetChannelText(_gBox, g);
            SetChannelText(_bBox, b);
            _picker.Color = Color.FromArgb(a, r, g, b);
        }
        finally
        {
            _syncing = false;
        }

        if (notify)
            Changed?.Invoke(this, EventArgs.Empty);
    }

    private Color ReadColor()
    {
        var a = ReadChannel(_aBox, 255);
        var r = ReadChannel(_rBox, 0);
        var g = ReadChannel(_gBox, 0);
        var b = ReadChannel(_bBox, 0);
        return Color.FromArgb(a, r, g, b);
    }

    private static byte ReadChannel(TextBox? box, byte fallback) =>
        box is null ? fallback : ParseChannel(box.Text, fallback);

    private static byte ParseChannel(string? text, byte fallback)
    {
        if (!int.TryParse(text?.Trim(), out var value))
            return fallback;

        return (byte)Math.Clamp(value, 0, 255);
    }

    private static void SetChannelText(TextBox? box, byte value)
    {
        if (box is null)
            return;

        var text = value.ToString();
        if (box.Text != text)
            box.Text = text;
    }

    private TextBox CreateChannelBox(string label, byte value)
    {
        var caption = new TextBlock
        {
            Text = label,
            FontSize = 12,
            Opacity = 0.8,
            HorizontalAlignment = HorizontalAlignment.Center
        };

        var box = new TextBox
        {
            Text = value.ToString(),
            MaxLength = 3,
            MinWidth = 48,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalTextAlignment = TextAlignment.Center,
            Padding = new Thickness(4, 4, 4, 4)
        };
        box.TextChanged += OnChannelTextChanged;

        var column = new StackPanel { Spacing = 2, HorizontalAlignment = HorizontalAlignment.Stretch };
        column.Children.Add(caption);
        column.Children.Add(box);
        return box;
    }

    private static void AddChannelColumn(Grid grid, int column, TextBox box)
    {
        if (box.Parent is StackPanel columnPanel)
        {
            Grid.SetColumn(columnPanel, column);
            grid.Children.Add(columnPanel);
        }
    }

    private static SolidColorBrush CreateBrush(byte a, byte r, byte g, byte b) =>
        new(Color.FromArgb(a, r, g, b));
}
