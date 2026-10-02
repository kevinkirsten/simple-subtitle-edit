using System.Diagnostics;
using System.Globalization;
using System.Text.Json.Nodes;

namespace Nikse.SubtitleEdit.UiLogic.SimpleSync;

public sealed record MkvTrack(int Id, int Number, string Type, string Codec, string Language, string LanguageIetf, string Name)
{
    public bool IsTextSubtitle => Type == "subtitles" && (Codec.Contains("SubRip", StringComparison.OrdinalIgnoreCase) ||
                                                          Codec.Contains("SSA", StringComparison.OrdinalIgnoreCase) ||
                                                          Codec.Contains("Text", StringComparison.OrdinalIgnoreCase) ||
                                                          Codec.Contains("WebVTT", StringComparison.OrdinalIgnoreCase));
}

public sealed record MkvInfo(long DurationNs, IReadOnlyList<MkvTrack> Tracks)
{
    public int Count(string type) => Tracks.Count(t => t.Type == type);
}

public sealed record EmbedResult(int TrackNumber, IReadOnlyList<int> RemovedTrackNumbers);

public sealed class EmbedException(string message) : Exception(message);

/// <summary>
/// Puts a subtitle inside an mkv as a text track (soft subtitle, can be turned off and edited
/// again), using mkvmerge from MKVToolNix. The new file is written next to the original and
/// checked (same duration, same video/audio tracks, the new track present) before it replaces
/// the original; if anything is off, the original is left untouched.
/// </summary>
public static class MatroskaEmbedder
{
    public static string? FindMkvmerge(string? configured = null)
    {
        var exe = OperatingSystem.IsWindows() ? "mkvmerge.exe" : "mkvmerge";
        var candidates = new List<string?>
        {
            configured,
            Path.Combine(AppContext.BaseDirectory, exe),
            "/opt/homebrew/bin/mkvmerge",
            "/usr/local/bin/mkvmerge",
            "/usr/bin/mkvmerge",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "MKVToolNix", "mkvmerge.exe"),
        };
        candidates.AddRange((Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(d => Path.Combine(d, exe)));
        return candidates.FirstOrDefault(c => !string.IsNullOrWhiteSpace(c) && File.Exists(c));
    }

    public static bool CanEmbedInto(string videoFileName) =>
        Path.GetExtension(videoFileName).Equals(".mkv", StringComparison.OrdinalIgnoreCase);

    /// <summary>"pt-br" → "pt-BR"; "en" → "en".</summary>
    public static string ToIetf(string language)
    {
        var parts = language.Replace('_', '-').Split('-', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            return "und";
        }

        return parts.Length == 1 ? parts[0].ToLowerInvariant() : parts[0].ToLowerInvariant() + "-" + parts[1].ToUpperInvariant();
    }

    /// <summary>"pt-BR" → "Português (Brasil)", the name players show for the track.</summary>
    public static string TrackNameFor(string ietf)
    {
        try
        {
            var name = CultureInfo.GetCultureInfo(ietf).NativeName;
            return (name.Length > 0 ? char.ToUpper(name[0], CultureInfo.GetCultureInfo(ietf)) + name[1..] : ietf).Normalize();
        }
        catch (CultureNotFoundException)
        {
            return ietf;
        }
    }

    /// <summary>
    /// Text subtitle tracks the new one replaces: any text track in the same language, so saving
    /// twice never leaves two pt-BR tracks; and the track being edited when it has no language
    /// tag. Editing the English track and saving it as pt-BR keeps the English one. Image
    /// tracks (PGS/VobSub) are always kept.
    /// </summary>
    public static List<MkvTrack> TracksToReplace(MkvInfo info, string ietf, int? editedTrackNumber)
    {
        return info.Tracks
            .Where(t => t.IsTextSubtitle)
            .Where(t => t.LanguageIetf.Equals(ietf, StringComparison.OrdinalIgnoreCase) ||
                        (ietf.Equals("pt-BR", StringComparison.OrdinalIgnoreCase) && t.Language == "pob") ||
                        (t.Number == editedTrackNumber && string.IsNullOrEmpty(t.LanguageIetf) && t.Language is "" or "und"))
            .ToList();
    }

    public static List<string> BuildArguments(string output, string video, string subtitleFile, string ietf, string trackName, IEnumerable<MkvTrack> remove)
    {
        var args = new List<string> { "--output", output };
        var removeIds = remove.Select(t => t.Id.ToString(CultureInfo.InvariantCulture)).ToList();
        if (removeIds.Count > 0)
        {
            args.Add("--subtitle-tracks");
            args.Add("!" + string.Join(",", removeIds));
        }

        args.Add(video);
        args.AddRange(
        [
            "--language", "0:" + ietf,
            "--track-name", "0:" + trackName,
            "--default-track-flag", "0:yes",
            "--sub-charset", "0:UTF-8",
            subtitleFile,
        ]);
        return args;
    }

