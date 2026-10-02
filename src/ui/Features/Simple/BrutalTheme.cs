using Avalonia;
using System;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;

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
    public static readonly IBrush Muted = new SolidColorBrush(Color.Parse("#9A9A94"));
    public static readonly IBrush Selected = new SolidColorBrush(Color.Parse("#D6E8FF"));
    public static readonly IBrush Focus = new SolidColorBrush(Color.Parse("#0057FF"));
    public static readonly IPen InkPen = new Pen(Ink, 2);
    public static readonly IPen ThinInkPen = new Pen(Ink, 1);
    public static readonly IPen CursorPen = new Pen(Cursor, 2);
    public static readonly FontFamily Mono = new("Menlo, Consolas, DejaVu Sans Mono, monospace");
    public static readonly Thickness Line = new(2);

    public const string ButtonClass = "brutal";
    public const string PrimaryClass = "primary";

    /// <summary>
    /// Hover, pressed and disabled looks. The Fluent theme paints those states on the button's
    /// inner ContentPresenter, over the button's own Background/Foreground, which turned the
    /// text white on a light background on hover. These styles target the same presenter.
    /// </summary>
    public static Styles CreateStyles()
    {
        static Style Presenter(Func<Selector?, Selector> state, IBrush background, IBrush foreground, IBrush border) => new(x =>
            state(x.OfType<Button>().Class(ButtonClass)).Template().OfType<ContentPresenter>().Name("PART_ContentPresenter"))
        {
            Setters =
            {
                new Setter(ContentPresenter.BackgroundProperty, background),
                new Setter(ContentPresenter.ForegroundProperty, foreground),
                new Setter(ContentPresenter.BorderBrushProperty, border),
            },
        };

        return new Styles
        {
            Presenter(x => x.Class(":pointerover"), Marker, Ink, Ink),
            Presenter(x => x.Class(":pressed"), MarkerActive, Ink, Ink),
            Presenter(x => x.Class(PrimaryClass).Class(":pointerover"), MarkerActive, Ink, Ink),
            Presenter(x => x.Class(":disabled"), PaperDim, Muted, Muted),

            new Style(x => x.OfType<ComboBox>().Class(ButtonClass).Class(":pointerover").Template().OfType<Border>().Name("Background"))
            {
                Setters =
                {
                    new Setter(Border.BackgroundProperty, Marker),
                    new Setter(Border.BorderBrushProperty, Ink),
                },
            },
            new Style(x => x.OfType<ComboBox>().Class(ButtonClass).Class(":pointerover").Template().OfType<ContentControl>().Name("ContentPresenter"))
            {
                Setters = { new Setter(ContentControl.ForegroundProperty, Ink) },
            },
            new Style(x => x.OfType<ComboBoxItem>().Class(":pointerover").Template().OfType<ContentPresenter>().Name("PART_ContentPresenter"))
            {
                Setters = { new Setter(ContentPresenter.BackgroundProperty, Marker), new Setter(ContentPresenter.ForegroundProperty, Ink) },
            },
            // The subtitle in use: light blue; hovering it still turns it yellow like the others.
            new Style(x => x.OfType<ComboBoxItem>().Class(":selected").Template().OfType<ContentPresenter>().Name("PART_ContentPresenter"))
            {
                Setters = { new Setter(ContentPresenter.BackgroundProperty, Selected), new Setter(ContentPresenter.ForegroundProperty, Ink) },
            },
            new Style(x => x.OfType<ComboBoxItem>().Class(":selected").Class(":pointerover").Template().OfType<ContentPresenter>().Name("PART_ContentPresenter"))
            {
                Setters = { new Setter(ContentPresenter.BackgroundProperty, Marker), new Setter(ContentPresenter.ForegroundProperty, Ink) },
            },

            // Text fields: the Fluent theme turns the box dark while focused or hovered, with dark text on it.
            TextBoxState(x => x.Class(":pointerover"), Paper, Ink),
            TextBoxState(x => x.Class(":focus"), Paper, Focus),
            new Style(x => x.OfType<TextBox>().Class(":focus"))
            {
                Setters = { new Setter(TextBox.ForegroundProperty, Ink), new Setter(TextBox.CaretBrushProperty, Ink) },
            },
            new Style(x => x.OfType<TextBox>().Class(":pointerover"))
            {
                Setters = { new Setter(TextBox.ForegroundProperty, Ink) },
            },
        };
    }

    private static Style TextBoxState(Func<Selector?, Selector> state, IBrush background, IBrush border) => new(x =>
        state(x.OfType<TextBox>()).Template().OfType<Border>().Name("PART_BorderElement"))
    {
        Setters =
        {
            new Setter(Border.BackgroundProperty, background),
            new Setter(Border.BorderBrushProperty, border),
            new Setter(Border.BorderThicknessProperty, Line),
        },
    };

    public static readonly Cursor Hand = new(StandardCursorType.Hand);

    public static Border Box(Control child, Thickness? padding = null) => new()
    {
        BorderBrush = Ink,
        BorderThickness = Line,
        Background = Paper,
        CornerRadius = new CornerRadius(0),
        Padding = padding ?? new Thickness(0),
        Child = child,
    };

    /// <summary>
    /// The label as its own TextBlock with the font set locally: Subtitle Edit styles every
    /// TextBlock with the UI font, and the one a Button generates for string content took it,
    /// with metrics that put capitals above the middle of the button.
    /// </summary>
    public static TextBlock ButtonLabel(string text) => new()
    {
        Text = text,
        FontFamily = Mono,
        FontWeight = FontWeight.Bold,
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center,
        TextAlignment = TextAlignment.Center,
    };

    public static void SetText(Button button, string text) => button.Content = ButtonLabel(text);

    /// <summary>A drop-down in the same look; items shown as dark mono text in every state.</summary>
    public static ComboBox ComboBox(string automationId, double minWidth)
    {
        var combo = new ComboBox
        {
            MinWidth = minWidth,
            FontFamily = Mono,
            Foreground = Ink,
            Background = Paper,
            BorderBrush = Ink,
            BorderThickness = Line,
            CornerRadius = new CornerRadius(0),
            Cursor = Hand,
            ItemTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<object>((item, _) => new TextBlock
            {
                Text = item?.ToString() ?? string.Empty,
                FontFamily = Mono,
                Foreground = Ink,
                VerticalAlignment = VerticalAlignment.Center,
            }),
        };
        combo.Classes.Add(ButtonClass);
        Avalonia.Automation.AutomationProperties.SetAutomationId(combo, automationId);
        return combo;
    }

    /// <summary>A small circled "i"; hovering it shows <paramref name="tip"/>.</summary>
    public static Control InfoIcon(string tip, string automationId)
    {
        var icon = new Border
        {
            Width = 20,
            Height = 20,
            CornerRadius = new CornerRadius(10),
            BorderBrush = Ink,
            BorderThickness = new Thickness(1.5),
            Background = Paper,
            VerticalAlignment = VerticalAlignment.Center,
            Cursor = new Cursor(StandardCursorType.Help),
            Child = new TextBlock
            {
                Text = "i",
                FontFamily = Mono,
                FontWeight = FontWeight.Bold,
                FontSize = 12,
                Foreground = Ink,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            },
        };
        ToolTip.SetTip(icon, new TextBlock { Text = tip, TextWrapping = TextWrapping.Wrap, MaxWidth = 380, FontFamily = Mono, FontSize = 12 });
        ToolTip.SetShowDelay(icon, 0);
        Avalonia.Automation.AutomationProperties.SetAutomationId(icon, automationId);
        Avalonia.Automation.AutomationProperties.SetHelpText(icon, tip);
        return icon;
    }

    public static Button Button(string text, string automationId)
    {
        var button = new Button
        {
            Content = ButtonLabel(text),
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
            VerticalContentAlignment = VerticalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Stretch,
            Cursor = Hand,
        };
        button.Classes.Add(ButtonClass);
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
