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

/// <summary>Opening a series folder and walking its episodes with PREV / NEXT.</summary>
public sealed class SimpleWindowPlaylistTests : IDisposable
{
    private readonly DirectoryInfo _root = Directory.CreateTempSubdirectory("sse-series-");

    public SimpleWindowPlaylistTests()
    {
        SimpleStrings.Current = SimpleStrings.English;
        foreach (var (season, episode) in new[] { (1, 1), (1, 2), (2, 1) })
        {
            var folder = Path.Combine(_root.FullName, $"Season 0{season}");
            Directory.CreateDirectory(folder);
            var name = $"Show - S0{season}E0{episode}";
            File.WriteAllBytes(Path.Combine(folder, name + ".mkv"), [0]);
            File.WriteAllText(Path.Combine(folder, name + ".srt"), $"1\n00:00:01,000 --> 00:00:02,000\n{name}\n");
        }
    }

    public void Dispose() => _root.Delete(recursive: true);

    private static void Pump()
    {
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }

    private static T Find<T>(Window window, string id) where T : Control =>
        window.GetVisualDescendants().OfType<T>().First(c => AutomationProperties.GetAutomationId(c) == id);

    private static async Task ClickAsync(Window window, Control control)
    {
        var point = control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), window)!.Value;
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
        // Saving on the way out reads the mkv's tracks (a short external process): settle.
        for (var i = 0; i < 30; i++)
        {
            await Task.Delay(10);
            Pump();
        }
    }

    private async Task<(SimpleWindow, FakeVideoPlayer)> OpenFolderAsync()
    {
        var player = new FakeVideoPlayer();
        var window = new SimpleWindow(createPlayer: false, player)
        {
            AskSaveDestination = _ => Task.FromResult(new SaveChoice(SaveDestination.SideFile, "pt-BR")),
        };
        window.Show();
        await window.HandleDroppedFilesAsync([_root.FullName]);
        Pump();
        return (window, player);
    }

    [AvaloniaFact]
    public async Task DroppingAFolder_OpensTheFirstEpisode_WithItsSubtitle()
    {
        var (window, player) = await OpenFolderAsync();

        Assert.EndsWith("Show - S01E01.mkv", player.FileName);
        Assert.Equal("1/3 · " + Path.Combine("Season 01", "Show - S01E01.mkv"), Find<TextBlock>(window, "PlaylistText").Text);
        Assert.Equal("Show - S01E01", window.ViewModel.Session!.Original.Paragraphs[0].Text);
        Assert.False(Find<Button>(window, "PreviousVideo").IsEnabled);
        window.Close();
    }

    [AvaloniaFact]
    public async Task Next_CrossesIntoTheNextSeason_AndLoadsThatSubtitle()
    {
        var (window, player) = await OpenFolderAsync();

        await ClickAsync(window, Find<Button>(window, "NextVideo"));
        await ClickAsync(window, Find<Button>(window, "NextVideo"));

        Assert.EndsWith("Show - S02E01.mkv", player.FileName);
        Assert.Equal("Show - S02E01", window.ViewModel.Session!.Original.Paragraphs[0].Text);
        Assert.False(Find<Button>(window, "NextVideo").IsEnabled);

        await ClickAsync(window, Find<Button>(window, "PreviousVideo"));
        Assert.EndsWith("Show - S01E02.mkv", player.FileName);
        window.Close();
    }

    [AvaloniaFact]
    public async Task UnsavedOffset_Cancel_StaysOnTheEpisode()
    {
        var (window, player) = await OpenFolderAsync();
        var asked = 0;
        window.AskLeave = () => { asked++; return Task.FromResult(LeaveChoice.Cancel); };
        window.ViewModel.Nudge(0.5);

        await ClickAsync(window, Find<Button>(window, "NextVideo"));

        Assert.Equal(1, asked);
        Assert.EndsWith("Show - S01E01.mkv", player.FileName);
        Assert.Equal(0.5, window.ViewModel.Session!.OffsetSeconds);
        window.Close();
    }

    [AvaloniaFact]
    public async Task UnsavedOffset_Save_WritesThenMovesOn()
    {
        var (window, player) = await OpenFolderAsync();
        window.AskLeave = () => Task.FromResult(LeaveChoice.Save);
        window.ViewModel.Nudge(1);

        await ClickAsync(window, Find<Button>(window, "NextVideo"));

        Assert.EndsWith("Show - S01E02.mkv", player.FileName);
        var saved = Nikse.SubtitleEdit.Core.Common.Subtitle.Parse(Path.Combine(_root.FullName, "Season 01", "Show - S01E01.srt"));
        Assert.Equal(2000, saved.Paragraphs[0].StartTime.TotalMilliseconds);
        Assert.True(File.Exists(Path.Combine(_root.FullName, "Season 01", "Show - S01E01.srt.bak")));
        window.Close();
    }

    [AvaloniaFact]
    public async Task UnsavedOffset_Discard_MovesOnWithoutWriting()
    {
        var (window, player) = await OpenFolderAsync();
        window.AskLeave = () => Task.FromResult(LeaveChoice.Discard);
        window.ViewModel.Nudge(1);

        await ClickAsync(window, Find<Button>(window, "NextVideo"));

        Assert.EndsWith("Show - S01E02.mkv", player.FileName);
        Assert.False(File.Exists(Path.Combine(_root.FullName, "Season 01", "Show - S01E01.srt.bak")));
        window.Close();
    }

    [AvaloniaFact]
    public async Task PageDown_GoesToTheNextEpisode()
    {
        var (window, player) = await OpenFolderAsync();
        window.Timeline.Focus();

        window.KeyPress(Key.PageDown, RawInputModifiers.None, PhysicalKey.PageDown, null);
        for (var i = 0; i < 5; i++)
        {
            await Task.Yield();
            Pump();
        }

        Assert.EndsWith("Show - S01E02.mkv", player.FileName);
        window.Close();
    }

    [AvaloniaFact]
    public async Task OpeningOneVideo_NextWalksItsFolder()
    {
        var player = new FakeVideoPlayer();
        var window = new SimpleWindow(createPlayer: false, player);
        window.Show();
        await window.OpenVideoAsync(Path.Combine(_root.FullName, "Season 01", "Show - S01E01.mkv"));
        Pump();

        await ClickAsync(window, Find<Button>(window, "NextVideo"));

        Assert.EndsWith("Show - S01E02.mkv", player.FileName);
        Assert.False(Find<Button>(window, "NextVideo").IsEnabled); // the folder of a single video, not the whole series
        window.Close();
    }
}
