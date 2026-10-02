using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Nikse.SubtitleEdit.Features.Simple;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace UITests.Features.Simple;

/// <summary>SPACE must toggle play/pause exactly once, wherever the keyboard focus is.</summary>
public sealed class SimpleWindowSpaceKeyTests : IDisposable
{
    private readonly DirectoryInfo _dir = Directory.CreateTempSubdirectory("sse-space-");

    public void Dispose() => _dir.Delete(recursive: true);

    private static void Pump()
    {
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }

    private async Task<(SimpleWindow, FakeVideoPlayer)> OpenAsync()
    {
        SimpleStrings.Current = SimpleStrings.English;
        var video = Path.Combine(_dir.FullName, "ep.mkv");
        File.WriteAllBytes(video, [0]);
        File.WriteAllText(Path.Combine(_dir.FullName, "ep.srt"), "1\n00:00:01,000 --> 00:00:02,000\nA\n");
        File.WriteAllText(Path.Combine(_dir.FullName, "ep.en.srt"), "1\n00:00:01,000 --> 00:00:02,000\nB\n");
        var player = new FakeVideoPlayer();
        var window = new SimpleWindow(createPlayer: false, player) { Width = 1200, Height = 860 };
        window.Show();
        await window.OpenVideoAsync(video);
        await window.ViewModel.WaveformLoading;
        Pump();
        return (window, player);
    }

    private static T Find<T>(Window w, string id) where T : Control =>
        w.GetVisualDescendants().OfType<T>().First(c => AutomationProperties.GetAutomationId(c) == id);

    private static void Click(Window window, Control control)
    {
        var p = control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), window)!.Value;
        window.MouseDown(p, MouseButton.Left);
        window.MouseUp(p, MouseButton.Left);
        Pump();
    }

    private static void Space(Window window)
    {
        window.KeyPress(Key.Space, RawInputModifiers.None, PhysicalKey.Space, " ");
        window.KeyRelease(Key.Space, RawInputModifiers.None, PhysicalKey.Space, " ");
        Pump();
    }

    [AvaloniaTheory]
    [InlineData("PlayPause")]
    [InlineData("OffsetPlusSmall")]
    [InlineData("ZoomIn")]
    [InlineData("Save")]
    [InlineData("FindOnline")]
    public async Task AfterClickingAButton_SpaceTogglesOnce_AndDoesNotPressTheButtonAgain(string buttonId)
    {
        var (window, player) = await OpenAsync();
        window.AskSaveDestination = _ => Task.FromResult(new SaveChoice(SaveDestination.Cancel, "pt-BR"));
        window.AskOnlineSettings = _ => Task.FromResult<Nikse.SubtitleEdit.UiLogic.SimpleSync.OpenSubtitlesSettings?>(null);
        var button = Find<Button>(window, buttonId);
        Click(window, button);
        var playingAfterClick = player.IsPlaying;
        var offsetAfterClick = window.ViewModel.Session!.OffsetSeconds;
        var viewAfterClick = window.ViewModel.ViewSeconds;

        Space(window);
        Assert.Equal(!playingAfterClick, player.IsPlaying);
        Space(window);
        Assert.Equal(playingAfterClick, player.IsPlaying);

        Assert.Equal(offsetAfterClick, window.ViewModel.Session!.OffsetSeconds); // the focused button was not "clicked" by SPACE
        Assert.Equal(viewAfterClick, window.ViewModel.ViewSeconds);
        window.Close();
    }

    [AvaloniaFact]
    public async Task AfterPickingASubtitle_SpaceTogglesPlayback_InsteadOfOpeningThePicker()
    {
        var (window, player) = await OpenAsync();
        var combo = window.GetVisualDescendants().OfType<ComboBox>().Single();
        combo.Focus();
        window.ViewModel.SelectedSource = window.ViewModel.Sources[1];
        Pump();

        Space(window);

        Assert.True(player.IsPlaying);
        Assert.False(combo.IsDropDownOpen);
        window.Close();
    }

    [AvaloniaFact]
    public async Task AfterClickingTheTimeline_SpaceToggles()
    {
        var (window, player) = await OpenAsync();
        Click(window, window.Timeline);

        Space(window);
        Assert.True(player.IsPlaying);
        Space(window);
        Assert.False(player.IsPlaying);
        window.Close();
    }
}
