using System.Globalization;
using System.Net;
using System.Xml.Linq;

namespace Nikse.SubtitleEdit.UiLogic.SimpleSync;

public sealed record PlexSettings
{
    public const string DefaultUrl = "http://127.0.0.1:32400";

    public string Url { get; init; } = DefaultUrl;
    public string Token { get; init; } = string.Empty;
    public string ServerName { get; init; } = string.Empty;

    /// <summary>Random id this app uses with plex.tv sign-in (Plex asks every client for one).</summary>
    public string ClientId { get; init; } = Guid.NewGuid().ToString("N");

    public bool IsConfigured => !string.IsNullOrWhiteSpace(Token) && !string.IsNullOrWhiteSpace(Url);
}

public sealed class PlexException(string message, HttpStatusCode? status = null) : Exception(message)
{
    public HttpStatusCode? Status { get; } = status;
}

/// <summary>
/// Talks to a Plex Media Server, on this computer or elsewhere (NAS, Docker): checks the
/// connection and tells it a video's subtitles changed. The video is matched by the end of its
/// path ("Season 01/Show - S01E01.mkv"), so it works when Plex sees the files under another
/// path than this computer does.
/// </summary>
public sealed class PlexClient
{
    private readonly HttpClient _http;
    private readonly string _baseUrl;
    private readonly string _token;

    public PlexClient(HttpClient http, string baseUrl, string token)
    {
        _http = http;
        _baseUrl = baseUrl.TrimEnd('/');
        _token = token;
    }

    /// <summary>Returns the server's name; throws <see cref="PlexException"/> when unreachable or the token is refused.</summary>
    public async Task<string> CheckAsync(CancellationToken token)
    {
        var root = await GetXmlAsync("/", token);
        // "/" answers without a valid token on some setups; the library list never does.
        await GetXmlAsync("/library/sections", token);
        return (string?)root.Root?.Attribute("friendlyName") ?? "Plex";
    }

    /// <summary>
    /// Scans the folder of the video and refreshes its item, so Plex re-reads the subtitles.
    /// Returns false when Plex has no item for this file (not in a library).
    /// </summary>
    public async Task<bool> RefreshAsync(string videoFileName, CancellationToken token)
    {
        var sections = (await GetXmlAsync("/library/sections", token)).Descendants("Directory")
            .Select(d => (Key: (string?)d.Attribute("key") ?? string.Empty, Type: (string?)d.Attribute("type") ?? string.Empty))
            .Where(s => s.Type is "show" or "movie")
            .ToList();

        (string Key, string RatingKey, string PlexFile, int Score) best = default;
        foreach (var section in sections)
        {
            var type = section.Type == "show" ? 4 : 1;
            var items = await GetXmlAsync($"/library/sections/{section.Key}/all?type={type}", token);
            foreach (var video in items.Descendants("Video"))
            {
                foreach (var part in video.Descendants("Part"))
                {
                    var plexFile = (string?)part.Attribute("file") ?? string.Empty;
                    var score = MatchingTailSegments(plexFile, videoFileName);
                    if (score > best.Score)
                    {
                        best = (section.Key, (string?)video.Attribute("ratingKey") ?? string.Empty, plexFile, score);
                    }
                }
            }
        }

        // The file name alone is not enough when two shows share episode names: want the folder too.
        if (best.Score < 2 || best.RatingKey.Length == 0)
        {
            return false;
        }

        var plexFolder = PlexDirectoryName(best.PlexFile);
        await SendAsync(HttpMethod.Get, $"/library/sections/{best.Key}/refresh?path={Uri.EscapeDataString(plexFolder)}", token);
        await SendAsync(HttpMethod.Put, $"/library/metadata/{best.RatingKey}/refresh", token);
        return true;
    }

    /// <summary>How many trailing path segments two paths share (any separator, case-insensitive).</summary>
    public static int MatchingTailSegments(string a, string b)
    {
        var x = Split(a);
        var y = Split(b);
        var n = 0;
        while (n < x.Length && n < y.Length && x[^(n + 1)].Equals(y[^(n + 1)], StringComparison.OrdinalIgnoreCase))
        {
            n++;
        }

        return n;
    }

