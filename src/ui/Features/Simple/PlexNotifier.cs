using Nikse.SubtitleEdit.UiLogic.SimpleSync;
using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Nikse.SubtitleEdit.Features.Simple;

/// <summary>
/// Tells Plex a video's subtitles changed. Uses the server set in ⚙ (any machine: this one, a
/// NAS, Docker); with nothing set, tries a Plex on this computer. Silent when there is no Plex.
/// </summary>
public static class PlexNotifier
{
    public static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(15) };

    /// <returns>True when Plex was told.</returns>
    public static async Task<bool> RefreshAsync(string videoFileName, CancellationToken token = default)
    {
        var settings = SimpleSettingsStore.LoadPlex();
        if (!settings.IsConfigured)
        {
            settings = DetectLocal() ?? settings;
        }

        if (!settings.IsConfigured)
        {
            return false;
        }

        try
        {
            return await new PlexClient(Http, settings.Url, settings.Token).RefreshAsync(videoFileName, token);
        }
        catch (Exception)
        {
            return false; // Plex down or unreachable: the subtitle is saved anyway
        }
    }

    /// <summary>A Plex Media Server on this computer, with the token from its own settings. Null if none.</summary>
    public static PlexSettings? DetectLocal()
    {
        var token = FindLocalToken();
        return string.IsNullOrEmpty(token) ? null : new PlexSettings { Url = PlexSettings.DefaultUrl, Token = token };
    }

    /// <summary>The local server's token, from where Plex keeps it on each system.</summary>
    public static string? FindLocalToken()
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

            // Linux: package install. Usually readable only by the "plex" user, hence SIGN IN WITH PLEX.
            var prefs = "/var/lib/plexmediaserver/Library/Application Support/Plex Media Server/Preferences.xml";
            return File.Exists(prefs) ? (string?)XDocument.Load(prefs).Root?.Attribute("PlexOnlineToken") : null;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
