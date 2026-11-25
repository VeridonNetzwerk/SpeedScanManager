using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using Button = System.Windows.Controls.Button;
using Orientation = System.Windows.Controls.Orientation;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using FontFamily = System.Windows.Media.FontFamily;
using Control = System.Windows.Controls.Control;
using Image = System.Windows.Controls.Image;
using System.Windows.Media.Imaging;

namespace SpeedScanInstaller;

/// <summary>
/// Title bar button "🌐 Deutsch ▾" with a two-column list of the languages. Choosing one calls
/// <see cref="I18n.Set"/>; the windows retranslate themselves on <see cref="I18n.Changed"/>.
/// </summary>
internal sealed class LanguagePicker
{
    public Button Button { get; }
    private readonly TextBlock _name;
    private readonly Image _flag;
    private readonly Popup _popup;

    public LanguagePicker()
    {
        _name = new TextBlock { VerticalAlignment = VerticalAlignment.Center };
        var content = new StackPanel { Orientation = Orientation.Horizontal };
        _flag = Flag(I18n.Current.Code);
        _flag.Margin = new Thickness(0, 0, 8, 0);
        content.Children.Add(_flag);
        content.Children.Add(_name);
        content.Children.Add(new TextBlock
        {
            Text = "", FontFamily = new FontFamily("Segoe MDL2 Assets"), FontSize = 8,
            VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(7, 2, 0, 0),
        });
        Button = new Button { Content = content };
        Button.SetResourceReference(FrameworkElement.StyleProperty, "TitleTextButton");
        Button.ToolTip = "Sprache";

        var grid = new UniformGrid { Columns = 2 };
        foreach (var language in I18n.Languages)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal };
            var flag = Flag(language.Code);
            flag.Margin = new Thickness(0, 0, 9, 0);
            row.Children.Add(flag);
            row.Children.Add(new TextBlock { Text = language.NativeName, VerticalAlignment = VerticalAlignment.Center });
            var entry = new Button { Content = row, Tag = language.Code, MinWidth = 150 };
            entry.SetResourceReference(FrameworkElement.StyleProperty, "MenuEntry");
            entry.Click += (_, _) =>
            {
                _popup!.IsOpen = false;
                if (language.Code != I18n.Current.Code)
                    I18n.Set(language.Code);
            };
            grid.Children.Add(entry);
        }
        var border = new Border
        {
            Child = grid, CornerRadius = new CornerRadius(8), BorderThickness = new Thickness(1),
            Padding = new Thickness(4), Margin = new Thickness(0, 4, 0, 0),
        };
        border.SetResourceReference(Border.BackgroundProperty, "HeaderBrush");
        border.SetResourceReference(Border.BorderBrushProperty, "BorderBrush");
        _popup = new Popup
        {
            Child = border, StaysOpen = false, AllowsTransparency = true,
            PlacementTarget = Button, Placement = PlacementMode.Bottom, HorizontalOffset = -150,
        };
        Button.Click += (_, _) => _popup.IsOpen = !_popup.IsOpen;
        Refresh();
    }

    /// <summary>Shows the current language and marks it in the list.</summary>
    public void Refresh()
    {
        _name.Text = I18n.Current.NativeName;
        _flag.Source = FlagSource(I18n.Current.Code);
        foreach (Button entry in ((UniformGrid)((Border)_popup.Child).Child).Children)
        {
            bool current = (string)entry.Tag == I18n.Current.Code;
            entry.FontWeight = current ? FontWeights.SemiBold : FontWeights.Normal;
            if (current) entry.SetResourceReference(Control.BackgroundProperty, "SelectedBrush");
            else entry.ClearValue(Control.BackgroundProperty);
        }
    }

    /// <summary>Flag image (installer/lang/flags/&lt;code&gt;.png, drawn like the app's ColorIcons.DrawFlag).</summary>
    private static Image Flag(string code) => new()
    {
        Source = FlagSource(code), Width = 20, Height = 15, VerticalAlignment = VerticalAlignment.Center,
        SnapsToDevicePixels = true,
    };

    private static ImageSource? FlagSource(string code)
    {
        try { return new BitmapImage(new Uri($"pack://application:,,,/SpeedScanInstaller;component/lang/flags/{code}.png")); }
        catch { return null; }
    }
}
