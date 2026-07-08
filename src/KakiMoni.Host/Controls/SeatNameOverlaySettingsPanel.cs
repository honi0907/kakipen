using KakiMoni.Core.Display;

using KakiMoni.Core.Models;

using KakiMoni_Host.Services;

using Microsoft.UI.Xaml;

using Microsoft.UI.Xaml.Controls;

using Microsoft.UI.Xaml.Media;

using Windows.UI;



namespace KakiMoni_Host.Controls;



internal sealed class SeatNameOverlaySettingsPanel

{

    private const int MinSeatId = 1;

    private const int MaxSeatId = 10;



    private readonly CheckBox _enabledCheck;

    private readonly RadioButton _sharedColorsRadio;

    private readonly RadioButton _perSeatColorsRadio;

    private readonly ComboBox _seatCombo;

    private readonly Button _resetSeatColorsButton;

    private readonly StackPanel _perSeatPanel;

    private readonly ComboBox _fontCombo;

    private readonly Slider _fontSizeSlider;

    private readonly TextBlock _fontSizeLabel;

    private readonly ComboBox _anchorCombo;

    private readonly Slider _marginXSlider;

    private readonly Slider _marginYSlider;

    private readonly SeatNameOverlayColorEditor _textColorEditor;

    private readonly SeatNameOverlayColorEditor _textStrokeColorEditor;

    private readonly SeatNameOverlayColorEditor _borderColorEditor;

    private readonly SeatNameOverlayColorEditor _backgroundColorEditor;

    private readonly Slider _borderThicknessSlider;

    private readonly Slider _textStrokeThicknessSlider;

    private readonly CheckBox _borderEnabledCheck;

    private readonly CheckBox _backgroundEnabledCheck;

    private readonly CheckBox _textStrokeEnabledCheck;

    private readonly TextBlock _marginXLabel;

    private readonly TextBlock _marginYLabel;

    private readonly TextBlock _borderThicknessLabel;

    private readonly TextBlock _textStrokeThicknessLabel;

    private readonly TextBlock _colorSectionLabel;

    private readonly Border _previewBorder;

    private readonly TextBlock _previewText;



    private SeatNameOverlayConfig _draft;

    private int _selectedSeatId = 1;

    private bool _loadingEditors;



    public Grid DetailContent { get; }



    public event Action? Changed;



    public SeatNameOverlaySettingsPanel(SeatNameOverlayConfig initial)

