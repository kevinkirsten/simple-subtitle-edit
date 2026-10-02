using Nikse.SubtitleEdit.Core.Common;
using System.Text.RegularExpressions;

namespace Nikse.SubtitleEdit.UiLogic.SimpleSync;

/// <summary>What an online subtitle search needs to know about a video, read from its file.</summary>
public sealed record VideoQuery(string FileName, string MovieHash, string Title, int? Year, int? Season, int? Episode)
{
    private static readonly Regex EpisodePattern = new(@"[Ss](?<s>\d{1,2})[ ._-]?[Ee](?<e>\d{1,3})", RegexOptions.Compiled);
    private static readonly Regex YearPattern = new(@"[\(\[ .](?<y>(19|20)\d{2})[\)\] .]", RegexOptions.Compiled);
    private static readonly Regex Braces = new(@"\{[^}]*\}|\[[^\]]*\]", RegexOptions.Compiled);

    public bool IsEpisode => Season != null && Episode != null;

    public static VideoQuery FromFile(string videoFileName)
    {
        var hash = string.Empty;
        try
        {
            hash = MovieHasher.GenerateHash(videoFileName);
        }
        catch
        {
            // Unreadable or tiny file: search by name only.
        }

        return Parse(Path.GetFileNameWithoutExtension(videoFileName), hash, videoFileName);
    }

    /// <summary>"The Sopranos (1999) - S01E01 - Pilot [Bluray-1080p]" → title, year, season, episode.</summary>
    public static VideoQuery Parse(string name, string movieHash = "", string fileName = "")
    {
        var clean = Braces.Replace(name, " ");
        int? season = null, episode = null, year = null;

        var titlePart = clean;
        var ep = EpisodePattern.Match(clean);
        if (ep.Success)
        {
            season = int.Parse(ep.Groups["s"].Value);
            episode = int.Parse(ep.Groups["e"].Value);
            titlePart = clean[..ep.Index];
        }

        var y = YearPattern.Match(" " + titlePart + " ");
        if (y.Success)
        {
            year = int.Parse(y.Groups["y"].Value);
            titlePart = (" " + titlePart + " ")[..y.Index];
        }
        else if (!ep.Success)
        {
            // Movie without a year in the name: stop at the first release tag.
            var tag = Regex.Match(titlePart, @"\b(1080p|2160p|720p|480p|bluray|web-?dl|webrip|hdtv|x26[45]|hevc)\b", RegexOptions.IgnoreCase);
            if (tag.Success)
            {
                titlePart = titlePart[..tag.Index];
            }
        }

        var title = Regex.Replace(titlePart.Replace('.', ' ').Replace('_', ' '), @"[\s\-\(\)]+", " ").Trim();
        return new VideoQuery(fileName, movieHash, title, year, season, episode);
    }
}
