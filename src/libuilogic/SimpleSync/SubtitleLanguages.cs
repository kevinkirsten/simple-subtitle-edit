using System.Globalization;

namespace Nikse.SubtitleEdit.UiLogic.SimpleSync;

public sealed record SubtitleLanguage(string Ietf, string Name)
{
    public override string ToString() => $"{Name} ({Ietf})";
}

/// <summary>Languages offered when saving a subtitle into a video, and how mkv tracks name them.</summary>
public static class SubtitleLanguages
{
    private static readonly string[] Codes =
    [
        "pt-BR", "pt-PT", "en", "es", "es-419", "fr", "de", "it", "nl", "pl", "ru", "uk", "tr", "el", "ro", "hu", "cs",
        "sv", "da", "nb", "fi", "ja", "ko", "zh-Hans", "zh-Hant", "ar", "he", "hi", "th", "vi", "id",
    ];

    // mkv tracks often use ISO 639-2/B codes; CultureInfo gives the /T ones.
    private static readonly Dictionary<string, string> BibliographicToTerminology = new(StringComparer.OrdinalIgnoreCase)
    {
        ["fre"] = "fra", ["ger"] = "deu", ["dut"] = "nld", ["chi"] = "zho", ["cze"] = "ces", ["gre"] = "ell",
        ["per"] = "fas", ["rum"] = "ron", ["slo"] = "slk", ["alb"] = "sqi", ["arm"] = "hye", ["baq"] = "eus",
        ["bur"] = "mya", ["geo"] = "kat", ["ice"] = "isl", ["mac"] = "mkd", ["may"] = "msa", ["wel"] = "cym",
        ["pob"] = "por", ["nob"] = "nor", ["nno"] = "nor",
    };

    public static IReadOnlyList<SubtitleLanguage> All { get; } = Codes
        .Select(c => new SubtitleLanguage(c, MatroskaEmbedder.TrackNameFor(c)))
        .ToList();

    /// <summary>The list entry for a code, adding it if it is not one of the common ones.</summary>
    public static SubtitleLanguage For(string ietf)
    {
        ietf = MatroskaEmbedder.ToIetf(ietf);
        return All.FirstOrDefault(l => l.Ietf.Equals(ietf, StringComparison.OrdinalIgnoreCase))
               ?? new SubtitleLanguage(ietf, MatroskaEmbedder.TrackNameFor(ietf));
    }

    /// <summary>"pt-BR" → "por"; "de" → "deu".</summary>
    public static string ThreeLetter(string ietf)
    {
        try
        {
            return CultureInfo.GetCultureInfo(ietf.Split('-')[0]).ThreeLetterISOLanguageName;
        }
        catch (CultureNotFoundException)
        {
            return string.Empty;
        }
    }

    /// <summary>Whether an mkv track is in the same base language (any variant) as <paramref name="ietf"/>.</summary>
    public static bool SameBaseLanguage(MkvTrack track, string ietf)
    {
        var baseCode = ietf.Split('-')[0];
        if (!string.IsNullOrEmpty(track.LanguageIetf))
        {
            return track.LanguageIetf.Split('-')[0].Equals(baseCode, StringComparison.OrdinalIgnoreCase);
        }

        var three = BibliographicToTerminology.TryGetValue(track.Language, out var t) ? t : track.Language;
        var wanted = ThreeLetter(ietf);
        return wanted.Length > 0 && three.Equals(wanted, StringComparison.OrdinalIgnoreCase) ||
               (wanted == "nob" && three == "nor");
    }
}

/// <summary>What is already inside the video for a language, for the warning in the save dialog.</summary>
public sealed record ExistingTracks(IReadOnlyList<MkvTrack> Replaced, IReadOnlyList<MkvTrack> Kept)
{
    public static ExistingTracks For(MkvInfo info, string ietf, int? editedTrackNumber)
    {
        var replaced = MatroskaEmbedder.TracksToReplace(info, ietf, editedTrackNumber);
        var kept = info.Tracks
            .Where(t => t.Type == "subtitles" && !replaced.Contains(t) && SubtitleLanguages.SameBaseLanguage(t, ietf))
            .ToList();
        return new ExistingTracks(replaced, kept);
    }

    public static string Describe(MkvTrack t, string textWord = "text", string imageWord = "image")
    {
        var kind = t.IsTextSubtitle ? textWord : imageWord;
        var name = !string.IsNullOrWhiteSpace(t.Name) ? t.Name : !string.IsNullOrWhiteSpace(t.LanguageIetf) ? t.LanguageIetf : t.Language;
        return $"#{t.Number} {name} ({kind})";
    }
}
