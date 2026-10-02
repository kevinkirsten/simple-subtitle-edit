using System.Net;
using System.Text;
using Nikse.SubtitleEdit.UiLogic.SimpleSync;

namespace LibUiLogicTests.SimpleSync;

/// <summary>Plex client and plex.tv sign-in against fake servers (never the user's real Plex).</summary>
public class PlexClientTests
{
    private sealed class Fake : HttpMessageHandler
    {
        public List<(HttpMethod Method, string PathAndQuery, string? Token)> Calls { get; } = [];
        public Func<HttpRequestMessage, HttpResponseMessage> Respond { get; set; } = _ => new HttpResponseMessage(HttpStatusCode.NotFound);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            request.Headers.TryGetValues("X-Plex-Token", out var token);
            Calls.Add((request.Method, request.RequestUri!.PathAndQuery, token?.FirstOrDefault()));
            return Task.FromResult(Respond(request));
        }
    }

    private static HttpResponseMessage Xml(string xml, HttpStatusCode code = HttpStatusCode.OK) =>
        new(code) { Content = new StringContent(xml, Encoding.UTF8, "application/xml") };

    // Plex in Docker: it sees the files under /data, this computer under /Volumes/X10 Pro.
    private const string Sections = """<MediaContainer><Directory key="1" type="show"><Location path="/data/tv"/></Directory><Directory key="2" type="movie"><Location path="/data/movies"/></Directory></MediaContainer>""";
    private const string Episodes = """
        <MediaContainer>
          <Video ratingKey="3"><Media><Part file="/data/tv/The Sopranos (1999)/Season 01/The Sopranos - S01E01 - Pilot.mkv"/></Media></Video>
          <Video ratingKey="4"><Media><Part file="/data/tv/The Sopranos (1999)/Season 01/The Sopranos - S01E02 - 46 Long.mkv"/></Media></Video>
          <Video ratingKey="9"><Media><Part file="/data/tv/Other Show/Season 01/The Sopranos - S01E01 - Pilot.mkv"/></Media></Video>
        </MediaContainer>
        """;

    private static Fake Server() => new()
    {
        Respond = r => r.RequestUri!.AbsolutePath switch
        {
            "/" => Xml("""<MediaContainer friendlyName="Kevin's MacBook Pro M5"/>"""),
            "/library/sections" => Xml(Sections),
            "/library/sections/1/all" => Xml(Episodes),
            "/library/sections/2/all" => Xml("<MediaContainer/>"),
            _ => Xml("<MediaContainer/>"),
        },
    };

    [Theory]
    [InlineData("/data/tv/Show/Season 01/ep.mkv", "/Volumes/X10 Pro/Series/Show/Season 01/ep.mkv", 3)]
    [InlineData("D:\\TV\\Show\\Season 01\\ep.mkv", "/mnt/tv/Show/Season 01/ep.mkv", 4)] // "TV" = "tv"
    [InlineData("/a/ep.mkv", "/b/other.mkv", 0)]
    public void MatchingTailSegments_IgnoresWhereTheLibraryIsMounted(string plex, string local, int expected)
    {
        Assert.Equal(expected, PlexClient.MatchingTailSegments(plex, local));
    }

    [Fact]
    public async Task Check_ReturnsTheServerName_UsingTheToken()
    {
        var fake = Server();
        var name = await new PlexClient(new HttpClient(fake), "http://nas:32400/", "TOKEN").CheckAsync(CancellationToken.None);

        Assert.Equal("Kevin's MacBook Pro M5", name);
        Assert.All(fake.Calls, c => Assert.Equal("TOKEN", c.Token));
    }

    [Fact]
    public async Task Check_WrongToken_SaysSo()
    {
        var fake = new Fake { Respond = r => r.RequestUri!.AbsolutePath == "/" ? Xml("<MediaContainer/>") : Xml("", HttpStatusCode.Unauthorized) };

        var ex = await Assert.ThrowsAsync<PlexException>(() => new PlexClient(new HttpClient(fake), "http://nas:32400", "BAD").CheckAsync(CancellationToken.None));

        Assert.Equal(HttpStatusCode.Unauthorized, ex.Status);
    }

    [Fact]
    public async Task Check_ServerDown_SaysWhere()
    {
        var down = new Fake { Respond = _ => throw new HttpRequestException("Connection refused") };

        var ex = await Assert.ThrowsAsync<PlexException>(() => new PlexClient(new HttpClient(down), "http://10.0.0.5:32400", "T").CheckAsync(CancellationToken.None));

        Assert.Contains("10.0.0.5:32400", ex.Message);
    }

    [Fact]
    public async Task Refresh_FindsTheEpisodeByItsFolderAndName_ThenScansAndRefreshesIt()
    {
        var fake = Server();
        var local = "/Volumes/X10 Pro/Movies & Series/Series/The Sopranos (1999)/Season 01/The Sopranos - S01E01 - Pilot.mkv";

        var found = await new PlexClient(new HttpClient(fake), "http://nas:32400", "T").RefreshAsync(local, CancellationToken.None);

        Assert.True(found);
        Assert.Contains(fake.Calls, c => c.Method == HttpMethod.Get && c.PathAndQuery == "/library/sections/1/refresh?path=" + Uri.EscapeDataString("/data/tv/The Sopranos (1999)/Season 01"));
        Assert.Contains(fake.Calls, c => c.Method == HttpMethod.Put && c.PathAndQuery == "/library/metadata/3/refresh"); // not 9, the other show
    }

    [Fact]
    public async Task Refresh_VideoNotInPlex_ReturnsFalse_WithoutRefreshing()
    {
        var fake = Server();

        var found = await new PlexClient(new HttpClient(fake), "http://nas:32400", "T").RefreshAsync("/Users/x/Downloads/random.mkv", CancellationToken.None);

        Assert.False(found);
        Assert.DoesNotContain(fake.Calls, c => c.Method == HttpMethod.Put);
    }

    [Fact]
    public async Task SignIn_PinFlow_ThenServers_OwnedAndLocalFirst_NoRelay()
    {
        var polls = 0;
        var fake = new Fake
        {
            Respond = r => (r.Method.Method, r.RequestUri!.AbsolutePath) switch
            {
                ("POST", "/api/v2/pins") => Xml("""<pin id="42" code="abc123" authToken=""/>"""),
                ("GET", "/api/v2/pins/42") => Xml(++polls < 3 ? """<pin id="42" authToken=""/>""" : """<pin id="42" authToken="USER"/>"""),
                ("GET", "/api/v2/resources") => Xml("""
                    <resources>
                      <resource name="Friend's server" provides="server" owned="0" accessToken="F"><connections><connection uri="https://friend:32400" local="0" relay="0"/></connections></resource>
                      <resource name="Kevin's MacBook Pro M5" provides="server" owned="1" accessToken="S">
                        <connections>
                          <connection uri="https://relay.plex.direct:8443" local="0" relay="1"/>
                          <connection uri="https://203-0-113-5.plex.direct:32400" local="0" relay="0"/>
                          <connection uri="https://192-168-15-24.plex.direct:32400" local="1" relay="0"/>
                        </connections>
                      </resource>
                      <resource name="Kevin's iPhone" provides="client,player" owned="1" accessToken="P"><connections><connection uri="https://phone" local="1" relay="0"/></connections></resource>
                    </resources>
                    """),
                _ => Xml("", HttpStatusCode.NotFound),
            },
        };
        var signIn = new PlexSignIn(new HttpClient(fake), "client-1");

        var pin = await signIn.CreatePinAsync(CancellationToken.None);
        var url = signIn.AuthUrl(pin);
        var userToken = await signIn.WaitForTokenAsync(pin, TimeSpan.FromSeconds(5), TimeSpan.FromMilliseconds(1), CancellationToken.None);
        var servers = await signIn.ServersAsync(userToken!, CancellationToken.None);

        Assert.Contains("clientID=client-1&code=abc123", url);
        Assert.Equal("USER", userToken);
        Assert.Equal(3, polls);
        Assert.Equal(["Kevin's MacBook Pro M5", "Friend's server"], servers.Select(s => s.Name));
        Assert.Equal("https://192-168-15-24.plex.direct:32400", servers[0].Url);
        Assert.Equal("S", servers[0].AccessToken);
    }

    [Fact]
    public async Task SignIn_NotApprovedInTime_ReturnsNull()
    {
        var fake = new Fake { Respond = _ => Xml("""<pin id="1" authToken=""/>""") };
        var signIn = new PlexSignIn(new HttpClient(fake), "c");

        Assert.Null(await signIn.WaitForTokenAsync(new PlexSignIn.Pin(1, "x"), TimeSpan.FromMilliseconds(30), TimeSpan.FromMilliseconds(5), CancellationToken.None));
    }
}
