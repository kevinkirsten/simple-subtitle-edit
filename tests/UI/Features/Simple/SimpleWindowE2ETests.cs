using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Features.Simple;
using Nikse.SubtitleEdit.Logic.Media;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace UITests.Features.Simple;

/// <summary>
/// End-to-end tests of the simple sync window: a real window, real controls and real mouse and
/// keyboard input through Avalonia's headless platform. Only the native video player is faked.
/// </summary>
public sealed class SimpleWindowE2ETests : IDisposable
{
    // Subtitle 1.5 s late on purpose: the "speech" in the demo is at 0-2.5 s of every 6 s.
    private const string LateSrt =
        "1\n00:00:01,500 --> 00:00:03,500\nTony, você precisa ver isso.\n\n" +
        "2\n00:00:07,500 --> 00:00:09,500\nAgora não, estou ocupado.\n\n" +
        "3\n00:00:13,500 --> 00:00:15,500\nÉ sobre o seu tio.\n";

    private readonly DirectoryInfo _dir = Directory.CreateTempSubdirectory("sse-e2e-");
    private readonly string _video;

    public SimpleWindowE2ETests()
    {
        SimpleStrings.Current = SimpleStrings.English;
        _video = Path.Combine(_dir.FullName, "Show S01E01.mkv");
        File.WriteAllBytes(_video, [0]); // the fake player never reads it
        File.WriteAllText(Path.Combine(_dir.FullName, "Show S01E01.pt-BR.srt"), LateSrt);
        File.WriteAllText(Path.Combine(_dir.FullName, "unrelated.srt"), LateSrt);
    }

    public void Dispose()
    {
        try
        {
            File.Delete(WavePeakGenerator2.GetPeakWaveFileName(_video));
            _dir.Delete(recursive: true);
        }
        catch
        {
            // best effort temp cleanup
        }
    }

    private async Task<(SimpleWindow Window, FakeVideoPlayer Player)> OpenAsync()
    {
        var player = new FakeVideoPlayer();
        var window = new SimpleWindow(createPlayer: false, player);
        window.Show();
        await window.OpenVideoAsync(_video);
        Pump();
        return (window, player);
    }

    private static void Pump()
    {
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }

    private static T Find<T>(Window window, string automationId) where T : Control =>
        window.GetVisualDescendants().OfType<T>().First(c => AutomationProperties.GetAutomationId(c) == automationId);

