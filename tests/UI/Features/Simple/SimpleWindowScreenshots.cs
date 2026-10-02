using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Nikse.SubtitleEdit.Features.Simple;
using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace UITests.Features.Simple;

/// <summary>
/// Renders the README screenshots. Skipped unless SSE_SCREENSHOTS_DIR is set and SSE_DEMO_VIDEO
/// points at a video with a subtitle next to it (scripts/make-screenshots.sh does both).
/// </summary>
public class SimpleWindowScreenshots
{
    private static (string Dir, string Video) Inputs()
    {
        var dir = Environment.GetEnvironmentVariable("SSE_SCREENSHOTS_DIR");
        var video = Environment.GetEnvironmentVariable("SSE_DEMO_VIDEO");
        if (string.IsNullOrEmpty(dir) || string.IsNullOrEmpty(video))
        {
            Assert.Skip("Set SSE_SCREENSHOTS_DIR and SSE_DEMO_VIDEO to render screenshots");
        }

        Directory.CreateDirectory(dir!);
        return (dir!, video!);
    }

    private static void Settle(SimpleWindow window)
    {
        for (var i = 0; i < 5; i++)
        {
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        }
    }

    [AvaloniaFact]
    public async Task Render()
    {
        var (dir, video) = Inputs();
        foreach (var (strings, suffix) in new[] { (SimpleStrings.English, "en"), (SimpleStrings.Portuguese, "pt") })
        {
            SimpleStrings.Current = strings;

            var empty = new SimpleWindow(createPlayer: false, new FakeVideoPlayer()) { Width = 1200, Height = 860 };
            empty.Show();
            Settle(empty);
            empty.CaptureRenderedFrame()!.Save(Path.Combine(dir, $"empty-{suffix}.png"));
            empty.Close();

            var player = new FakeVideoPlayer(90);
            var window = new SimpleWindow(createPlayer: false, player) { Width = 1200, Height = 860 };
            window.Show();
            await window.OpenVideoAsync(video);

            // The waveform loads in the background; wait for it (ffmpeg on a 90 s clip).
            for (var i = 0; i < 300 && window.ViewModel.Peaks == null; i++)
            {
                await Task.Delay(50);
                Dispatcher.UIThread.RunJobs();
            }

            var frame = Path.ChangeExtension(video, ".frame.png");
            if (File.Exists(frame))
            {
                window.ShowStillFrame(new Bitmap(frame));
            }

            player.Position = 13.0;
            window.ViewModel.Tick();
            window.ViewModel.SetOffset(-1.5);
            Settle(window);
            window.CaptureRenderedFrame()!.Save(Path.Combine(dir, $"main-{suffix}.png"));
            window.Close();

            var info = new Nikse.SubtitleEdit.UiLogic.SimpleSync.MkvInfo(1,
            [
                new Nikse.SubtitleEdit.UiLogic.SimpleSync.MkvTrack(2, 3, "subtitles", "HDMV PGS", "eng", "", ""),
                new Nikse.SubtitleEdit.UiLogic.SimpleSync.MkvTrack(13, 14, "subtitles", "HDMV PGS", "por", "", ""),
                new Nikse.SubtitleEdit.UiLogic.SimpleSync.MkvTrack(14, 15, "subtitles", "SubRip/SRT", "por", "pt-BR", "Português (Brasil)"),
            ]);
            var dialog = new SaveChoiceDialog(new SaveRequest(info, "pt-BR", 15, InsideIsDefault: true));
            dialog.Show();
            Settle(window);
            dialog.CaptureRenderedFrame()!.Save(Path.Combine(dir, $"save-{suffix}.png"));
            dialog.Close();
        }
    }
}
