using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Nikse.SubtitleEdit.UiLogic.SimpleSync;

public sealed record OpenSubtitlesSettings
{
    public string ApiKey { get; init; } = string.Empty;
    public string Username { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;

    /// <summary>Name of the API consumer created on opensubtitles.com; sent as the User-Agent.</summary>
    public string AppName { get; init; } = "SimpleSubtitleEdit";

    /// <summary>OpenSubtitles language code, e.g. "pt-br", "en", "es".</summary>
    public string Language { get; init; } = "pt-br";

    public bool CanSearch => !string.IsNullOrWhiteSpace(ApiKey);

    public bool CanDownload => CanSearch && !string.IsNullOrWhiteSpace(Username) && !string.IsNullOrWhiteSpace(Password);
}

public sealed record OnlineSubtitle(
    int FileId,
    string Release,
    string Language,
    int DownloadCount,
    bool HashMatch,
    double Fps,
    bool AiTranslated,
    bool HearingImpaired,
    string Uploader)
{
    public string Describe()
    {
        var parts = new List<string> { Language, Release };
        if (HashMatch)
        {
            parts.Add("✓ hash");
        }

        if (AiTranslated)
        {
            parts.Add("AI");
        }

        if (HearingImpaired)
        {
            parts.Add("HI");
        }

        parts.Add("⬇" + DownloadCount.ToString("N0", CultureInfo.InvariantCulture));
        return string.Join(" · ", parts);
    }
}

public sealed record DownloadResult(string FileName, int? RemainingDownloads);

public sealed class OpenSubtitlesException(string message, HttpStatusCode? status = null) : Exception(message)
{
    public HttpStatusCode? Status { get; } = status;
}

/// <summary>
/// The parts of the OpenSubtitles.com REST API the simple window uses: search (API key only)
/// and download (needs a logged-in user; this is what counts against the daily limit).
/// Downloads are cached on disk by file id, so picking the same subtitle again is free.
/// </summary>
public sealed class OpenSubtitlesClient
{
    public const string DefaultBaseUrl = "https://api.opensubtitles.com/api/v1";
    public const string AppVersion = "1.0.0";

    private readonly HttpClient _http;
    private readonly OpenSubtitlesSettings _settings;
    private readonly string _cacheFolder;
    private string _baseUrl = DefaultBaseUrl;
    private string? _token;

    public OpenSubtitlesClient(HttpClient http, OpenSubtitlesSettings settings, string cacheFolder)
    {
        _http = http;
        _settings = settings;
        _cacheFolder = cacheFolder;
    }

    public string CachePathFor(int fileId) => Path.Combine(_cacheFolder, fileId.ToString(CultureInfo.InvariantCulture) + ".srt");

    /// <summary>
    /// Searches by file hash and by title (+ season/episode), merges both lists, and puts exact
    /// hash matches first, then the most downloaded.
    /// </summary>
    public async Task<List<OnlineSubtitle>> SearchAsync(VideoQuery video, CancellationToken token)
    {
        if (!_settings.CanSearch)
        {
            throw new OpenSubtitlesException("OpenSubtitles API key missing");
        }

        var results = new Dictionary<int, OnlineSubtitle>();
        if (!string.IsNullOrEmpty(video.MovieHash))
        {
            foreach (var s in await SearchOnceAsync(new() { ["moviehash"] = video.MovieHash }, token))
            {
                results[s.FileId] = s with { HashMatch = true };
            }
        }

        if (!string.IsNullOrEmpty(video.Title))
        {
            var query = new Dictionary<string, string> { ["query"] = video.Title };
            if (video.IsEpisode)
            {
                query["season_number"] = video.Season!.Value.ToString(CultureInfo.InvariantCulture);
                query["episode_number"] = video.Episode!.Value.ToString(CultureInfo.InvariantCulture);
                query["type"] = "episode";
            }
            else if (video.Year != null)
            {
                query["year"] = video.Year.Value.ToString(CultureInfo.InvariantCulture);
            }

            foreach (var s in await SearchOnceAsync(query, token))
            {
                results.TryAdd(s.FileId, s);
            }
        }

        return results.Values
            .OrderByDescending(s => s.HashMatch)
            .ThenBy(s => s.AiTranslated)
            .ThenByDescending(s => s.DownloadCount)
            .ToList();
    }

    private async Task<List<OnlineSubtitle>> SearchOnceAsync(Dictionary<string, string> query, CancellationToken token)
    {
        query["languages"] = _settings.Language.ToLowerInvariant();

        // API best practice: parameters sorted, values lower case (better cache hits, no redirects).
        var queryString = string.Join("&", query
            .OrderBy(kv => kv.Key, StringComparer.Ordinal)
            .Select(kv => kv.Key + "=" + Uri.EscapeDataString(kv.Value.ToLowerInvariant())));

        using var request = NewRequest(HttpMethod.Get, "/subtitles?" + queryString);
        using var response = await _http.SendAsync(request, token);
        var json = await ReadJsonAsync(response, token);
        return ParseSearch(json);
    }

