using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Nikse.SubtitleEdit.Features.Simple;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Xunit;

namespace UITests.Features.Simple;

/// <summary>
/// Measures the rendered pixels: inside every button, the empty space above the label must
/// match the empty space below it (within a pixel or two).
/// </summary>
public class ButtonCenteringTests
{
    private static void Pump()
    {
        for (var i = 0; i < 5; i++)
        {
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        }
    }

    /// <summary>(gap above, gap below) of the dark pixels inside the button, border excluded.</summary>
    public static (int Top, int Bottom) InkGaps(WriteableBitmap frame, Rect box, double scale)
    {
        using var buffer = frame.Lock();
        var stride = buffer.RowBytes;
        var pixels = new byte[stride * buffer.Size.Height];
        Marshal.Copy(buffer.Address, pixels, 0, pixels.Length);

        var inset = 4; // skip the 2 px border and anti-aliasing
        int x0 = (int)(box.X * scale) + inset, x1 = (int)((box.X + box.Width) * scale) - inset;
        int y0 = (int)(box.Y * scale) + inset, y1 = (int)((box.Y + box.Height) * scale) - inset;
        var inkRows = new List<int>();
        for (var y = y0; y < y1; y++)
        {
            for (var x = x0; x < x1; x++)
            {
                var i = y * stride + x * 4;
                var luminance = (pixels[i] + pixels[i + 1] + pixels[i + 2]) / 3;
                if (luminance < 110)
                {
                    inkRows.Add(y);
                    break;
                }
            }
        }

        return inkRows.Count == 0 ? (-1, -1) : (inkRows.Min() - y0, y1 - 1 - inkRows.Max());
    }

    /// <summary>What Subtitle Edit does at startup: every TextBlock gets the UI font.</summary>
    private static Avalonia.Styling.Styles AppUiFontStyle() => new()
    {
        new Avalonia.Styling.Style(x => x.Is<TextBlock>())
        {
            Setters = { new Avalonia.Styling.Setter(TextBlock.FontFamilyProperty, new Avalonia.Media.FontFamily("Helvetica Neue, Arial, sans-serif")) },
        },
    };

    private static void AssertButtonsCentered(TopLevel top)
    {
        Pump();
        var frame = top.CaptureRenderedFrame()!;
        var scale = frame.PixelSize.Width / top.Bounds.Width;
        var report = new List<string>();
        foreach (var button in top.GetVisualDescendants().OfType<Button>().Where(b => b.Classes.Contains(BrutalTheme.ButtonClass) && b.IsEffectivelyVisible && b.IsEffectivelyEnabled))
        {
            var origin = button.TranslatePoint(new Point(0, 0), top)!.Value;
            var (above, below) = InkGaps(frame, new Rect(origin, button.Bounds.Size), scale);
            var id = Avalonia.Automation.AutomationProperties.GetAutomationId(button);
            report.Add($"{id}: {above}/{below}");
            if (above >= 0)
            {
                Assert.True(Math.Abs(above - below) <= 2, $"{id} not centered: {above} px above, {below} px below. All: {string.Join("; ", report)}");
            }
        }

        Assert.NotEmpty(report);
    }

    [AvaloniaFact]
    public void MainWindow_EveryButtonLabelIsVerticallyCentered_WithTheAppUiFont()
    {
        SimpleStrings.Current = SimpleStrings.English;
        var styles = AppUiFontStyle();
        Application.Current!.Styles.Add(styles);
        try
        {
            var window = new SimpleWindow(createPlayer: false, new FakeVideoPlayer()) { Width = 1200, Height = 860 };
            window.Show();
            AssertButtonsCentered(window);
            window.Close();
        }
        finally
        {
            Application.Current.Styles.Remove(styles);
        }
    }

    [AvaloniaFact]
    public void Dialogs_ButtonsAreVerticallyCentered_WithTheAppUiFont()
    {
        SimpleStrings.Current = SimpleStrings.English;
        var styles = AppUiFontStyle();
        Application.Current!.Styles.Add(styles);
        try
        {
            var save = new SaveChoiceDialog(new SaveRequest(null, "pt-BR", null, InsideIsDefault: true));
            save.Show();
            AssertButtonsCentered(save);
            save.Close();

            var settings = new OnlineSettingsDialog(new Nikse.SubtitleEdit.UiLogic.SimpleSync.OpenSubtitlesSettings());
            settings.Show();
            AssertButtonsCentered(settings);
            settings.Close();
        }
        finally
        {
            Application.Current.Styles.Remove(styles);
        }
    }
}
