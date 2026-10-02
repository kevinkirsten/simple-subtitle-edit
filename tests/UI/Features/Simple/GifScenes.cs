using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Nikse.SubtitleEdit.Features.Simple;
using Nikse.SubtitleEdit.UiLogic.SimpleSync;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace UITests.Features.Simple;

/// <summary>
/// Records the README GIFs frame by frame (scripts/make-gifs.sh assembles them with ffmpeg).
/// Skipped unless SSE_GIF_DIR (output) and SSE_DEMO_DIR (demo series from the script) are set.
/// Headless rendering has no mouse pointer, popups or dialogs in the window's frame, so the
/// recorder composites those and draws a pointer.
/// </summary>
public class GifScenes
{
    private const int W = 1200;
    private const int H = 820;

    private static (string Out, string Demo) Inputs()
    {
        var output = Environment.GetEnvironmentVariable("SSE_GIF_DIR");
        var demo = Environment.GetEnvironmentVariable("SSE_DEMO_DIR");
        if (string.IsNullOrEmpty(output) || string.IsNullOrEmpty(demo))
        {
            Assert.Skip("Set SSE_GIF_DIR and SSE_DEMO_DIR (scripts/make-gifs.sh)");
        }

        return (output!, demo!);
    }

    /// <summary>Writes numbered frames of one scene; holds a frame for several ticks to pause.</summary>
    private sealed class Recorder(string folder, SimpleWindow window, string framesFolder)
    {
        private int _index;
        public Point Pointer { get; set; } = new(W * 0.5, H * 0.9);
        public bool PointerDown { get; set; }
        public (TopLevel Top, Point At)? Extra { get; set; }

        public void Frame(int hold = 1)
        {
            for (var i = 0; i < 4; i++)
            {
                Dispatcher.UIThread.RunJobs();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            }

            var frame = window.CaptureRenderedFrame()!;
            if (Extra is { } extra)
            {
                Composite(frame, extra.Top, extra.At);
            }

            DrawPointer(frame, Pointer, PointerDown);
            for (var i = 0; i < hold; i++)
            {
                frame.Save(Path.Combine(folder, $"{_index++:D4}.png"));
            }
        }

        /// <summary>Moves the pointer in a straight line, one frame per step.</summary>
        public void MoveTo(Point target, int steps)
        {
            var start = Pointer;
            for (var i = 1; i <= steps; i++)
            {
                var t = Ease(i / (double)steps);
                Pointer = new Point(start.X + (target.X - start.X) * t, start.Y + (target.Y - start.Y) * t);
                Frame();
            }
        }

        public void Click(int holdDown = 2)
        {
            PointerDown = true;
            Frame(holdDown);
            PointerDown = false;
        }

        /// <summary>Video picture at <paramref name="seconds"/> (frames extracted 4 per second by the script).</summary>
        public void ShowVideo(string episode, double seconds)
        {
            var file = Path.Combine(framesFolder, episode, $"{(int)Math.Round(seconds * 4) + 1:D4}.png");
            if (File.Exists(file))
            {
                window.ShowStillFrame(new Bitmap(file));
            }
        }

        private static double Ease(double t) => t < 0.5 ? 2 * t * t : 1 - Math.Pow(-2 * t + 2, 2) / 2;
    }

    private static Point Center(Control c, Visual relativeTo) =>
        c.TranslatePoint(new Point(c.Bounds.Width / 2, c.Bounds.Height / 2), relativeTo)!.Value;

    private static T Find<T>(Visual root, string id) where T : Control =>
        root.GetVisualDescendants().OfType<T>().First(c => AutomationProperties.GetAutomationId(c) == id);

    // --- pixel helpers --------------------------------------------------------------------

    private static void Composite(WriteableBitmap target, TopLevel top, Point at)
    {
        for (var i = 0; i < 3; i++)
        {
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        }

        using var source = top.CaptureRenderedFrame()!;
        using var src = source.Lock();
        using var dst = target.Lock();
        var srcBytes = new byte[src.RowBytes * src.Size.Height];
        Marshal.Copy(src.Address, srcBytes, 0, srcBytes.Length);
        var dstBytes = new byte[dst.RowBytes * dst.Size.Height];
        Marshal.Copy(dst.Address, dstBytes, 0, dstBytes.Length);

        int ox = (int)at.X, oy = (int)at.Y;
        // Drop shadow, then the window itself with a 2 px frame.
        for (var y = -2; y < src.Size.Height + 8; y++)
        {
            for (var x = -2; x < src.Size.Width + 8; x++)
            {
                var tx = ox + x;
                var ty = oy + y;
                if (tx < 0 || ty < 0 || tx >= dst.Size.Width || ty >= dst.Size.Height)
                {
                    continue;
                }

                var d = ty * dst.RowBytes + tx * 4;
                var inside = x >= 0 && y >= 0 && x < src.Size.Width && y < src.Size.Height;
                if (inside)
                {
                    var s = y * src.RowBytes + x * 4;
                    var frame = x < 2 || y < 2 || x >= src.Size.Width - 2 || y >= src.Size.Height - 2;
                    for (var c = 0; c < 3; c++)
                    {
                        dstBytes[d + c] = frame ? (byte)17 : srcBytes[s + c];
                    }
                }
                else if (x >= 6 && y >= 6)
                {
                    for (var c = 0; c < 3; c++)
                    {
                        dstBytes[d + c] = (byte)(dstBytes[d + c] * 0.6);
                    }
                }
            }
        }

        Marshal.Copy(dstBytes, 0, dst.Address, dstBytes.Length);
    }