    {

        _draft = initial.Clone();



        _enabledCheck = new CheckBox

        {

            Content = "キャンバス上に席名を表示する",

            IsChecked = _draft.Base.Enabled,

            HorizontalAlignment = HorizontalAlignment.Stretch

        };



        _sharedColorsRadio = new RadioButton

        {

            Content = "全席共通の色",

            GroupName = "SeatNameOverlayColorMode",

            IsChecked = !_draft.UsePerSeatColors

        };

        _perSeatColorsRadio = new RadioButton

        {

            Content = "席ごとに色を変える",

            GroupName = "SeatNameOverlayColorMode",

            IsChecked = _draft.UsePerSeatColors

        };



        _seatCombo = CreateFullWidthComboBox();

        for (var seatId = MinSeatId; seatId <= MaxSeatId; seatId++)

            _seatCombo.Items.Add(HostSeatNameLabels.GetLabel(seatId));

        _seatCombo.SelectedIndex = 0;



        _resetSeatColorsButton = new Button

        {

            Content = "この席は共通色を使う",

            HorizontalAlignment = HorizontalAlignment.Left

        };



        _perSeatPanel = new StackPanel { Spacing = 6 };

        _perSeatPanel.Children.Add(_seatCombo);

        _perSeatPanel.Children.Add(_resetSeatColorsButton);



        _fontCombo = CreateFullWidthComboBox();

        foreach (var font in SeatNameOverlayStyle.FontPresets)

            _fontCombo.Items.Add(font);

        _fontCombo.SelectedItem = SeatNameOverlayStyle.FontPresets

            .FirstOrDefault(f => f.Equals(_draft.Base.FontFamily, StringComparison.OrdinalIgnoreCase))

            ?? SeatNameOverlayStyle.FontPresets[0];



        _fontSizeSlider = CreateFullWidthSlider(16, SeatNameOverlayStyle.MaxFontSize, 1, _draft.Base.FontSize);

        _fontSizeLabel = new TextBlock { VerticalAlignment = VerticalAlignment.Center };



        _anchorCombo = CreateFullWidthComboBox();

        _anchorCombo.Items.Add("左上");

        _anchorCombo.Items.Add("上中央");

        _anchorCombo.Items.Add("右上");

        _anchorCombo.Items.Add("左下");

        _anchorCombo.Items.Add("下中央");

        _anchorCombo.Items.Add("右下");

        _anchorCombo.SelectedIndex = (int)_draft.Base.Anchor;



        _marginXSlider = CreateFullWidthSlider(0, 120, 4, _draft.Base.MarginX);

        _marginYSlider = CreateFullWidthSlider(0, 120, 4, _draft.Base.MarginY);

        _marginXLabel = new TextBlock { VerticalAlignment = VerticalAlignment.Center };

        _marginYLabel = new TextBlock { VerticalAlignment = VerticalAlignment.Center };



        _textColorEditor = new SeatNameOverlayColorEditor(_draft.Base.TextColor, includeAlpha: false, "文字色");

        _textStrokeColorEditor = new SeatNameOverlayColorEditor(_draft.Base.TextStrokeColor, includeAlpha: false, "縁色");

        _borderColorEditor = new SeatNameOverlayColorEditor(_draft.Base.BorderColor, includeAlpha: false, "枠色");

        _backgroundColorEditor = new SeatNameOverlayColorEditor(_draft.Base.BackgroundColor, includeAlpha: true, "背景色");

        _borderThicknessSlider = CreateFullWidthSlider(0, 8, 1, _draft.Base.BorderThickness);

        _textStrokeThicknessSlider = CreateFullWidthSlider(0, SeatNameOverlayStyle.MaxTextStrokeThickness, 1, _draft.Base.TextStrokeThickness);

        _borderThicknessLabel = new TextBlock { VerticalAlignment = VerticalAlignment.Center };

        _textStrokeThicknessLabel = new TextBlock { VerticalAlignment = VerticalAlignment.Center };

        _colorSectionLabel = SectionLabel("色");

        _borderEnabledCheck = new CheckBox { Content = "枠を表示", IsChecked = _draft.Base.BorderEnabled };

        _backgroundEnabledCheck = new CheckBox { Content = "背景を表示", IsChecked = _draft.Base.BackgroundEnabled };

        _textStrokeEnabledCheck = new CheckBox { Content = "文字縁を表示", IsChecked = _draft.Base.TextStrokeEnabled };



        _previewText = new TextBlock { Text = "サンプル席名", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold };

        _previewBorder = new Border

        {

            Padding = new Thickness(8, 4, 8, 4),

            CornerRadius = new CornerRadius(4),

            Child = _previewText,

            HorizontalAlignment = HorizontalAlignment.Stretch,

            MinHeight = 72

        };



        _fontSizeSlider.ValueChanged += (_, _) => NotifyChanged();

        _marginXSlider.ValueChanged += (_, _) => NotifyChanged();

        _marginYSlider.ValueChanged += (_, _) => NotifyChanged();

        _enabledCheck.Checked += (_, _) => NotifyChanged();

        _enabledCheck.Unchecked += (_, _) => NotifyChanged();

        _fontCombo.SelectionChanged += (_, _) => NotifyChanged();

        _anchorCombo.SelectionChanged += (_, _) => NotifyChanged();

        _textColorEditor.Changed += (_, _) => NotifyChanged();

        _textStrokeColorEditor.Changed += (_, _) => NotifyChanged();

        _borderColorEditor.Changed += (_, _) => NotifyChanged();

        _backgroundColorEditor.Changed += (_, _) => NotifyChanged();

        _borderThicknessSlider.ValueChanged += (_, _) => NotifyChanged();

        _textStrokeThicknessSlider.ValueChanged += (_, _) => NotifyChanged();

        _borderEnabledCheck.Checked += (_, _) => NotifyChanged();

        _borderEnabledCheck.Unchecked += (_, _) => NotifyChanged();

        _backgroundEnabledCheck.Checked += (_, _) => NotifyChanged();

        _backgroundEnabledCheck.Unchecked += (_, _) => NotifyChanged();

        _textStrokeEnabledCheck.Checked += (_, _) => NotifyChanged();

        _textStrokeEnabledCheck.Unchecked += (_, _) => NotifyChanged();

        _sharedColorsRadio.Checked += (_, _) => OnColorModeChanged();

        _perSeatColorsRadio.Checked += (_, _) => OnColorModeChanged();

        _seatCombo.SelectionChanged += (_, _) => OnSeatSelectionChanged();

        _resetSeatColorsButton.Click += (_, _) => ResetSelectedSeatColors();



        var content = new Grid

        {

            HorizontalAlignment = HorizontalAlignment.Stretch,

            ColumnSpacing = 12,

            RowSpacing = 6

        };

        content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });



        var row = 0;

        AddFullWidthRow(content, row++, _enabledCheck);

        AddTwoColumnRow(content, row, _sharedColorsRadio, _perSeatColorsRadio);

        row++;

        AddFullWidthRow(content, row++, _perSeatPanel);

        AddFullWidthRow(content, row++, SectionLabel("フォント"));

        AddFullWidthRow(content, row++, _fontCombo);

        AddLabeledSliderRow(content, row++, _fontSizeLabel, _fontSizeSlider);

        AddFullWidthRow(content, row++, SectionLabel("位置"));

        AddFullWidthRow(content, row++, _anchorCombo);



        AddTwoColumnRow(content, row, _marginXLabel, _marginXSlider, _marginYLabel, _marginYSlider);

        row++;



        AddFullWidthRow(content, row++, _colorSectionLabel);

        var colorGrid = CreateColorGrid();

        AddFullWidthRow(content, row++, colorGrid);

        AddFullWidthRow(content, row++, _textStrokeEnabledCheck);

        AddLabeledSliderRow(content, row++, _textStrokeThicknessLabel, _textStrokeThicknessSlider);

        AddTwoColumnRow(content, row, _borderEnabledCheck, _backgroundEnabledCheck);

        row++;

        AddLabeledSliderRow(content, row++, _borderThicknessLabel, _borderThicknessSlider);

        AddFullWidthRow(content, row, _previewBorder);



        DetailContent = content;

        UpdateColorModeUi();

        LoadColorEditorsFromDraft();

        UpdatePreview();

    }



    public SeatNameOverlayConfig BuildConfig()

    {

        SaveColorEditorsToDraft();

        ApplyBaseFieldsToDraft();

        _draft.Normalize();

        return _draft.Clone();

    }



    private void OnColorModeChanged()

    {

        if (_loadingEditors)

            return;



        SaveColorEditorsToDraft();

        _draft.UsePerSeatColors = _perSeatColorsRadio.IsChecked == true;

        UpdateColorModeUi();

        LoadColorEditorsFromDraft();

        NotifyChanged();

    }



    private void OnSeatSelectionChanged()

    {

        if (_loadingEditors || _draft.UsePerSeatColors != true)

            return;



        SaveColorEditorsToDraft();

        _selectedSeatId = Math.Clamp(_seatCombo.SelectedIndex + 1, MinSeatId, MaxSeatId);

        LoadColorEditorsFromDraft();

        NotifyChanged();

    }



    private void ResetSelectedSeatColors()

    {

        SaveColorEditorsToDraft();

        _draft.PerSeatColors.Remove(_selectedSeatId);

        LoadColorEditorsFromDraft();

        NotifyChanged();

    }



    private void UpdateColorModeUi()

    {

        var perSeat = _draft.UsePerSeatColors;

        _perSeatPanel.Visibility = perSeat ? Visibility.Visible : Visibility.Collapsed;

        _resetSeatColorsButton.Visibility = perSeat ? Visibility.Visible : Visibility.Collapsed;

        _colorSectionLabel.Text = perSeat ? $"色（{_selectedSeatId}番席）" : "色（全席共通）";

    }



    private void SaveColorEditorsToDraft()

    {

        if (_loadingEditors)

            return;



        if (_draft.UsePerSeatColors)

        {

            _draft.PerSeatColors[_selectedSeatId] = new SeatNameOverlayColorOverride

            {

                TextColor = _textColorEditor.GetHex(),

                TextStrokeColor = _textStrokeColorEditor.GetHex(),

                BorderColor = _borderColorEditor.GetHex(),

                BackgroundColor = _backgroundColorEditor.GetHex()

            };

            return;

        }



        _draft.Base.TextColor = _textColorEditor.GetHex();

        _draft.Base.TextStrokeColor = _textStrokeColorEditor.GetHex();

        _draft.Base.BorderColor = _borderColorEditor.GetHex();

        _draft.Base.BackgroundColor = _backgroundColorEditor.GetHex();

    }



    private void LoadColorEditorsFromDraft()

    {

        _loadingEditors = true;

        try

        {

            var style = SeatNameOverlayResolver.Resolve(_draft, _selectedSeatId);

            _textColorEditor.SetHex(style.TextColor);

            _textStrokeColorEditor.SetHex(style.TextStrokeColor);

            _borderColorEditor.SetHex(style.BorderColor);

            _backgroundColorEditor.SetHex(style.BackgroundColor);

        }

        finally

        {

            _loadingEditors = false;

        }

    }



    private void ApplyBaseFieldsToDraft()

    {

        _draft.Base.Enabled = _enabledCheck.IsChecked == true;

        _draft.Base.FontFamily = _fontCombo.SelectedItem?.ToString() ?? SeatNameOverlayStyle.FontPresets[0];

        _draft.Base.FontSize = _fontSizeSlider.Value;

        _draft.Base.Anchor = (SeatNameOverlayAnchor)Math.Clamp(_anchorCombo.SelectedIndex, 0, 5);

        _draft.Base.MarginX = _marginXSlider.Value;

        _draft.Base.MarginY = _marginYSlider.Value;

        _draft.Base.TextStrokeThickness = _textStrokeThicknessSlider.Value;

        _draft.Base.TextStrokeEnabled = _textStrokeEnabledCheck.IsChecked == true;

        _draft.Base.BorderThickness = _borderThicknessSlider.Value;

        _draft.Base.BorderEnabled = _borderEnabledCheck.IsChecked == true;

        _draft.Base.BackgroundEnabled = _backgroundEnabledCheck.IsChecked == true;

        _draft.UsePerSeatColors = _perSeatColorsRadio.IsChecked == true;

    }



    private Grid CreateColorGrid()

    {

        var grid = new Grid

        {

            HorizontalAlignment = HorizontalAlignment.Stretch,

            ColumnSpacing = 8

        };

        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });



        AddColorColumn(grid, 0, _textColorEditor);

        AddColorColumn(grid, 1, _textStrokeColorEditor);

        AddColorColumn(grid, 2, _borderColorEditor);

        AddColorColumn(grid, 3, _backgroundColorEditor);

        return grid;

    }



    private static void AddColorColumn(Grid grid, int column, SeatNameOverlayColorEditor editor)

    {

        editor.Root.HorizontalAlignment = HorizontalAlignment.Stretch;

        Grid.SetColumn(editor.Root, column);

        grid.Children.Add(editor.Root);

    }



    private static TextBlock SectionLabel(string text) => new()

    {

        Text = text,

        FontWeight = Microsoft.UI.Text.FontWeights.SemiBold

    };



    private static ComboBox CreateFullWidthComboBox() => new()

    {

        HorizontalAlignment = HorizontalAlignment.Stretch,

        HorizontalContentAlignment = HorizontalAlignment.Stretch

    };



    private static Slider CreateFullWidthSlider(double min, double max, double step, double value) => new()

    {

        Minimum = min,

        Maximum = max,

        StepFrequency = step,

        Value = value,

        HorizontalAlignment = HorizontalAlignment.Stretch

    };



    private static void AddFullWidthRow(Grid grid, int row, FrameworkElement element)

    {

        Grid.SetRow(element, row);

        Grid.SetColumn(element, 0);

        Grid.SetColumnSpan(element, 2);

        element.HorizontalAlignment = HorizontalAlignment.Stretch;

        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        grid.Children.Add(element);

    }



    private static void AddLabeledSliderRow(Grid grid, int row, TextBlock label, Slider slider)

    {

        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });



        var host = new Grid { ColumnSpacing = 8 };

        host.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        host.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        Grid.SetColumn(label, 0);

        Grid.SetColumn(slider, 1);

        host.Children.Add(label);

        host.Children.Add(slider);



        AddFullWidthRow(grid, row, host);

    }



    private static void AddTwoColumnRow(Grid grid, int row, FrameworkElement left, FrameworkElement right)

    {

        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });



        Grid.SetRow(left, row);

        Grid.SetColumn(left, 0);

        left.HorizontalAlignment = HorizontalAlignment.Stretch;

        Grid.SetRow(right, row);

        Grid.SetColumn(right, 1);

        right.HorizontalAlignment = HorizontalAlignment.Stretch;

        grid.Children.Add(left);

        grid.Children.Add(right);

    }



    private static void AddTwoColumnRow(

        Grid grid,

        int row,

        TextBlock leftLabel,

        Slider leftSlider,

        TextBlock rightLabel,

        Slider rightSlider)

    {

        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });



        var left = new StackPanel { Spacing = 2, HorizontalAlignment = HorizontalAlignment.Stretch };

        left.Children.Add(leftLabel);

        left.Children.Add(leftSlider);



        var right = new StackPanel { Spacing = 2, HorizontalAlignment = HorizontalAlignment.Stretch };

        right.Children.Add(rightLabel);

        right.Children.Add(rightSlider);



        Grid.SetRow(left, row);

        Grid.SetColumn(left, 0);

        Grid.SetRow(right, row);

        Grid.SetColumn(right, 1);

        grid.Children.Add(left);

        grid.Children.Add(right);

    }



    private void NotifyChanged()

    {

        if (!_loadingEditors)

            SaveColorEditorsToDraft();



        UpdatePreview();

        Changed?.Invoke();

    }



    private void UpdatePreview()

    {

        _fontSizeLabel.Text = $"サイズ {(int)_fontSizeSlider.Value}px";

        _marginXLabel.Text = $"横 {(int)_marginXSlider.Value}px";

        _marginYLabel.Text = $"縦 {(int)_marginYSlider.Value}px";

        _borderThicknessLabel.Text = $"枠の太さ {(int)_borderThicknessSlider.Value}px";

        _textStrokeThicknessLabel.Text = $"文字縁の太さ {(int)_textStrokeThicknessSlider.Value}px";



        var borderOn = _borderEnabledCheck.IsChecked == true;

        var backgroundOn = _backgroundEnabledCheck.IsChecked == true;

        var textStrokeOn = _textStrokeEnabledCheck.IsChecked == true;

        _borderColorEditor.SetEnabled(borderOn);

        _borderThicknessSlider.IsEnabled = borderOn;

        _backgroundColorEditor.SetEnabled(backgroundOn);

        _textStrokeColorEditor.SetEnabled(textStrokeOn);

        _textStrokeThicknessSlider.IsEnabled = textStrokeOn;



        var previewConfig = BuildConfig();

        var previewStyle = SeatNameOverlayResolver.Resolve(previewConfig, _selectedSeatId);

        var previewName = HostSeatNameLabels.GetLabel(_selectedSeatId).Replace($"席{_selectedSeatId} ", string.Empty);

        if (previewName.StartsWith("ID ", StringComparison.Ordinal))

            previewName = "サンプル席名";



        SeatNameOverlayUi.Apply(

            _previewBorder,

            _previewText,

            previewStyle,

            previewName,

            480,

            SeatNameOverlayStyle.ReferenceHeight);

    }

}


