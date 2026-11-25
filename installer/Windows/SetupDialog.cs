using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using Application = System.Windows.Application;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using Button = System.Windows.Controls.Button;
using FontFamily = System.Windows.Media.FontFamily;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using Orientation = System.Windows.Controls.Orientation;

namespace SpeedScanInstaller;

/// <summary>
/// Message box in the setup's own design (light / dark, translated buttons) instead of the
/// Windows one. Same call shape as <see cref="System.Windows.MessageBox.Show(string, string, MessageBoxButton, MessageBoxImage)"/>,
/// so the windows use it through <c>using MessageBox = SpeedScanInstaller.SetupDialog;</c>.
/// </summary>
internal static class SetupDialog
{
    public static MessageBoxResult Show(string text, string caption,
        MessageBoxButton buttons = MessageBoxButton.OK, MessageBoxImage image = MessageBoxImage.None)
    {
        var result = buttons switch
        {
            MessageBoxButton.OKCancel or MessageBoxButton.YesNoCancel => MessageBoxResult.Cancel,
            MessageBoxButton.YesNo => MessageBoxResult.No,
            _ => MessageBoxResult.OK,
        };

        var owner = Application.Current?.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
                    ?? Application.Current?.MainWindow;
        if (owner is { IsVisible: false }) owner = null;

        var window = new Window
        {
            Title = caption,
            WindowStyle = WindowStyle.None,
            ResizeMode = ResizeMode.NoResize,
            SizeToContent = SizeToContent.Height,
            Width = 460,
            ShowInTaskbar = owner == null,
            WindowStartupLocation = owner != null ? WindowStartupLocation.CenterOwner : WindowStartupLocation.CenterScreen,
            Owner = owner,
            FontFamily = new FontFamily("Segoe UI"),
            UseLayoutRounding = true,
            Language = XmlLanguage.GetLanguage(I18n.Current.Code),
        };
        TextOptions.SetTextFormattingMode(window, TextFormattingMode.Display);
        window.SetResourceReference(Window.BackgroundProperty, "WindowBrush");
        window.SourceInitialized += (_, _) => Theme.RoundCorners(window);

        // Icon tile + title + text
        var (glyph, color) = image switch
        {
            MessageBoxImage.Error => ("", "#E5484D"),
            MessageBoxImage.Warning => ("", "#F59E0B"),
            MessageBoxImage.Question => ("", "#5865F2"),
            _ => ("", "#3B82F6"),
        };
        var tile = new Border { Width = 40, Height = 40, CornerRadius = new CornerRadius(10), VerticalAlignment = VerticalAlignment.Top,
            Background = (Brush)new BrushConverter().ConvertFromString(color)! };
        tile.Child = new TextBlock { Text = glyph, FontFamily = new FontFamily("Segoe MDL2 Assets"), FontSize = 18,
            Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };

        var title = new TextBlock { Text = caption, FontSize = 15, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap };
        title.SetResourceReference(TextBlock.ForegroundProperty, "TextBrush");
        var message = new TextBlock { Text = text, FontSize = 12.5, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 0), LineHeight = 19 };
        message.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");
        var texts = new StackPanel { Margin = new Thickness(14, 0, 0, 0) };
        texts.Children.Add(title);
        texts.Children.Add(message);
        var body = new DockPanel { Margin = new Thickness(22, 22, 22, 20) };
        DockPanel.SetDock(tile, Dock.Left);
        body.Children.Add(tile);
        body.Children.Add(texts);
        body.MouseLeftButtonDown += (_, e) => { if (e.ClickCount == 1) window.DragMove(); };
        body.Background = Brushes.Transparent;

        // Buttons (primary on the right)
        var bar = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(16, 12, 16, 12) };
        Button? defaultButton = null;
        void Add(string label, MessageBoxResult value, bool primary)
        {
            var b = new Button { Content = label, MinWidth = 96, Margin = new Thickness(8, 0, 0, 0) };
            b.SetResourceReference(FrameworkElement.StyleProperty, primary ? "AccentButton" : "SecondaryButton");
            b.Click += (_, _) => { result = value; window.Close(); };
            bar.Children.Add(b);
            if (primary) defaultButton = b;
        }
        switch (buttons)
        {
            case MessageBoxButton.OKCancel:
                Add(I18n.T("Abbrechen"), MessageBoxResult.Cancel, false);
                Add(I18n.T("OK"), MessageBoxResult.OK, true);
                break;
            case MessageBoxButton.YesNoCancel:
                Add(I18n.T("Abbrechen"), MessageBoxResult.Cancel, false);
                Add(I18n.T("Nein"), MessageBoxResult.No, false);
                Add(I18n.T("Ja"), MessageBoxResult.Yes, true);
                break;
            case MessageBoxButton.YesNo:
                Add(I18n.T("Nein"), MessageBoxResult.No, false);
                Add(I18n.T("Ja"), MessageBoxResult.Yes, true);
                break;
            default:
                Add(I18n.T("OK"), MessageBoxResult.OK, true);
                break;
        }
        var footer = new Border { Child = bar, BorderThickness = new Thickness(0, 1, 0, 0) };
        footer.SetResourceReference(Border.BackgroundProperty, "FooterBrush");
        footer.SetResourceReference(Border.BorderBrushProperty, "BorderBrush");

        var root = new DockPanel();
        DockPanel.SetDock(footer, Dock.Bottom);
        root.Children.Add(footer);
        root.Children.Add(body);
        var frame = new Border { Child = root, BorderThickness = new Thickness(1) };
        frame.SetResourceReference(Border.BorderBrushProperty, "BorderBrush");
        frame.SetResourceReference(Border.BackgroundProperty, "WindowBrush");
        window.Content = frame;

        window.PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape) window.Close(); // keeps the "cancel" result
        };
        window.Loaded += (_, _) => defaultButton?.Focus();
        (image switch
        {
            MessageBoxImage.Error => System.Media.SystemSounds.Hand,
            MessageBoxImage.Warning => System.Media.SystemSounds.Exclamation,
            MessageBoxImage.Question => System.Media.SystemSounds.Question,
            _ => System.Media.SystemSounds.Asterisk,
        }).Play();
        window.ShowDialog();
        return result;
    }
}