    /// <summary>A classic arrow pointer: black outline, white fill (yellow while pressed).</summary>
    private static void DrawPointer(WriteableBitmap target, Point tip, bool pressed)
    {
        Point[] arrow = [new(0, 0), new(0, 26), new(7, 19), new(12, 30), new(17, 28), new(12, 17), new(21, 17)];
        using var dst = target.Lock();
        var bytes = new byte[dst.RowBytes * dst.Size.Height];
        Marshal.Copy(dst.Address, bytes, 0, bytes.Length);
        for (var y = -2; y <= 33; y++)
        {
            for (var x = -2; x <= 24; x++)
            {
                var inside = InPolygon(arrow, x + 0.5, y + 0.5);
                var outline = !inside && Near(arrow, x + 0.5, y + 0.5, 1.6);
                if (!inside && !outline)
                {
                    continue;
                }

                var px = (int)tip.X + x;
                var py = (int)tip.Y + y;
                if (px < 0 || py < 0 || px >= dst.Size.Width || py >= dst.Size.Height)
                {
                    continue;
                }

                var i = py * dst.RowBytes + px * 4;
                // BGRA
                (bytes[i], bytes[i + 1], bytes[i + 2]) = outline ? ((byte)17, (byte)17, (byte)17) : pressed ? ((byte)0x6E, (byte)0xE3, (byte)0xFF) : ((byte)255, (byte)255, (byte)255);
                bytes[i + 3] = 255;
            }
        }

        Marshal.Copy(bytes, 0, dst.Address, bytes.Length);
    }

    private static bool InPolygon(Point[] p, double x, double y)
    {
        var inside = false;
        for (int i = 0, j = p.Length - 1; i < p.Length; j = i++)
        {
            if ((p[i].Y > y) != (p[j].Y > y) && x < (p[j].X - p[i].X) * (y - p[i].Y) / (p[j].Y - p[i].Y) + p[i].X)
            {
                inside = !inside;
            }
        }

        return inside;
    }

    private static bool Near(Point[] p, double x, double y, double distance)
    {
        for (int i = 0, j = p.Length - 1; i < p.Length; j = i++)
        {
            var (ax, ay, bx, by) = (p[j].X, p[j].Y, p[i].X, p[i].Y);
            var len = (bx - ax) * (bx - ax) + (by - ay) * (by - ay);
            var t = Math.Clamp(((x - ax) * (bx - ax) + (y - ay) * (by - ay)) / len, 0, 1);
            var dx = ax + t * (bx - ax) - x;
            var dy = ay + t * (by - ay) - y;
            if (dx * dx + dy * dy <= distance * distance)
            {
                return true;
            }
        }

        return false;
    }

    // --- scenes ------------------------------------------------------------------------------

    private static async Task<(SimpleWindow Window, FakeVideoPlayer Player)> OpenAsync(string video)
    {
        var player = new FakeVideoPlayer(60);
        var window = new SimpleWindow(createPlayer: false, player) { Width = W, Height = H };
        window.Show();
        await window.OpenVideoAsync(video);
        for (var i = 0; i < 400 && window.ViewModel.Peaks == null; i++)
        {
            await Task.Delay(25);
            Dispatcher.UIThread.RunJobs();
        }

        return (window, player);
    }

    private static string Scene(string output, string name)
    {
        var dir = Path.Combine(output, name);
        Directory.CreateDirectory(dir);
        foreach (var f in Directory.GetFiles(dir, "*.png"))
        {
            File.Delete(f);
        }

        return dir;
    }

