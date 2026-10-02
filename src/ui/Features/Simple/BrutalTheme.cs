using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace Nikse.SubtitleEdit.Features.Simple;

/// <summary>Square, high-contrast look of the simple window: black lines, white boxes, yellow text marks.</summary>
public static class BrutalTheme
{
    public static readonly IBrush Ink = new SolidColorBrush(Color.Parse("#111111"));
    public static readonly IBrush Paper = new SolidColorBrush(Color.Parse("#FFFFFF"));
    public static readonly IBrush PaperDim = new SolidColorBrush(Color.Parse("#F2F2EE"));
    public static readonly IBrush Marker = new SolidColorBrush(Color.Parse("#FFE36E"));
    public static readonly IBrush MarkerActive = new SolidColorBrush(Color.Parse("#FFB800"));
    public static readonly IBrush Wave = new SolidColorBrush(Color.Parse("#3A9A4A"));
    public static readonly IBrush Cursor = new SolidColorBrush(Color.Parse("#E5322D"));
    public static readonly IBrush Viewport = new SolidColorBrush(Color.Parse("#330057FF"));
    public static readonly IPen InkPen = new Pen(Ink, 2);
    public static readonly IPen ThinInkPen = new Pen(Ink, 1);
    public static readonly IPen CursorPen = new Pen(Cursor, 2);
    public static readonly FontFamily Mono = new("Menlo, Consolas, DejaVu Sans Mono, monospace");
    public static readonly Thickness Line = new(2);

    public static Border Box(Control child, Thickness? padding = null) => new()
    {
        BorderBrush = Ink,
        BorderThickness = Line,
        Background = Paper,
        CornerRadius = new CornerRadius(0),
        Padding = padding ?? new Thickness(0),
        Child = child,
    };

    public static Button Button(string text, string automationId)
    {
        var button = new Button
        {
            Content = text,
            FontFamily = Mono,
            FontWeight = FontWeight.Bold,
            Foreground = Ink,
            Background = Paper,
            BorderBrush = Ink,
            BorderThickness = Line,
            CornerRadius = new CornerRadius(0),
            Padding = new Thickness(12, 6),
            MinWidth = 44,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Stretch,
        };
        Avalonia.Automation.AutomationProperties.SetAutomationId(button, automationId);
        return button;
    }

    public static TextBlock Label(string text, double size = 13) => new()
    {
        Text = text,
        FontFamily = Mono,
        FontWeight = FontWeight.Bold,
        FontSize = size,
        Foreground = Ink,
        VerticalAlignment = VerticalAlignment.Center,
    };
}