    public static MkvInfo ParseIdentify(string json)
    {
        var root = JsonNode.Parse(json) ?? throw new EmbedException("mkvmerge -J returned nothing");
        var duration = root["container"]?["properties"]?["duration"]?.GetValue<long>() ?? 0;
        var tracks = new List<MkvTrack>();
        foreach (var t in root["tracks"]?.AsArray() ?? [])
        {
            var p = t?["properties"];
            tracks.Add(new MkvTrack(
                t?["id"]?.GetValue<int>() ?? -1,
                p?["number"]?.GetValue<int>() ?? -1,
                t?["type"]?.GetValue<string>() ?? string.Empty,
                t?["codec"]?.GetValue<string>() ?? string.Empty,
                p?["language"]?.GetValue<string>() ?? string.Empty,
                p?["language_ietf"]?.GetValue<string>() ?? string.Empty,
                (p?["track_name"]?.GetValue<string>() ?? string.Empty).Normalize()));
        }

        return new MkvInfo(duration, tracks);
    }

    public static async Task<MkvInfo> IdentifyAsync(string mkvmerge, string file, CancellationToken token)
    {
        var (code, stdout, stderr) = await RunAsync(mkvmerge, ["-J", file], token);
        if (code > 1)
        {
            throw new EmbedException("mkvmerge could not read the video: " + (stderr.Length > 0 ? stderr : stdout));
        }

        return ParseIdentify(stdout);
    }

    /// <summary>Embeds <paramref name="subtitleFile"/> (UTF-8 SRT) into <paramref name="video"/>.</summary>
    public static async Task<EmbedResult> EmbedAsync(string mkvmerge, string video, string subtitleFile, string language, int? editedTrackNumber, CancellationToken token)
    {
        var ietf = ToIetf(language);
        var before = await IdentifyAsync(mkvmerge, video, token);
        var remove = TracksToReplace(before, ietf, editedTrackNumber);

        var folder = Path.GetDirectoryName(Path.GetFullPath(video))!;
        var temp = Path.Combine(folder, "." + Path.GetFileNameWithoutExtension(video) + ".sse-tmp.mkv");
        File.Delete(temp);
        try
        {
            var args = BuildArguments(temp, video, subtitleFile, ietf, TrackNameFor(ietf), remove);
            var (code, stdout, stderr) = await RunAsync(mkvmerge, args, token);
            if (code > 1 || !File.Exists(temp))
            {
                throw new EmbedException("mkvmerge failed: " + LastLines(stdout + stderr));
            }

            var after = await IdentifyAsync(mkvmerge, temp, token);
            var added = after.Tracks.LastOrDefault(t => t.IsTextSubtitle && t.LanguageIetf.Equals(ietf, StringComparison.OrdinalIgnoreCase));
            var problems = new List<string>();
            if (Math.Abs(after.DurationNs - before.DurationNs) > 1_000_000_000)
            {
                problems.Add($"duration {before.DurationNs / 1e9:0.0}s → {after.DurationNs / 1e9:0.0}s");
            }

            if (after.Count("video") != before.Count("video") || after.Count("audio") != before.Count("audio"))
            {
                problems.Add("video/audio tracks changed");
            }

            if (after.Count("subtitles") != before.Count("subtitles") - remove.Count + 1 || added == null)
            {
                problems.Add("the new subtitle track is missing");
            }

            if (problems.Count > 0)
            {
                throw new EmbedException("Check failed, the original was not touched: " + string.Join("; ", problems));
            }

            ReplaceFile(video, temp);
            return new EmbedResult(added!.Number, remove.Select(t => t.Number).ToList());
        }
        finally
        {
            try { File.Delete(temp); } catch { /* already moved or never written */ }
        }
    }

    /// <summary>Original aside, new file in place, original deleted; put back if the swap fails.</summary>
    private static void ReplaceFile(string video, string replacement)
    {
        var aside = video + ".sse-old";
        File.Delete(aside);
        File.Move(video, aside);
        try
        {
            File.Move(replacement, video);
        }
        catch
        {
            File.Move(aside, video);
            throw;
        }

        File.Delete(aside);
    }

    private static string LastLines(string text) =>
        string.Join(" ", text.Split('\n', StringSplitOptions.RemoveEmptyEntries).TakeLast(3)).Trim();

    private static async Task<(int Code, string Stdout, string Stderr)> RunAsync(string exe, IEnumerable<string> args, CancellationToken token)
    {
        var psi = new ProcessStartInfo(exe)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var a in args)
        {
            psi.ArgumentList.Add(a);
        }

        using var process = Process.Start(psi) ?? throw new EmbedException("Could not start mkvmerge");
        var stdout = process.StandardOutput.ReadToEndAsync(token);
        var stderr = process.StandardError.ReadToEndAsync(token);
        await process.WaitForExitAsync(token);
        return (process.ExitCode, await stdout, await stderr);
    }
}
