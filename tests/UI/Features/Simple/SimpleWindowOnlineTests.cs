using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
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
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace UITests.Features.Simple;

/// <summary>FIND ONLINE end to end, with OpenSubtitles replaced by a fake server.</summary>
public sealed class SimpleWindowOnlineTests : IDisposable
{
    private readonly DirectoryInfo _dir = Directory.CreateTempSubdirectory("sse-online-");
    private readonly string _video;
    private readonly FakeOpenSubtitles _server = new();

    public SimpleWindowOnlineTests()
    {
        SimpleStrings.Current = SimpleStrings.English;
        _video = Path.Combine(_dir.FullName, "The Sopranos (1999) - S01E01 - Pilot.mkv");
        File.WriteAllBytes(_video, new byte[200_000]);
        File.WriteAllText(Path.ChangeExtension(_video, ".srt"), "1\n00:00:01,000 --> 00:00:02,000\nLocal\n");
    }

    public void Dispose() => _dir.Delete(recursive: true);

    private sealed class FakeOpenSubtitles : HttpMessageHandler
    {
        public int Downloads { get; private set; }
        public List<string> Paths { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Paths.Add(request.RequestUri!.AbsolutePath);
            static HttpResponseMessage Json(string json) => new(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
            return Task.FromResult(request.RequestUri.AbsolutePath switch
            {
                "/api/v1/subtitles" => Json("""
                    {"data":[
                      {"attributes":{"release":"Sopranos.S01E01.x265-ImE","language":"pt-BR","download_count":1033,"fps":23.976,"files":[{"file_id":111}]}},
                      {"attributes":{"release":"Sopranos.S01E01.WEB","language":"pt-BR","download_count":50,"fps":25,"files":[{"file_id":222}]}}
                    ]}
                    """),
                "/api/v1/login" => Json("""{"token":"JWT"}"""),
                "/api/v1/download" => CountDownload(Json("""{"link":"https://dl.example/sub.srt","remaining":998}""")),
                "/sub.srt" => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("1\n00:00:05,000 --> 00:00:06,000\nDa internet\n") },
                _ => new HttpResponseMessage(HttpStatusCode.NotFound),
            });
        }

        private HttpResponseMessage CountDownload(HttpResponseMessage response)
        {
            Downloads++;
            return response;
        }
    }

    private static void Pump()
    {
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }

    private static async Task SettleAsync()
    {
        for (var i = 0; i < 20; i++)
        {
            await Task.Delay(10);
            Pump();
        }
    }

    private async Task<SimpleWindow> OpenAsync(OpenSubtitlesSettings settings)
    {
        var window = new SimpleWindow(createPlayer: false, new FakeVideoPlayer());
        window.ViewModel.OnlineClientFactory = s => new OpenSubtitlesClient(new HttpClient(_server), s, Path.Combine(_dir.FullName, "cache"));
        window.ViewModel.OnlineSettings = settings;
        window.Show();
        await window.OpenVideoAsync(_video);
        Pump();
        return window;
    }

    private static readonly OpenSubtitlesSettings Full = new() { ApiKey = "KEY", Username = "u", Password = "p", AppName = "sonarr" };

    [AvaloniaFact]
    public async Task FindOnline_AddsOnlineEntries_WithoutDownloadingAnything()
    {
        var window = await OpenAsync(Full);

        await window.FindOnlineAsync();
        Pump();

        var online = window.ViewModel.Sources.Where(s => s.Kind == SubtitleSourceKind.Online).ToList();
        Assert.Equal(2, online.Count);
        Assert.StartsWith("pt-BR · Sopranos.S01E01.x265-ImE", online[0].DisplayName);
        Assert.Equal(0, _server.Downloads);
        Assert.Equal("Local", window.ViewModel.Session!.Original.Paragraphs[0].Text); // local stays selected
        Assert.Contains("2 subtitles online", window.ViewModel.StatusText);
        window.Close();
    }

    [AvaloniaFact]
    public async Task PickingAnOnlineEntry_DownloadsOnce_AndLoadsIt()
    {
        var window = await OpenAsync(Full);
        await window.FindOnlineAsync();
        var first = window.ViewModel.Sources.First(s => s.Kind == SubtitleSourceKind.Online);

        window.ViewModel.SelectedSource = first;
        await SettleAsync();

        Assert.Equal("Da internet", window.ViewModel.Session!.Original.Paragraphs[0].Text);
        Assert.Equal(1, _server.Downloads);
        Assert.Contains("998", window.ViewModel.StatusText);

        // Back to local and again to the same online one: served from cache.
        window.ViewModel.SelectedSource = window.ViewModel.Sources.First(s => s.Kind == SubtitleSourceKind.File);
        window.ViewModel.SelectedSource = first;
        await SettleAsync();
        Assert.Equal(1, _server.Downloads);
        window.Close();
    }

    [AvaloniaFact]
    public async Task OnlineSubtitle_SavedWithOffset_GoesNextToTheVideo()
    {
        var window = await OpenAsync(Full);
        await window.FindOnlineAsync();
        window.ViewModel.SelectedSource = window.ViewModel.Sources.First(s => s.Kind == SubtitleSourceKind.Online);
        await SettleAsync();

        window.ViewModel.SetOffset(-1);
        window.ViewModel.Save();

        var saved = Nikse.SubtitleEdit.Core.Common.Subtitle.Parse(Path.ChangeExtension(_video, ".srt"));
        Assert.Equal("Da internet", saved.Paragraphs[0].Text);
        Assert.Equal(4000, saved.Paragraphs[0].StartTime.TotalMilliseconds);
        Assert.True(File.Exists(Path.ChangeExtension(_video, ".srt") + ".bak")); // the local one, kept
        window.Close();
    }

    [AvaloniaFact]
    public async Task FindOnline_WithoutApiKey_AsksForIt_AndCancelDoesNothing()
    {
        var window = await OpenAsync(new OpenSubtitlesSettings());
        var asked = 0;
        window.AskOnlineSettings = _ => { asked++; return Task.FromResult<OpenSubtitlesSettings?>(null); };

        await window.FindOnlineAsync();

        Assert.Equal(1, asked);
        Assert.Empty(_server.Paths);
        window.Close();
    }

    [AvaloniaFact]
    public async Task DownloadWithoutLogin_ShowsTheProblem()
    {
        var window = await OpenAsync(Full with { Password = "" });
        await window.FindOnlineAsync();

        window.ViewModel.SelectedSource = window.ViewModel.Sources.First(s => s.Kind == SubtitleSourceKind.Online);
        await SettleAsync();

        Assert.Contains("username and password", window.ViewModel.StatusText);
        Assert.Equal(0, _server.Downloads);
        window.Close();
    }
}