    public static List<OnlineSubtitle> ParseSearch(JsonNode? json)
    {
        var list = new List<OnlineSubtitle>();
        if (json?["data"] is not JsonArray data)
        {
            return list;
        }

        foreach (var item in data)
        {
            var a = item?["attributes"];
            var file = a?["files"] is JsonArray files && files.Count > 0 ? files[0] : null;
            var fileId = file?["file_id"]?.GetValue<int>();
            if (a == null || fileId == null)
            {
                continue;
            }

            list.Add(new OnlineSubtitle(
                fileId.Value,
                a["release"]?.GetValue<string>() ?? file?["file_name"]?.GetValue<string>() ?? fileId.Value.ToString(CultureInfo.InvariantCulture),
                a["language"]?.GetValue<string>() ?? string.Empty,
                a["download_count"]?.GetValue<int>() ?? 0,
                a["moviehash_match"]?.GetValue<bool>() ?? false,
                a["fps"]?.GetValue<double>() ?? 0,
                a["ai_translated"]?.GetValue<bool>() ?? false,
                a["hearing_impaired"]?.GetValue<bool>() ?? false,
                a["uploader"]?["name"]?.GetValue<string>() ?? string.Empty));
        }

        return list;
    }

    /// <summary>Downloads one subtitle (spends one download, unless it is already cached).</summary>
    public async Task<DownloadResult> DownloadAsync(int fileId, CancellationToken token)
    {
        var target = CachePathFor(fileId);
        if (File.Exists(target))
        {
            return new DownloadResult(target, null);
        }

        if (!_settings.CanDownload)
        {
            throw new OpenSubtitlesException("OpenSubtitles username and password are needed to download");
        }

        await EnsureLoginAsync(token);

        using var request = NewRequest(HttpMethod.Post, "/download");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);
        request.Content = new StringContent(JsonSerializer.Serialize(new { file_id = fileId }), Encoding.UTF8, "application/json");
        using var response = await _http.SendAsync(request, token);
        var json = await ReadJsonAsync(response, token);
        var link = json?["link"]?.GetValue<string>() ?? throw new OpenSubtitlesException("OpenSubtitles did not return a download link");
        var remaining = json["remaining"]?.GetValue<int>();

        var bytes = await _http.GetByteArrayAsync(link, token);
        Directory.CreateDirectory(_cacheFolder);
        await File.WriteAllBytesAsync(target, bytes, token);
        return new DownloadResult(target, remaining);
    }

    private async Task EnsureLoginAsync(CancellationToken token)
    {
        if (_token != null)
        {
            return;
        }

        using var request = NewRequest(HttpMethod.Post, "/login");
        request.Content = new StringContent(
            JsonSerializer.Serialize(new { username = _settings.Username, password = _settings.Password }),
            Encoding.UTF8,
            "application/json");
        using var response = await _http.SendAsync(request, token);
        var json = await ReadJsonAsync(response, token);
        _token = json?["token"]?.GetValue<string>() ?? throw new OpenSubtitlesException("OpenSubtitles login failed");

        // VIP accounts get their own host; every later request must go there.
        var baseHost = json["base_url"]?.GetValue<string>();
        if (!string.IsNullOrWhiteSpace(baseHost))
        {
            _baseUrl = (baseHost.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? baseHost : "https://" + baseHost).TrimEnd('/') + "/api/v1";
        }
    }

    private HttpRequestMessage NewRequest(HttpMethod method, string pathAndQuery)
    {
        var request = new HttpRequestMessage(method, _baseUrl + pathAndQuery);
        request.Headers.Add("Api-Key", _settings.ApiKey);
        request.Headers.TryAddWithoutValidation("User-Agent", $"{_settings.AppName} v{AppVersion}");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        if (_token != null && method == HttpMethod.Get)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);
        }

        return request;
    }

    private static async Task<JsonNode?> ReadJsonAsync(HttpResponseMessage response, CancellationToken token)
    {
        var body = await response.Content.ReadAsStringAsync(token);
        if (!response.IsSuccessStatusCode)
        {
            string? message = null;
            try
            {
                message = JsonNode.Parse(body)?["message"]?.GetValue<string>();
            }
            catch (JsonException)
            {
                // not JSON (HTML error page)
            }

            throw new OpenSubtitlesException($"OpenSubtitles: {(int)response.StatusCode} {message ?? response.ReasonPhrase}", response.StatusCode);
        }

        return JsonNode.Parse(body);
    }
}