    [AvaloniaFact]
    public async Task Record()
    {
        var (output, demo) = Inputs();
        SimpleStrings.Current = SimpleStrings.English;
        var series = Path.Combine(demo, "Demo Show");
        var ep1 = Path.Combine(series, "Season 01", "Demo Show - S01E01.mkv");
        var frames = Path.Combine(demo, "frames");

        // 1. Sync: the subtitle is 1.5 s late; drag the yellow lane onto the speech, play.
        {
            var (window, player) = await OpenAsync(ep1);
            var rec = new Recorder(Scene(output, "sync"), window, frames);
            player.Position = 12.4;
            window.ViewModel.Tick();
            rec.ShowVideo("ep1", 12.4);
            var timeline = window.Timeline;
            var y = timeline.Bounds.Height - 30;
            var from = timeline.TranslatePoint(new Point(timeline.SecondsToX(14.2), y), window)!.Value;
            var to = timeline.TranslatePoint(new Point(timeline.SecondsToX(12.7), y), window)!.Value;
            rec.Frame(8);
            rec.MoveTo(from, 14);
            rec.PointerDown = true;
            rec.Frame(2);
            for (var i = 1; i <= 22; i++)
            {
                var x = from.X + (to.X - from.X) * i / 22.0;
                window.ViewModel.SetOffset((x - from.X) * timeline.SecondsPerPixel);
                rec.Pointer = new Point(x, from.Y);
                rec.Frame();
            }

            window.ViewModel.SetOffset(-1.5);
            rec.PointerDown = false;
            rec.Frame(6);
            rec.MoveTo(Center(Find<Button>(window, "PlayPause"), window), 12);
            rec.Click();
            player.Play();
            for (var t = 12.4; t <= 15.4; t += 1 / 12.0)
            {
                player.Position = t;
                window.ViewModel.Tick();
                rec.ShowVideo("ep1", t);
                rec.Frame();
            }

            player.Pause();
            rec.Frame(10);
            window.Close();
        }

        // 2. A whole series: drop the folder, NEXT walks the episodes, each with its subtitle.
        {
            var player = new FakeVideoPlayer(60);
            var window = new SimpleWindow(createPlayer: false, player) { Width = W, Height = H };
            window.Show();
            var rec = new Recorder(Scene(output, "folder"), window, frames);
            rec.Frame(10);
            await window.HandleDroppedFilesAsync([series]);
            for (var i = 0; i < 400 && window.ViewModel.Peaks == null; i++)
            {
                await Task.Delay(25);
                Dispatcher.UIThread.RunJobs();
            }

            player.Position = 1.6;
            window.ViewModel.Tick();
            rec.ShowVideo("ep1", 1.6);
            rec.Frame(14);
            for (var episode = 2; episode <= 3; episode++)
            {
                rec.MoveTo(Center(Find<Button>(window, "NextVideo"), window), 14);
                rec.Click();
                await window.ViewModel.GoNextAsync();
                for (var i = 0; i < 400 && window.ViewModel.Peaks == null; i++)
                {
                    await Task.Delay(25);
                    Dispatcher.UIThread.RunJobs();
                }

                player.Position = 1.6;
                window.ViewModel.Tick();
                rec.ShowVideo($"ep{episode}", 1.6);
                rec.Frame(16);
            }

            window.Close();
        }

        // 3. FIND ONLINE (OpenSubtitles replaced by a fake server): pick an ONLINE subtitle.
        {
            var (window, player) = await OpenAsync(ep1);
            window.ViewModel.OnlineSettings = new OpenSubtitlesSettings { ApiKey = "demo", Username = "demo", Password = "demo" };
            window.ViewModel.OnlineClientFactory = s => new OpenSubtitlesClient(new HttpClient(new FakeOpenSubtitles(Path.Combine(demo, "online.srt"))), s, Path.Combine(output, "cache"));
            var rec = new Recorder(Scene(output, "online"), window, frames);
            player.Position = 13.2;
            window.ViewModel.Tick();
            rec.ShowVideo("ep1", 13.2);
            rec.Frame(8);
            rec.MoveTo(Center(Find<Button>(window, "FindOnline"), window), 14);
            rec.Click();
            await window.FindOnlineAsync();
            rec.Frame(10);

            var combo = window.GetVisualDescendants().OfType<ComboBox>().Single();
            rec.MoveTo(Center(combo, window), 14);
            rec.Click();
            combo.IsDropDownOpen = true;
            Dispatcher.UIThread.RunJobs();
            // Headless popups may be drawn inside the window (overlay) or in a top level of their own.
            var popupTop = TopLevel.GetTopLevel(combo.GetLogicalDescendants().OfType<ComboBoxItem>().First())!;
            var comboAt = combo.TranslatePoint(new Point(0, 0), window)!.Value;
            var popupAt = ReferenceEquals(popupTop, window) ? new Point(0, 0) : new Point(comboAt.X, comboAt.Y - popupTop.Bounds.Height - 4);
            rec.Extra = ReferenceEquals(popupTop, window) ? null : (popupTop, popupAt);
            rec.Frame(8);

            var onlineItem = combo.GetLogicalDescendants().OfType<ComboBoxItem>().First(i => (i.Content as SubtitleSource)?.Kind == SubtitleSourceKind.Online);
            var itemAt = onlineItem.TranslatePoint(new Point(onlineItem.Bounds.Width * 0.3, onlineItem.Bounds.Height / 2), popupTop)!.Value;
            rec.MoveTo(new Point(popupAt.X + itemAt.X, popupAt.Y + itemAt.Y), 16);
            popupTop.MouseMove(itemAt);
            rec.Frame(4);
            rec.Click();
            combo.IsDropDownOpen = false;
            rec.Extra = null;
            window.ViewModel.SelectedSource = (SubtitleSource)onlineItem.Content!;
            for (var i = 0; i < 100 && window.ViewModel.Session?.Original.Paragraphs.FirstOrDefault()?.Text.Contains("online", StringComparison.OrdinalIgnoreCase) != true; i++)
            {
                await Task.Delay(20);
                Dispatcher.UIThread.RunJobs();
            }

            window.ViewModel.Tick();
            rec.Frame(20);
            window.Close();
        }

        // 4. SAVE inside the video: language, what is already inside, real-looking progress.
        {
            var (window, player) = await OpenAsync(ep1);
            var rec = new Recorder(Scene(output, "save"), window, frames);
            player.Position = 13.2;
            window.ViewModel.Tick();
            window.ViewModel.SetOffset(-1.5);
            rec.ShowVideo("ep1", 13.2);
            rec.Frame(8);
            rec.MoveTo(Center(Find<Button>(window, "Save"), window), 14);
            rec.Click();

            var info = new MkvInfo(1,
            [
                new MkvTrack(2, 3, "subtitles", "HDMV PGS", "eng", "", ""),
                new MkvTrack(13, 14, "subtitles", "HDMV PGS", "por", "", ""),
            ]);
            var dialog = new SaveChoiceDialog(new SaveRequest(info, "pt-BR", null, InsideIsDefault: false));
            dialog.Show();
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            var dialogAt = new Point((W - dialog.Bounds.Width) / 2, (H - dialog.Bounds.Height) / 2);
            rec.Extra = (dialog, dialogAt);
            rec.Frame(16);
            var inside = Find<Button>(dialog, "SaveInsideVideo");
            var insideAt = Center(inside, dialog);
            rec.MoveTo(new Point(dialogAt.X + insideAt.X, dialogAt.Y + insideAt.Y), 16);
            dialog.MouseMove(insideAt);
            rec.Frame(4);
            rec.Click();
            rec.Extra = null;
            dialog.Close();

            // The real save (mkvmerge on the demo episode). It is too quick on a 40 s clip to film,
            // so the progress the overlay shows for a full episode is played first.
            window.ViewModel.BusyText = SimpleStrings.Current.Embedding;
            window.ViewModel.IsIdle = false;
            for (var p = 0; p <= 100; p += 4)
            {
                window.ViewModel.BusyPercent = p;
                rec.Frame();
            }

            window.ViewModel.IsIdle = true;
            window.ViewModel.PlexRefresh = _ => Task.FromResult(true);
            Assert.True(await window.ViewModel.SaveInsideVideoAsync("pt-BR"));
            window.ViewModel.Tick();
            rec.Pointer = new Point(W * 0.4, H * 0.95);
            rec.Frame(30);
            window.Close();
        }
    }

