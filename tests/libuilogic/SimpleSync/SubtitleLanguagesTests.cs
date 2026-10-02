using Nikse.SubtitleEdit.UiLogic.SimpleSync;

namespace LibUiLogicTests.SimpleSync;

public class SubtitleLanguagesTests
{
    private static MkvTrack Sub(int number, string codec, string language, string ietf = "", string name = "") =>
        new(number - 1, number, "subtitles", codec, language, ietf, name);

    private static readonly MkvInfo Sopranos = new(1, [
        new MkvTrack(0, 1, "video", "HEVC", "und", "", ""),
        Sub(3, "HDMV PGS", "eng"),
        Sub(6, "HDMV PGS", "fre"),
        Sub(14, "HDMV PGS", "por"),
        Sub(15, "SubRip/SRT", "por", "pt-BR", "Português (Brasil)"),
        Sub(16, "SubRip/SRT", "por", "pt-PT", "Português (Portugal)"),
    ]);

    [Fact]
    public void All_HasNativeNames()
    {
        Assert.Contains(SubtitleLanguages.All, l => l.Ietf == "pt-BR" && l.Name == "Português (Brasil)");
        Assert.Contains(SubtitleLanguages.All, l => l.Ietf == "de" && l.Name == "Deutsch");
        Assert.Equal("Português (Brasil) (pt-BR)", SubtitleLanguages.For("pt-br").ToString());
    }

    [Fact]
    public void For_UnknownCode_IsStillOffered()
    {
        Assert.Equal("ca", SubtitleLanguages.For("ca").Ietf);
    }

    [Fact]
    public void ExistingTracks_PtBr_ReplacesTheTextTrack_KeepsImageAndOtherVariant()
    {
        var existing = ExistingTracks.For(Sopranos, "pt-BR", editedTrackNumber: null);

        Assert.Equal([15], existing.Replaced.Select(t => t.Number));
        Assert.Equal([14, 16], existing.Kept.Select(t => t.Number));
    }

    [Fact]
    public void ExistingTracks_French_MatchesTheBibliographicCode()
    {
        var existing = ExistingTracks.For(Sopranos, "fr", editedTrackNumber: null);

        Assert.Empty(existing.Replaced);
        Assert.Equal([6], existing.Kept.Select(t => t.Number));
    }

    [Fact]
    public void ExistingTracks_LanguageNotInside_IsEmpty()
    {
        var existing = ExistingTracks.For(Sopranos, "ja", editedTrackNumber: null);

        Assert.Empty(existing.Replaced);
        Assert.Empty(existing.Kept);
    }

    [Fact]
    public void Describe_NamesTheTrack()
    {
        Assert.Equal("#15 Português (Brasil) (text)", ExistingTracks.Describe(Sopranos.Tracks[4]));
        Assert.Equal("#14 por (image)", ExistingTracks.Describe(Sopranos.Tracks[3]));
    }
}
