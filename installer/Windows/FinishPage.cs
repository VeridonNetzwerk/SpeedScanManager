using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using FontFamily = System.Windows.Media.FontFamily;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using Point = System.Windows.Point;

namespace SpeedScanInstaller;

internal enum CheckState { Ok, Warn, Off }

/// <summary>
/// Building blocks of the finish pages (installer and uninstaller): checklist rows and the
/// entry animation (banner slides in, the check mark pops, the cards follow one by one).
/// </summary>
internal static class FinishPage
{
    private static readonly Color Green = Color.FromRgb(0x23, 0xA5, 0x5A);
    private static readonly Color Amber = Color.FromRgb(0xF5, 0x9E, 0x0B);

    /// <summary>Checklist row: round status mark, title and an optional muted detail line.</summary>
    public static FrameworkElement Row(string title, string? detail, CheckState state)
    {
        var grid = new Grid { Margin = new Thickness(0, 5, 14, 5) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition());

        var mark = new Border
        {
            Width = 20,
            Height = 20,
            CornerRadius = new CornerRadius(10),
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 1, 0, 0),
        };
        var glyph = new TextBlock
        {
            FontFamily = new FontFamily("Segoe MDL2 Assets"),
            FontSize = 10,
            FontWeight = FontWeights.Bold,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        switch (state)
        {
            case CheckState.Ok:
                mark.Background = new SolidColorBrush(Green);
                glyph.Text = "";
                glyph.Foreground = Brushes.White;
                break;
            case CheckState.Warn:
                mark.Background = new SolidColorBrush(Amber);
                glyph.Text = "!";
                glyph.FontFamily = new FontFamily("Segoe UI");
                glyph.FontSize = 12;
                glyph.Foreground = Brushes.White;
                break;
            default:
                mark.SetResourceReference(Border.BackgroundProperty, "TrackBrush");
                glyph.Text = "";
                glyph.SetResourceReference(TextBlock.ForegroundProperty, "TextMutedBrush");
                break;
        }
        mark.Child = glyph;
        grid.Children.Add(mark);

        var text = new StackPanel { Margin = new Thickness(10, 0, 0, 0) };
        var titleBlock = new TextBlock { Text = title, FontSize = 13, TextTrimming = TextTrimming.CharacterEllipsis };
        titleBlock.SetResourceReference(TextBlock.ForegroundProperty, state == CheckState.Off ? "TextSecondaryBrush" : "TextBrush");
        text.Children.Add(titleBlock);
        if (!string.IsNullOrEmpty(detail))
        {
            var detailBlock = new TextBlock
            {
                Text = detail,
                FontSize = 11.5,
                TextTrimming = TextTrimming.CharacterEllipsis,
                ToolTip = detail,
                Margin = new Thickness(0, 1, 0, 0),
            };
            detailBlock.SetResourceReference(TextBlock.ForegroundProperty, "TextMutedBrush");
            text.Children.Add(detailBlock);
        }
        Grid.SetColumn(text, 1);
        grid.Children.Add(text);
        return grid;
    }

    /// <summary>No entry animation (screenshots).</summary>
    internal static bool Disabled;

    /// <summary>Entry animation: banner slides in, the check pops, the other parts follow.</summary>
    public static void Play(FrameworkElement banner, FrameworkElement check, params FrameworkElement[] parts)
    {
        if (Disabled) return;
        SlideIn(banner, 0);

        var scale = new ScaleTransform(0.3, 0.3);
        check.RenderTransformOrigin = new Point(0.5, 0.5);
        check.RenderTransform = scale;
        var pop = new DoubleAnimation(0.3, 1, TimeSpan.FromMilliseconds(520))
        {
            BeginTime = TimeSpan.FromMilliseconds(140),
            EasingFunction = new BackEase { Amplitude = 0.55, EasingMode = EasingMode.EaseOut },
        };
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, pop);
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, pop);

        int delay = 200;
        foreach (var part in parts)
        {
            if (part.Visibility != Visibility.Visible) continue;
            SlideIn(part, delay);
            delay += 80;
        }
    }

    private static void SlideIn(FrameworkElement element, int delayMs)
    {
        var move = new TranslateTransform(0, 14);
        element.RenderTransform = move;
        element.Opacity = 0;
        var begin = TimeSpan.FromMilliseconds(delayMs);
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        element.BeginAnimation(UIElement.OpacityProperty,
            new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(320)) { BeginTime = begin, EasingFunction = ease });
        move.BeginAnimation(TranslateTransform.YProperty,
            new DoubleAnimation(14, 0, TimeSpan.FromMilliseconds(380)) { BeginTime = begin, EasingFunction = ease });
    }
}
