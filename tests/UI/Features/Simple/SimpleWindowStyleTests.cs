using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Styling;
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
            var text = button.GetVisualDescendants().OfType<TextBlock>().FirstOrDefault();
            if (button.IsEnabled)
            {
                Assert.True(button.IsPointerOver, label);
                Assert.Equal(((ISolidColorBrush)BrutalTheme.Ink).Color, ((ISolidColorBrush)presenter.Foreground!).Color);
                if (text != null)
                {
                    Assert.True(((ISolidColorBrush)BrutalTheme.Ink).Color == ((ISolidColorBrush)text.Foreground!).Color, $"{label}: label text {((ISolidColorBrush)text.Foreground!).Color} on hover");
                }
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

    /// <summary>What Subtitle Edit's dark theme does at startup: every TextBlock light grey, in the UI font.</summary>
    public static Avalonia.Styling.Styles DarkThemeTextStyle() => new()
    {
        new Avalonia.Styling.Style(x => x.OfType<TextBlock>())
        {
            Setters =
            {
                new Avalonia.Styling.Setter(TextBlock.ForegroundProperty, new SolidColorBrush(Color.Parse("#DDDDDD"))),
                new Avalonia.Styling.Setter(TextBlock.FontFamilyProperty, new FontFamily("Helvetica Neue, Arial, sans-serif")),
            },
        },
    };

    [AvaloniaFact]
    public void WithTheAppsDarkThemeStyle_EveryButtonLabelIsDark_DisabledOnesGrey()
    {
        SimpleStrings.Current = SimpleStrings.English;
        var styles = DarkThemeTextStyle();
        Application.Current!.Styles.Add(styles);
        try
        {
            var window = new SimpleWindow(createPlayer: false, new FakeVideoPlayer()) { Width = 1200, Height = 860 };
            window.Show();
            Pump();
            var buttons = window.GetVisualDescendants().OfType<Button>().Where(b => b.Classes.Contains(BrutalTheme.ButtonClass) && b.IsEffectivelyVisible).ToList();
            var checkedLabels = 0;
            foreach (var button in buttons)
            {
                var text = button.GetVisualDescendants().OfType<TextBlock>().FirstOrDefault();
                if (text == null)
                {
                    continue;
                }

                var expected = button.IsEffectivelyEnabled ? BrutalTheme.Ink : BrutalTheme.Muted;
                var id = Avalonia.Automation.AutomationProperties.GetAutomationId(button);
                Assert.True(((ISolidColorBrush)expected).Color == ((ISolidColorBrush)text.Foreground!).Color, $"{id}: {((ISolidColorBrush)text.Foreground!).Color}");
                Assert.Equal(BrutalTheme.Mono, text.FontFamily);
                checkedLabels++;
            }

            Assert.True(checkedLabels >= 10, $"only {checkedLabels} labels checked");
            AssertReadableOnHover(window);
            window.Close();
        }
        finally
        {
            Application.Current.Styles.Remove(styles);
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
            Assert.Equal(playing ? "Pause" : "Play", Avalonia.Automation.AutomationProperties.GetName(play));
            widthPlaying = widthPlaying == 0 ? play.Bounds.Width : widthPlaying;
            Assert.Equal(widthPlaying, play.Bounds.Width);
            offsetXs.Add(offset.TranslatePoint(new Point(0, 0), window)!.Value.X);
        }

        Assert.Single(offsetXs.Distinct());

        // The icon sits in the middle of the button, both ways.
        var icon = play.GetVisualDescendants().OfType<Avalonia.Controls.Shapes.Path>().Single();
        var iconCenter = icon.TranslatePoint(new Point(icon.Bounds.Width / 2, icon.Bounds.Height / 2), play)!.Value;
        Assert.InRange(iconCenter.Y, play.Bounds.Height / 2 - 1, play.Bounds.Height / 2 + 1);
        Assert.InRange(iconCenter.X, play.Bounds.Width / 2 - 2, play.Bounds.Width / 2 + 2);
        window.Close();
    }

    [AvaloniaFact]
    public void OnlineSettings_LanguageIsADropDown_AndSavesTheOpenSubtitlesCode()
    {
        SimpleStrings.Current = SimpleStrings.English;
        var dialog = new OnlineSettingsDialog(new Nikse.SubtitleEdit.UiLogic.SimpleSync.OpenSubtitlesSettings { ApiKey = "k", Language = "pt-br" });
        dialog.Show();
        Pump();

        var picker = dialog.GetVisualDescendants().OfType<ComboBox>().Single(c => Avalonia.Automation.AutomationProperties.GetAutomationId(c) == "OnlineLanguage");
        Assert.DoesNotContain(dialog.GetVisualDescendants().OfType<TextBox>(), t => Avalonia.Automation.AutomationProperties.GetAutomationId(t) == "OnlineLanguage");
        Assert.Equal("pt-BR", ((Nikse.SubtitleEdit.UiLogic.SimpleSync.SubtitleLanguage)picker.SelectedItem!).Ietf);

        picker.SelectedItem = Nikse.SubtitleEdit.UiLogic.SimpleSync.SubtitleLanguages.All.First(l => l.Ietf == "es-419");
        var save = dialog.GetVisualDescendants().OfType<Button>().Single(b => Avalonia.Automation.AutomationProperties.GetAutomationId(b) == "OnlineSave");
        save.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Pump();

        var result = typeof(OnlineSettingsDialog).GetField("_result", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(dialog);
        Assert.Equal("ea", ((Nikse.SubtitleEdit.UiLogic.SimpleSync.OpenSubtitlesSettings)result!).Language);
    }

    [AvaloniaFact]
    public void LanguagePickers_HaveAnInfoTooltip_AndDarkTextOnHover()
    {
        SimpleStrings.Current = SimpleStrings.English;
        var dialog = new OnlineSettingsDialog(new Nikse.SubtitleEdit.UiLogic.SimpleSync.OpenSubtitlesSettings { Language = "pt-br" });
        dialog.Show();
        Pump();

        var info = dialog.GetVisualDescendants().OfType<Control>().Single(c => Avalonia.Automation.AutomationProperties.GetAutomationId(c) == "OnlineLanguageInfo");
        Assert.Contains("\"ea\"", ((TextBlock)ToolTip.GetTip(info)!).Text);
        Assert.Equal(0, ToolTip.GetShowDelay(info));

        var picker = dialog.GetVisualDescendants().OfType<ComboBox>().Single(c => Avalonia.Automation.AutomationProperties.GetAutomationId(c) == "OnlineLanguage");
        var center = picker.TranslatePoint(new Point(picker.Bounds.Width / 2, picker.Bounds.Height / 2), dialog)!.Value;
        dialog.MouseMove(center);
        Pump();
        Assert.True(picker.IsPointerOver);
        var shown = picker.GetVisualDescendants().OfType<TextBlock>().First(t => t.Text == "Português (Brasil) (pt-BR)");
        Assert.Equal(((ISolidColorBrush)BrutalTheme.Ink).Color, ((ISolidColorBrush)shown.Foreground!).Color);
        dialog.Close();

        var save = new SaveChoiceDialog(new SaveRequest(null, "pt-BR", null, InsideIsDefault: false));
        save.Show();
        Pump();
        var saveInfo = save.GetVisualDescendants().OfType<Control>().Single(c => Avalonia.Automation.AutomationProperties.GetAutomationId(c) == "SaveLanguageInfo");
        Assert.Contains("pt-PT", ((TextBlock)ToolTip.GetTip(saveInfo)!).Text);
        save.Close();
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
