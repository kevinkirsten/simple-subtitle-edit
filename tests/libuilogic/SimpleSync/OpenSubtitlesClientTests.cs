using System.Net;
using System.Text;
using Nikse.SubtitleEdit.UiLogic.SimpleSync;

namespace LibUiLogicTests.SimpleSync;

/// <summary>OpenSubtitles client against a fake HTTP server: never touches the real API or the user's quota.</summary>
public class OpenSubtitlesClientTests : IDisposable
{
    private readonly DirectoryInfo _cache = Directory.CreateTempSubdirectory("sse-os-");

    public void Dispose() => _cache.Delete(recursive: true);

    private sealed class FakeServer : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];
        public List<string> Bodies { get; } = [];
        public Func<HttpRequestMessage, HttpResponseMessage> Respond { get; set; } = _ => new HttpResponseMessage(HttpStatusCode.NotFound);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            Bodies.Add(request.Content == null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken));
            return Respond(request);
        }
    }

    private static HttpResponseMessage Json(string json, HttpStatusCode code = HttpStatusCode.OK) =>
        new(code) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private static string Result(int fileId, string release, int downloads, bool ai = false) =>
        $$$"""{"attributes":{"release":"{{{release}}}","language":"pt-BR","download_count":{{{downloads}}},"fps":23.976,"ai_translated":{{{ai.ToString().ToLowerInvariant()}}},"hearing_impaired":false,"moviehash_match":false,"uploader":{"name":"x"},"files":[{"file_id":{{{fileId}}},"file_name":"f.srt"}]}}""";

    private static readonly OpenSubtitlesSettings Settings = new()
    {
        ApiKey = "KEY",
        Username = "user",
        Password = "pass",
        AppName = "sonarr",
        Language = "pt-BR",
    };

    [Fact]
    public async Task Search_MergesHashAndTitleResults_HashFirstThenMostDownloaded()
    {
        var server = new FakeServer
        {
            Respond = r => r.RequestUri!.Query.Contains("moviehash=")
                ? Json($$"""{"data":[{{Result(1, "hash-release", 5)}}]}""")
                : Json($$"""{"data":[{{Result(1, "hash-release", 5)}},{{Result(2, "popular", 900)}},{{Result(3, "ai", 5000, ai: true)}},{{Result(4, "less", 10)}}]}"""),
        };
        var client = new OpenSubtitlesClient(new HttpClient(server), Settings, _cache.FullName);

        var results = await client.SearchAsync(new VideoQuery("x.mkv", "8e245d9679d31e12", "The Sopranos", 1999, 1, 1), CancellationToken.None);

        Assert.Equal([1, 2, 4, 3], results.Select(r => r.FileId));
        Assert.True(results[0].HashMatch);
        Assert.False(results[1].HashMatch);
    }

    [Fact]
    public async Task Search_SendsKeyUserAgentAndSortedLowercaseQuery()
    {
        var server = new FakeServer { Respond = _ => Json("""{"data":[]}""") };
        var client = new OpenSubtitlesClient(new HttpClient(server), Settings, _cache.FullName);

        await client.SearchAsync(new VideoQuery("x.mkv", "", "The Sopranos", 1999, 1, 2), CancellationToken.None);

        var request = Assert.Single(server.Requests);
        Assert.Equal("KEY", request.Headers.GetValues("Api-Key").Single());
        Assert.Equal("sonarr v1.0.0", string.Join(" ", request.Headers.GetValues("User-Agent")));
        Assert.Equal("?episode_number=2&languages=pt-br&query=the%20sopranos&season_number=1&type=episode", request.RequestUri!.Query);
    }

    [Fact]
    public async Task Search_WithoutApiKey_Throws()
    {
        var client = new OpenSubtitlesClient(new HttpClient(new FakeServer()), new OpenSubtitlesSettings(), _cache.FullName);

        await Assert.ThrowsAsync<OpenSubtitlesException>(() => client.SearchAsync(VideoQuery.Parse("Movie (2020)"), CancellationToken.None));
    }

    [Fact]
    public async Task Download_LogsIn_UsesTheVipHost_SavesTheFile_AndCachesIt()
    {
        var server = new FakeServer
        {
            Respond = r => r.RequestUri!.AbsolutePath switch
            {
                "/api/v1/login" => Json("""{"token":"JWT","base_url":"vip-api.opensubtitles.com"}"""),
                "/api/v1/download" => Json("""{"link":"https://dl.example/file.srt","remaining":999}"""),
                "/file.srt" => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("1\n00:00:01,000 --> 00:00:02,000\nOi\n") },
                _ => new HttpResponseMessage(HttpStatusCode.NotFound),
            },
        };
        var client = new OpenSubtitlesClient(new HttpClient(server), Settings, _cache.FullName);

        var first = await client.DownloadAsync(4714815, CancellationToken.None);
        var second = await client.DownloadAsync(4714815, CancellationToken.None);

        Assert.Equal(999, first.RemainingDownloads);
        Assert.Contains("Oi", File.ReadAllText(first.FileName));
        Assert.Equal(first.FileName, second.FileName);
        Assert.Null(second.RemainingDownloads); // served from cache
        Assert.Equal(3, server.Requests.Count); // login, download, file - nothing for the second call

        var download = server.Requests[1];
        Assert.Equal("vip-api.opensubtitles.com", download.RequestUri!.Host);
        Assert.Equal("Bearer JWT", download.Headers.Authorization!.ToString());
        Assert.Equal("KEY", download.Headers.GetValues("Api-Key").Single());
        Assert.Equal("""{"file_id":4714815}""", server.Bodies[1]);
    }

    [Fact]
    public async Task Download_WrongPassword_ReportsTheServerMessage()
    {
        var server = new FakeServer { Respond = _ => Json("""{"message":"You cannot consume this service"}""", HttpStatusCode.Unauthorized) };
        var client = new OpenSubtitlesClient(new HttpClient(server), Settings, _cache.FullName);

        var ex = await Assert.ThrowsAsync<OpenSubtitlesException>(() => client.DownloadAsync(1, CancellationToken.None));

        Assert.Equal(HttpStatusCode.Unauthorized, ex.Status);
        Assert.Contains("You cannot consume this service", ex.Message);
    }

    [Fact]
    public async Task Download_WithoutLogin_Throws_WithoutCallingTheServer()
    {
        var server = new FakeServer();
        var client = new OpenSubtitlesClient(new HttpClient(server), Settings with { Password = "" }, _cache.FullName);

        await Assert.ThrowsAsync<OpenSubtitlesException>(() => client.DownloadAsync(1, CancellationToken.None));
        Assert.Empty(server.Requests);
    }

    [Theory]
    [InlineData("The Sopranos (1999) - S01E01 - Pilot [Bluray-1080p][AAC 5.1][x265]-ImE", "The Sopranos", 1999, 1, 1)]
    [InlineData("The.Sopranos.S06E21.1080p.BluRay.x265-RARBG", "The Sopranos", null, 6, 21)]
    [InlineData("Duna (2021) {tmdb-438631} - [Bluray-1080p]", "Duna", 2021, null, null)]
    [InlineData("Dune.Part.Two.2024.2160p.WEB-DL", "Dune Part Two", 2024, null, null)]
    [InlineData("Some Movie 1080p BluRay", "Some Movie", null, null, null)]
    public void VideoQuery_Parse_ReadsTitleYearSeasonEpisode(string name, string title, int? year, int? season, int? episode)
    {
        var q = VideoQuery.Parse(name);

        Assert.Equal(title, q.Title);
        Assert.Equal(year, q.Year);
        Assert.Equal(season, q.Season);
        Assert.Equal(episode, q.Episode);
    }

    [Fact]
    public void Describe_ShowsLanguageReleaseFlagsAndDownloads()
    {
        var s = new OnlineSubtitle(1, "Rel", "pt-BR", 10109, true, 23.976, false, true, "u");

        Assert.Equal("pt-BR · Rel · ✓ hash · HI · ⬇10,109", s.Describe());
    }
}