    private static void Click(Window window, Control control, Point? at = null)
    {
        var point = control.TranslatePoint(at ?? new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), window)!.Value;
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
        Pump();
    }

    [AvaloniaFact]
    public async Task OpeningAVideo_PicksTheSubtitleNamedLikeIt()
    {
        var (window, player) = await OpenAsync();

        Assert.Equal(_video, player.FileName);
        Assert.Equal("Show S01E01.pt-BR.srt", window.ViewModel.SelectedSource?.DisplayName);
        Assert.Equal(2, window.ViewModel.Sources.Count);
        Assert.NotNull(window.ViewModel.Session);
        Assert.Equal(3, window.ViewModel.Session!.Original.Paragraphs.Count);
        window.Close();
    }

    [AvaloniaFact]
    public async Task OffsetButtons_MoveTheSubtitle_AndResetClearsIt()
    {
        var (window, _) = await OpenAsync();

        Click(window, Find<Button>(window, "OffsetMinusBig"));
        Click(window, Find<Button>(window, "OffsetMinusSmall"));
        Click(window, Find<Button>(window, "OffsetMinusSmall"));
        Click(window, Find<Button>(window, "OffsetMinusSmall"));
        Click(window, Find<Button>(window, "OffsetMinusSmall"));
        Click(window, Find<Button>(window, "OffsetMinusSmall"));

        Assert.Equal("-1.500s", Find<TextBlock>(window, "OffsetValue").Text);
        Assert.True(window.ViewModel.IsDirty);

        Click(window, Find<Button>(window, "OffsetReset"));

        Assert.Equal("+0.000s", Find<TextBlock>(window, "OffsetValue").Text);
        Assert.False(window.ViewModel.IsDirty);
        window.Close();
    }

    [AvaloniaFact]
    public async Task ClickingTheTimeline_SeeksAndShowsTheLineUnderTheVideo()
    {
        var (window, player) = await OpenAsync();
        var timeline = window.Timeline;

        // View is 0-20 s; 2 s is inside the first (late) line.
        var x = timeline.SecondsToX(2.0);
        Click(window, timeline, new Point(x, 30));

        Assert.InRange(player.Position, 1.9, 2.1);
        Assert.Equal("Tony, você precisa ver isso.", Find<TextBlock>(window, "Caption").Text);
        window.Close();
    }

    [AvaloniaFact]
    public async Task DraggingTheTextLane_ChangesTheOffset()
    {
        var (window, _) = await OpenAsync();
        var timeline = window.Timeline;
        var y = timeline.Bounds.Height - 15; // inside the yellow text lane
        var from = timeline.TranslatePoint(new Point(timeline.SecondsToX(3), y), window)!.Value;
        var to = timeline.TranslatePoint(new Point(timeline.SecondsToX(1.5), y), window)!.Value;

        window.MouseDown(from, MouseButton.Left);
        window.MouseMove(new Point((from.X + to.X) / 2, y));
        window.MouseMove(to);
        window.MouseUp(to, MouseButton.Left);
        Pump();

        Assert.InRange(window.ViewModel.Session!.OffsetSeconds, -1.6, -1.4);
        window.Close();
    }

    [AvaloniaFact]
    public async Task DraggingTheAudioLane_ScrollsInsteadOfMovingTheSubtitle()
    {
        var (window, _) = await OpenAsync();
        window.ViewModel.Zoom(0.5, 0); // 10 s view, so there is room to scroll
        Pump();
        var timeline = window.Timeline;
        var from = timeline.TranslatePoint(new Point(400, 40), window)!.Value;
        var to = timeline.TranslatePoint(new Point(200, 40), window)!.Value;

        window.MouseDown(from, MouseButton.Left);
        window.MouseMove(to);
        window.MouseUp(to, MouseButton.Left);
        Pump();

        Assert.True(window.ViewModel.ViewStart > 0);
        Assert.Equal(0, window.ViewModel.Session!.OffsetSeconds);
        window.Close();
    }

    [AvaloniaFact]
    public async Task ZoomButtons_ChangeTheVisibleSpan()
    {
        var (window, _) = await OpenAsync();

        Click(window, Find<Button>(window, "ZoomIn"));
        Assert.Equal(10, window.ViewModel.ViewSeconds);

        Click(window, Find<Button>(window, "ZoomOut"));
        Click(window, Find<Button>(window, "ZoomOut"));
        Assert.Equal(40, window.ViewModel.ViewSeconds);
        window.Close();
    }

    [AvaloniaFact]
    public async Task ClickingTheOverview_CentersTheTimelineThere()
    {
        var (window, _) = await OpenAsync();
        var minimap = window.Minimap;

        Click(window, minimap, new Point(minimap.SecondsToX(60), minimap.Bounds.Height / 2));

        Assert.InRange(window.ViewModel.ViewStart, 49, 51); // 60 s centered in a 20 s view
        window.Close();
    }

    [AvaloniaFact]
    public async Task Keyboard_SpacePlays_PeriodAndCommaNudge()
    {
        var (window, player) = await OpenAsync();
        window.Timeline.Focus();

        window.KeyPress(Key.Space, RawInputModifiers.None, PhysicalKey.Space, " ");
        window.KeyPress(Key.OemPeriod, RawInputModifiers.None, PhysicalKey.Period, ".");
        window.KeyPress(Key.OemPeriod, RawInputModifiers.None, PhysicalKey.Period, ".");
        window.KeyPress(Key.OemComma, RawInputModifiers.Shift, PhysicalKey.Comma, "<");
        Pump();

        Assert.True(player.IsPlaying);
        Assert.Equal(-0.8, window.ViewModel.Session!.OffsetSeconds, 3);
        window.Close();
    }

    [AvaloniaFact]
    public async Task Save_WritesSrtNextToTheVideo_WithTheOffsetApplied()
    {
        var (window, _) = await OpenAsync();
        window.ViewModel.SetOffset(-1.5);

        Click(window, Find<Button>(window, "Save"));

        var output = Path.Combine(_dir.FullName, "Show S01E01.srt");
        Assert.True(File.Exists(output));
        var saved = Subtitle.Parse(output);
        Assert.Equal(0, saved.Paragraphs[0].StartTime.TotalMilliseconds);
        Assert.Equal(6000, saved.Paragraphs[1].StartTime.TotalMilliseconds);
        Assert.Equal("Show S01E01.srt", window.ViewModel.SelectedSource?.DisplayName);
        Assert.Equal(0, window.ViewModel.Session!.OffsetSeconds); // the saved file is the new base
        Assert.Contains("Show S01E01.srt", Find<TextBlock>(window, "Status").Text);
        window.Close();
    }

    [AvaloniaFact]
    public async Task SavingTwice_KeepsTheOldFileAsBackup()
    {
        var (window, _) = await OpenAsync();
        window.ViewModel.SetOffset(-1);
        window.ViewModel.Save();
        window.ViewModel.SetOffset(-0.5);

        window.ViewModel.Save();

        var backup = Path.Combine(_dir.FullName, "Show S01E01.srt.bak");
        Assert.True(File.Exists(backup));
        Assert.Equal(500, Subtitle.Parse(backup).Paragraphs[0].StartTime.TotalMilliseconds);
        Assert.Equal(0, Subtitle.Parse(Path.Combine(_dir.FullName, "Show S01E01.srt")).Paragraphs[0].StartTime.TotalMilliseconds);
        window.Close();
    }

    [AvaloniaFact]
    public async Task DroppingASubtitleFile_AddsAndSelectsIt()
    {
        var (window, _) = await OpenAsync();
        var other = Path.Combine(_dir.FullName, "elsewhere", "dropped.srt");
        Directory.CreateDirectory(Path.GetDirectoryName(other)!);
        File.WriteAllText(other, "1\n00:00:10,000 --> 00:00:11,000\nDropped\n");

        await window.HandleDroppedFilesAsync([other]);
        Pump();

        Assert.Equal("dropped.srt", window.ViewModel.SelectedSource?.DisplayName);
        Assert.Single(window.ViewModel.Session!.Original.Paragraphs);
        window.Close();
    }

    [AvaloniaFact]
    public async Task VideoWithoutSubtitles_SaysSo()
    {
        var player = new FakeVideoPlayer();
        var window = new SimpleWindow(createPlayer: false, player);
        window.Show();
        var lonely = Path.Combine(_dir.FullName, "lonely", "movie.mp4");
        Directory.CreateDirectory(Path.GetDirectoryName(lonely)!);
        File.WriteAllBytes(lonely, [0]);

        await window.OpenVideoAsync(lonely);
        Pump();

        Assert.Null(window.ViewModel.Session);
        Assert.Contains("OTHER FILE", SimpleStrings.English.NoSubtitleFound);
        window.Close();
    }

    [AvaloniaFact]
    public async Task RealVideo_WaveformIsExtractedWithFfmpeg()
    {
        var ffmpeg = WaveformService.FindFfmpeg();
        if (ffmpeg == null)
        {
            Assert.Skip("ffmpeg is not installed");
        }

        var video = Path.Combine(_dir.FullName, "tone.mkv");
        var make = System.Diagnostics.Process.Start(ffmpeg, ["-v", "error", "-y", "-f", "lavfi", "-i", "sine=frequency=440:duration=3", "-c:a", "aac", video]);
        await make.WaitForExitAsync(TestContext.Current.CancellationToken);
        Assert.Equal(0, make.ExitCode);

        try
        {
            var peaks = await WaveformService.LoadAsync(video, TestContext.Current.CancellationToken);

            Assert.NotNull(peaks);
            Assert.InRange(peaks.LengthInSeconds, 2.5, 3.5);
            Assert.True(peaks.HighestPeak > 0);
        }
        finally
        {
            File.Delete(WavePeakGenerator2.GetPeakWaveFileName(video));
        }
    }
}
