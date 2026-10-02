using Nikse.SubtitleEdit.Core.ContainerFormats.Matroska;
using Nikse.SubtitleEdit.Core.ContainerFormats.Mp4;

namespace Nikse.SubtitleEdit.UiLogic.SimpleSync;

/// <summary>
/// Lists the subtitles that can go with a video: files in the same folder (the ones named like
/// the video first) and text tracks embedded in mkv/mp4 files.
/// </summary>
public static class SubtitleSourceFinder
{
    public static readonly string[] SubtitleFileExtensions =
    [
        ".srt", ".ass", ".ssa", ".vtt", ".sub", ".smi", ".sami", ".sbv", ".ttml", ".dfxp", ".xml", ".txt", ".stl", ".lrc",
    ];

    // Image based tracks need OCR, which the simple window does not do.
    private static readonly string[] ImageCodecIds = ["S_HDMV/PGS", "S_DVBSUB", "S_VOBSUB", "S_HDMV/TEXTST"];

    public static List<SubtitleSource> Find(string videoFileName)
    {
        var result = new List<SubtitleSource>();
        result.AddRange(FindFiles(videoFileName));
        result.AddRange(FindEmbedded(videoFileName));
        return result;
    }

    public static List<SubtitleSource> FindFiles(string videoFileName)
    {
        var folder = Path.GetDirectoryName(Path.GetFullPath(videoFileName));
        if (folder == null || !Directory.Exists(folder))
        {
            return [];
        }

        var videoBase = Path.GetFileNameWithoutExtension(videoFileName);
        var files = Directory.EnumerateFiles(folder)
            .Where(f => SubtitleFileExtensions.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase))
            .Where(f => !Path.GetFileName(f).StartsWith("._", StringComparison.Ordinal)) // macOS metadata on exFAT
            .Where(f => !IsBigTextFile(f))
            .Select(f => new
            {
                File = f,
                Rank = RankFor(Path.GetFileNameWithoutExtension(f), videoBase),
            })
            .OrderBy(x => x.Rank)
            .ThenBy(x => Path.GetFileName(x.File), StringComparer.OrdinalIgnoreCase);

        return files
            .Select(x => new SubtitleSource(SubtitleSourceKind.File, Path.GetFileName(x.File), x.File))
            .ToList();
    }

    public static List<SubtitleSource> FindEmbedded(string videoFileName)
    {
        var ext = Path.GetExtension(videoFileName).ToLowerInvariant();
        try
        {
            if (ext is ".mkv" or ".mka" or ".webm" or ".mks")
            {
                using var matroska = new MatroskaFile(videoFileName);
                if (!matroska.IsValid)
                {
                    return [];
                }

                return matroska.GetTracks(subtitleOnly: true)
                    .Where(t => !ImageCodecIds.Contains(t.CodecId, StringComparer.OrdinalIgnoreCase))
                    .Select(t => new SubtitleSource(
                        SubtitleSourceKind.Matroska,
                        MakeTrackName("MKV", t.TrackNumber, t.Language, t.Name),
                        videoFileName,
                        t.TrackNumber,
                        t.Language ?? string.Empty))
                    .ToList();
            }

            if (ext is ".mp4" or ".m4v" or ".mov")
            {
                var mp4 = new MP4Parser(videoFileName);
                var tracks = mp4.GetSubtitleTracks();
                return tracks
                    .Select((t, i) => new SubtitleSource(SubtitleSourceKind.Mp4, MakeTrackName("MP4", i + 1, string.Empty, string.Empty), videoFileName, i))
                    .ToList();
            }
        }
        catch
        {
            // A broken or unusual container only means "no embedded subtitles" here.
        }

        return [];
    }

    /// <summary>0 = same name as the video, 1 = starts with the video name (e.g. ".pt-BR"), 2 = anything else.</summary>
    public static int RankFor(string subtitleBaseName, string videoBaseName)
    {
        if (subtitleBaseName.Equals(videoBaseName, StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        return subtitleBaseName.StartsWith(videoBaseName, StringComparison.OrdinalIgnoreCase) ? 1 : 2;
    }

    public static string MakeTrackName(string container, int trackNumber, string? language, string? name)
    {
        var parts = new List<string> { $"{container} #{trackNumber}" };
        if (!string.IsNullOrWhiteSpace(language) && language != "und")
        {
            parts.Add(language);
        }

        if (!string.IsNullOrWhiteSpace(name))
        {
            parts.Add(name);
        }

        return string.Join(" · ", parts);
    }

    // .txt/.xml are only subtitles when small; skip big logs or databases sitting next to a video.
    private static bool IsBigTextFile(string fileName)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return ext is ".txt" or ".xml" && new FileInfo(fileName).Length > 5_000_000;
    }
}