    private sealed class FakeOpenSubtitles(string downloadFile) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r, CancellationToken cancellationToken)
        {
            static HttpResponseMessage Json(string json) => new(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
            var byHash = r.RequestUri!.Query.Contains("moviehash=", StringComparison.Ordinal);
            return Task.FromResult(r.RequestUri!.AbsolutePath switch
            {
                // The exact-file match comes only from the hash search, like the real service.
                "/api/v1/subtitles" when byHash => Json("""{"data":[{"attributes":{"release":"Demo.Show.S01E01.1080p.WEB-DL","language":"pt-BR","download_count":10233,"files":[{"file_id":901}]}}]}"""),
                "/api/v1/subtitles" => Json("""
                    {"data":[
                      {"attributes":{"release":"Demo.Show.S01E01.1080p.WEB-DL","language":"pt-BR","download_count":10233,"moviehash_match":true,"files":[{"file_id":901}]}},
                      {"attributes":{"release":"Demo.Show.S01E01.720p.HDTV","language":"pt-BR","download_count":4112,"files":[{"file_id":902}]}},
                      {"attributes":{"release":"Demo.Show.S01E01.DVDRip","language":"pt-BR","download_count":988,"files":[{"file_id":903}]}}
                    ]}
                    """),
                "/api/v1/login" => Json("""{"token":"t"}"""),
                "/api/v1/download" => Json("""{"link":"https://dl.example/s.srt","remaining":996}"""),
                "/s.srt" => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(File.ReadAllText(downloadFile)) },
                _ => new HttpResponseMessage(HttpStatusCode.NotFound),
            });
        }
    }
}
