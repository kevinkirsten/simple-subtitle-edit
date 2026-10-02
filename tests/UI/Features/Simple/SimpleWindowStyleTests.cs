using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Nikse.SubtitleEdit.Features.Simple;
using System.Linq;
using Xunit;

namespace UITests.Features.Simple;

/// <summary>
/// The Fluent theme repaints buttons on hover; text once went white on a light background.
/// Every button must stay readable in every state, and everything clickable shows a hand cursor.
/// </summary>
public class SimpleWindowStyleTests
{
    private static void Pump()
    {
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }

    private static void AssertReadableOnHover(Window window)
    {
        var buttons = window.GetVisualDescendants().OfType<Button>().Where(b => b.Classes.Contains(BrutalTheme.ButtonClass) && b.IsEffectivelyVisible).ToList();
        Assert.NotEmpty(buttons);

        foreach (var button in buttons)
        {
            var center = button.TranslatePoint(new Point(button.Bounds.Width / 2, button.Bounds.Height / 2), window)!.Value;
            window.MouseMove(center);
            Pump();

            var presenter = button.GetVisualDescendants().OfType<ContentPresenter>().First(p => p.Name == "PART_ContentPresenter");
            var label = Avalonia.Automation.AutomationProperties.GetAutomationId(button);
            if (button.IsEnabled)
            {
                Assert.True(button.IsPointerOver, label);
                Assert.Equal(((ISolidColorBrush)BrutalTheme.Ink).Color, ((ISolidColorBrush)presenter.Foreground!).Color);
                var background = ((ISolidColorBrush)presenter.Background!).Color;
                Assert.True(background == ((ISolidColorBrush)BrutalTheme.Marker).Color || background == ((ISolidColorBrush)BrutalTheme.MarkerActive).Color, $"{label}: hover background {background}");
            }
            else
            {
                Assert.Equal(((ISolidColorBrush)BrutalTheme.Muted).Color, ((ISolidColorBrush)presenter.Foreground!).Color);
            }

            Assert.Same(BrutalTheme.Hand, button.Cursor);
        }
    }

    [AvaloniaFact]
    public void MainWindow_EveryButtonStaysReadableOnHover_AndShowsAHand()
    {
        SimpleStrings.Current = SimpleStrings.English;
        var window = new SimpleWindow(createPlayer: false, new FakeVideoPlayer()) { Width = 1200, Height = 860 };
        window.Show();
        Pump();

        AssertReadableOnHover(window);

        Assert.Same(BrutalTheme.Hand, window.Timeline.Cursor);
        Assert.Same(BrutalTheme.Hand, window.Minimap.Cursor);
        Assert.Same(BrutalTheme.Hand, window.GetVisualDescendants().OfType<ComboBox>().Single().Cursor);
        window.Close();
    }

    [AvaloniaFact]
    public void UnsavedDialog_ButtonsStayReadableOnHover()
    {
        SimpleStrings.Current = SimpleStrings.English;
        var dialog = new UnsavedDialog();
        dialog.Show();
        Pump();

        AssertReadableOnHover(dialog);
        Assert.Equal(BrutalTheme.Mono, dialog.FontFamily);
        dialog.Close();
    }
}
