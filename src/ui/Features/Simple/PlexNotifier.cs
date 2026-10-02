using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Nikse.SubtitleEdit.Features.Simple;

/// <summary>
/// Tells a Plex Media Server on this computer that a video's subtitles changed: a scan of the
/// video's folder (picks up a rewritten mkv or a new .srt) and a metadata refresh of the item
/// (re-reads a .srt that only changed its content). Silent when there is no local Plex.
/// </summary>
public static class PlexNotifier
{
    private const string BaseUrl = "http://127.0.0.1:32400";
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(10) };

    /// <returns>True when Plex was found and told.</returns>
    public static async Task<bool> RefreshAsync(string videoFileName, CancellationToken token = default)
    {
        var plexToken = FindToken();
        if (string.IsNullOrEmpty(plexToken))
        {
            return false;
        }

        try
        {
            var sections = XDocument.Parse(await GetAsync("/library/sections", plexToken, token));
            var full = Path.GetFullPath(videoFileName);
            var section = sections.Descendants("Directory")
                .Select(d => (Key: (string?)d.Attribute("key"), Paths: d.Elements("Location").Select(l => (string?)l.Attribute("path")).OfType<string>().ToList()))
                .FirstOrDefault(s => s.Paths.Any(p => full.StartsWith(p.TrimEnd('/', '\\') + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)));
            if (section.Key == null)
            {
                return false;
            }

            var folder = Path.GetDirectoryName(full)!;
            await GetAsync($"/library/sections/{section.Key}/refresh?path={Uri.EscapeDataString(folder)}", plexToken, token);

            // Find the item whose media part is this file and refresh it.
            var all = XDocument.Parse(await GetAsync($"/library/sections/{section.Key}/all?type=4", plexToken, token));
            var item = all.Descendants("Video")
                .FirstOrDefault(v => v.Descendants("Part").Any(p => string.Equals((string?)p.Attribute("file"), full, StringComparison.OrdinalIgnoreCase)));
            if (item == null)
            {
                all = XDocument.Parse(await GetAsync($"/library/sections/{section.Key}/all?type=1", plexToken, token));
                item = all.Descendants("Video")
                    .FirstOrDefault(v => v.Descendants("Part").Any(p => string.Equals((string?)p.Attribute("file"), full, StringComparison.OrdinalIgnoreCase)));
            }

            if ((string?)item?.Attribute("ratingKey") is { } key)
            {
                using var request = new HttpRequestMessage(HttpMethod.Put, $"{BaseUrl}/library/metadata/{key}/refresh");
                request.Headers.Add("X-Plex-Token", plexToken);
                await Http.SendAsync(request, token);
            }

            return true;
        }
        catch (Exception)
        {
            return false; // Plex not running or unreachable: the file is saved anyway
        }
    }

    private static async Task<string> GetAsync(string path, string token, CancellationToken cancel)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, BaseUrl + path);
        request.Headers.Add("X-Plex-Token", token);
        using var response = await Http.SendAsync(request, cancel);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancel);
    }

    /// <summary>The local server's token, from where Plex keeps it on each system.</summary>
    public static string? FindToken()
    {
        try
        {
            if (OperatingSystem.IsMacOS())
            {
                var psi = new ProcessStartInfo("defaults", ["read", "com.plexapp.plexmediaserver", "PlexOnlineToken"])
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };
                using var process = Process.Start(psi);
                var output = process?.StandardOutput.ReadToEnd().Trim();
                process?.WaitForExit(3000);
                return process?.ExitCode == 0 ? output : null;
            }

            if (OperatingSystem.IsWindows())
            {
                return Microsoft.Win32.Registry.GetValue(@"HKEY_CURRENT_USER\Software\Plex, Inc.\Plex Media Server", "PlexOnlineToken", null) as string;
            }

            var prefs = "/var/lib/plexmediaserver/Library/Application Support/Plex Media Server/Preferences.xml";
            return File.Exists(prefs) ? (string?)XDocument.Load(prefs).Root?.Attribute("PlexOnlineToken") : null;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
