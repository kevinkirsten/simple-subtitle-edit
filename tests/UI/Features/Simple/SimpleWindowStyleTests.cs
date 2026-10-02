using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.LogicalTree;
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
    public async System.Threading.Tasks.Task SubtitlePicker_SelectedNameStaysDarkOnHover()
    {
        SimpleStrings.Current = SimpleStrings.English;
        var dir = System.IO.Directory.CreateTempSubdirectory("sse-style-");
        try
        {
            var video = System.IO.Path.Combine(dir.FullName, "Demo S01E01.mkv");
            System.IO.File.WriteAllBytes(video, [0]);
            System.IO.File.WriteAllText(System.IO.Path.ChangeExtension(video, ".srt"), "1\n00:00:01,000 --> 00:00:02,000\nOi\n");
            var window = new SimpleWindow(createPlayer: false, new FakeVideoPlayer()) { Width = 1200, Height = 860 };
            window.Show();
            await window.OpenVideoAsync(video);
            Pump();

            var combo = window.GetVisualDescendants().OfType<ComboBox>().Single();
            var center = combo.TranslatePoint(new Point(combo.Bounds.Width / 2, combo.Bounds.Height / 2), window)!.Value;
            window.MouseMove(center);
            Pump();

            var name = combo.GetVisualDescendants().OfType<TextBlock>().First(t => t.Text == "Demo S01E01.srt");
            Assert.True(combo.IsPointerOver);
            Assert.Equal(((ISolidColorBrush)BrutalTheme.Ink).Color, ((ISolidColorBrush)name.Foreground!).Color);
            window.Close();
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    [AvaloniaFact]
    public void OnlineSettings_FocusedFieldStaysLightWithDarkText()
    {
        SimpleStrings.Current = SimpleStrings.English;
        var dialog = new OnlineSettingsDialog(new Nikse.SubtitleEdit.UiLogic.SimpleSync.OpenSubtitlesSettings { Username = "quitolice" });
        dialog.Show();
        Pump();
        var box = dialog.GetVisualDescendants().OfType<TextBox>().First(t => Avalonia.Automation.AutomationProperties.GetAutomationId(t) == "OnlineUsername");

        box.Focus();
        var center = box.TranslatePoint(new Point(box.Bounds.Width / 2, box.Bounds.Height / 2), dialog)!.Value;
        dialog.MouseMove(center);
        Pump();

        var border = box.GetVisualDescendants().OfType<Border>().First(b => b.Name == "PART_BorderElement");
        Assert.True(box.IsFocused);
        Assert.Equal(((ISolidColorBrush)BrutalTheme.Paper).Color, ((ISolidColorBrush)border.Background!).Color);
        Assert.Equal(((ISolidColorBrush)BrutalTheme.Ink).Color, ((ISolidColorBrush)box.Foreground!).Color);
        dialog.Close();
    }

    [AvaloniaFact]
    public async System.Threading.Tasks.Task SubtitlePicker_SelectedEntryIsLightBlue_HoveredIsYellow()
    {
        SimpleStrings.Current = SimpleStrings.English;
        var dir = System.IO.Directory.CreateTempSubdirectory("sse-style-");
        try
        {
            var video = System.IO.Path.Combine(dir.FullName, "ep.mkv");
            System.IO.File.WriteAllBytes(video, [0]);
            System.IO.File.WriteAllText(System.IO.Path.Combine(dir.FullName, "ep.srt"), "1\n00:00:01,000 --> 00:00:02,000\nA\n");
            System.IO.File.WriteAllText(System.IO.Path.Combine(dir.FullName, "ep.en.srt"), "1\n00:00:01,000 --> 00:00:02,000\nB\n");
            var window = new SimpleWindow(createPlayer: false, new FakeVideoPlayer()) { Width = 1200, Height = 860 };
            window.Show();
            await window.OpenVideoAsync(video);
            Pump();
            var combo = window.GetVisualDescendants().OfType<ComboBox>().Single();
            combo.IsDropDownOpen = true;
            Pump();

            var items = combo.GetLogicalDescendants().OfType<ComboBoxItem>().ToList();
            var selected = items.Single(i => i.IsSelected);
            var presenter = selected.GetVisualDescendants().OfType<ContentPresenter>().First(p => p.Name == "PART_ContentPresenter");
            Assert.Equal(((ISolidColorBrush)BrutalTheme.Selected).Color, ((ISolidColorBrush)presenter.Background!).Color);

            // The drop-down lives in its own popup window: move the mouse there.
            var popup = TopLevel.GetTopLevel(selected)!;
            popup.MouseMove(selected.TranslatePoint(new Point(20, selected.Bounds.Height / 2), popup)!.Value);
            Pump();
            Assert.True(selected.IsPointerOver);
            Assert.Equal(((ISolidColorBrush)BrutalTheme.Marker).Color, ((ISolidColorBrush)presenter.Background!).Color);
            combo.IsDropDownOpen = false;
            window.Close();
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    [AvaloniaFact]
    public async System.Threading.Tasks.Task PlayPause_TogglingDoesNotShiftTheBar()
    {
        SimpleStrings.Current = SimpleStrings.English;
        var player = new FakeVideoPlayer();
        var window = new SimpleWindow(createPlayer: false, player) { Width = 1200, Height = 860 };
        window.Show();
        Pump();
        var play = window.GetVisualDescendants().OfType<Button>().First(b => Avalonia.Automation.AutomationProperties.GetAutomationId(b) == "PlayPause");
        var offset = window.GetVisualDescendants().OfType<TextBlock>().First(t => Avalonia.Automation.AutomationProperties.GetAutomationId(t) == "OffsetValue");
        var widthPlaying = 0.0;
        var offsetXs = new System.Collections.Generic.List<double>();

        foreach (var playing in new[] { false, true, false })
        {
            if (player.IsPlaying != playing)
            {
                player.PlayOrPause();
            }

            await System.Threading.Tasks.Task.Delay(150, TestContext.Current.CancellationToken); // the idle poll updates the glyph
            Pump();
            Assert.Equal(playing ? "❚❚" : "▶", play.Content);
            widthPlaying = widthPlaying == 0 ? play.Bounds.Width : widthPlaying;
            Assert.Equal(widthPlaying, play.Bounds.Width);
            offsetXs.Add(offset.TranslatePoint(new Point(0, 0), window)!.Value.X);
        }

        Assert.Single(offsetXs.Distinct());
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