    private static string[] Split(string path) => path.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries);

    /// <summary>The folder of a path as Plex writes it (keeps Plex's own separator).</summary>
    private static string PlexDirectoryName(string plexFile)
    {
        var cut = Math.Max(plexFile.LastIndexOf('/'), plexFile.LastIndexOf('\\'));
        return cut > 0 ? plexFile[..cut] : plexFile;
    }

    private async Task<XDocument> GetXmlAsync(string path, CancellationToken token)
    {
        var body = await SendAsync(HttpMethod.Get, path, token);
        try
        {
            return XDocument.Parse(body);
        }
        catch (System.Xml.XmlException)
        {
            throw new PlexException("Plex answered something that is not XML");
        }
    }

    private async Task<string> SendAsync(HttpMethod method, string path, CancellationToken token)
    {
        using var request = new HttpRequestMessage(method, _baseUrl + path);
        request.Headers.Add("X-Plex-Token", _token);
        request.Headers.Add("Accept", "application/xml");
        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(request, token);
        }
        catch (HttpRequestException ex)
        {
            throw new PlexException($"Plex not reachable at {_baseUrl}: {ex.Message}");
        }
        catch (TaskCanceledException) when (!token.IsCancellationRequested)
        {
            throw new PlexException($"Plex did not answer at {_baseUrl}");
        }

        using (response)
        {
            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                throw new PlexException("Plex refused the sign-in (token invalid or expired)", response.StatusCode);
            }

            if (!response.IsSuccessStatusCode)
            {
                throw new PlexException($"Plex: {(int)response.StatusCode} {response.ReasonPhrase}", response.StatusCode);
            }

            return await response.Content.ReadAsStringAsync(token);
        }
    }
}

/// <summary>
/// "Sign in with Plex": the PIN flow plex.tv offers to apps. The user approves this app in the
/// browser; the app then receives a token and the list of the user's servers.
/// </summary>
public sealed class PlexSignIn
{
    public const string PlexTv = "https://plex.tv";
    public const string Product = "Simple Subtitle Edit";

    private readonly HttpClient _http;
    private readonly string _clientId;

    public PlexSignIn(HttpClient http, string clientId)
    {
        _http = http;
        _clientId = clientId;
    }

    public sealed record Pin(long Id, string Code);

    public sealed record Server(string Name, string Url, string AccessToken, bool Owned, bool Local);

    public async Task<Pin> CreatePinAsync(CancellationToken token)
    {
        var doc = await SendAsync(HttpMethod.Post, "/api/v2/pins?strong=true", null, token);
        var pin = doc.Root!;
        return new Pin(long.Parse((string?)pin.Attribute("id") ?? "0", CultureInfo.InvariantCulture), (string?)pin.Attribute("code") ?? string.Empty);
    }

    /// <summary>The page where the user approves this app.</summary>
    public string AuthUrl(Pin pin) =>
        "https://app.plex.tv/auth#?" +
        $"clientID={Uri.EscapeDataString(_clientId)}&code={Uri.EscapeDataString(pin.Code)}" +
        $"&context%5Bdevice%5D%5Bproduct%5D={Uri.EscapeDataString(Product)}";

    /// <summary>Polls until the user approves (returns the token) or the time runs out (null).</summary>
    public async Task<string?> WaitForTokenAsync(Pin pin, TimeSpan timeout, TimeSpan interval, CancellationToken token)
    {
        var until = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < until)
        {
            var doc = await SendAsync(HttpMethod.Get, $"/api/v2/pins/{pin.Id}", null, token);
            var authToken = (string?)doc.Root?.Attribute("authToken");
            if (!string.IsNullOrEmpty(authToken))
            {
                return authToken;
            }

            await Task.Delay(interval, token);
        }

        return null;
    }

    /// <summary>The user's servers, owned first, each with its best address (local before remote, never the relay).</summary>
    public async Task<List<Server>> ServersAsync(string userToken, CancellationToken token)
    {
        var doc = await SendAsync(HttpMethod.Get, "/api/v2/resources?includeHttps=1", userToken, token);
        var servers = new List<Server>();
        foreach (var resource in doc.Descendants("resource"))
        {
            if (!((string?)resource.Attribute("provides") ?? string.Empty).Contains("server", StringComparison.Ordinal))
            {
                continue;
            }

            var connection = resource.Descendants("connection")
                .Where(c => (string?)c.Attribute("relay") != "1")
                .OrderByDescending(c => (string?)c.Attribute("local") == "1")
                .FirstOrDefault();
            if (connection == null)
            {
                continue;
            }

            servers.Add(new Server(
                (string?)resource.Attribute("name") ?? "Plex",
                (string?)connection.Attribute("uri") ?? string.Empty,
                (string?)resource.Attribute("accessToken") ?? userToken,
                (string?)resource.Attribute("owned") == "1",
                (string?)connection.Attribute("local") == "1"));
        }

        return servers.OrderByDescending(s => s.Owned).ThenByDescending(s => s.Local).ToList();
    }

    private async Task<XDocument> SendAsync(HttpMethod method, string path, string? userToken, CancellationToken token)
    {
        using var request = new HttpRequestMessage(method, PlexTv + path);
        request.Headers.Add("Accept", "application/xml");
        request.Headers.Add("X-Plex-Product", Product);
        request.Headers.Add("X-Plex-Client-Identifier", _clientId);
        if (userToken != null)
        {
            request.Headers.Add("X-Plex-Token", userToken);
        }

        using var response = await _http.SendAsync(request, token);
        if (!response.IsSuccessStatusCode)
        {
            throw new PlexException($"plex.tv: {(int)response.StatusCode} {response.ReasonPhrase}", response.StatusCode);
        }

        return XDocument.Parse(await response.Content.ReadAsStringAsync(token));
    }
}
