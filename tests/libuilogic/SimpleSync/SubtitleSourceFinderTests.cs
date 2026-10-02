using Nikse.SubtitleEdit.UiLogic.SimpleSync;

namespace LibUiLogicTests.SimpleSync;

public class SubtitleSourceFinderTests
{
    private const string Srt = "1\n00:00:01,000 --> 00:00:02,000\nOi\n\n2\n00:00:03,000 --> 00:00:04,000\nTchau\n";

    [Theory]
    [InlineData("ep01", "ep01", 0)]
    [InlineData("EP01", "ep01", 0)]
    [InlineData("ep01.pt-BR", "ep01", 1)]
    [InlineData("outra", "ep01", 2)]
    public void RankFor_PrefersSubtitlesNamedLikeTheVideo(string subtitle, string video, int expected)
    {
        Assert.Equal(expected, SubtitleSourceFinder.RankFor(subtitle, video));
    }

    [Fact]
    public void FindFiles_OrdersByNameMatch_AndSkipsMacMetadataAndNonSubtitles()
    {
        var dir = Directory.CreateTempSubdirectory("sse-find-");
        try
        {
            var video = Path.Combine(dir.FullName, "ep01.mkv");
            File.WriteAllText(video, "");
            File.WriteAllText(Path.Combine(dir.FullName, "zzz.srt"), Srt);
            File.WriteAllText(Path.Combine(dir.FullName, "ep01.pt-BR.srt"), Srt);
            File.WriteAllText(Path.Combine(dir.FullName, "ep01.srt"), Srt);
            File.WriteAllText(Path.Combine(dir.FullName, "._ep01.srt"), "lixo do exFAT");
            File.WriteAllText(Path.Combine(dir.FullName, "capa.jpg"), "");

            var names = SubtitleSourceFinder.FindFiles(video).Select(s => s.DisplayName).ToList();

            Assert.Equal(["ep01.srt", "ep01.pt-BR.srt", "zzz.srt"], names);
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    [Fact]
    public void FindFiles_SkipsSubtitlesOfOtherVideosInTheSameFolder()
    {
        var dir = Directory.CreateTempSubdirectory("sse-find-");
        try
        {
            var video = Path.Combine(dir.FullName, "Show - S01E01.mkv");
            File.WriteAllText(video, "");
            File.WriteAllText(Path.Combine(dir.FullName, "Show - S01E02.mkv"), "");
            File.WriteAllText(Path.Combine(dir.FullName, "Show - S01E01.srt"), Srt);
            File.WriteAllText(Path.Combine(dir.FullName, "Show - S01E02.srt"), Srt);
            File.WriteAllText(Path.Combine(dir.FullName, "Show - S01E02.pt-BR.srt"), Srt);
            File.WriteAllText(Path.Combine(dir.FullName, "loose.srt"), Srt);

            var names = SubtitleSourceFinder.FindFiles(video).Select(s => s.DisplayName).ToList();

            Assert.Equal(["Show - S01E01.srt", "loose.srt"], names);
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    [Fact]
    public void FindFiles_MissingFolder_ReturnsEmpty()
    {
        Assert.Empty(SubtitleSourceFinder.FindFiles(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "x.mkv")));
    }

    [Fact]
    public void FindEmbedded_NotAContainer_ReturnsEmpty()
    {
        var file = Path.GetTempFileName();
        try
        {
            File.WriteAllText(file, "isto não é um mkv");
            var mkv = Path.ChangeExtension(file, ".mkv");
            File.Move(file, mkv);
            file = mkv;

            Assert.Empty(SubtitleSourceFinder.FindEmbedded(mkv));
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Theory]
    [InlineData("MKV", 3, "por", "Forced", "MKV #3 · por · Forced")]
    [InlineData("MKV", 2, "und", "", "MKV #2")]
    [InlineData("MP4", 1, null, null, "MP4 #1")]
    public void MakeTrackName_SkipsEmptyParts(string container, int track, string? lang, string? name, string expected)
    {
        Assert.Equal(expected, SubtitleSourceFinder.MakeTrackName(container, track, lang, name));
    }

    [Fact]
    public void Loader_LoadsAnSrtFile_SortedAndRenumbered()
    {
        var file = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".srt");
        try
        {
            File.WriteAllText(file, "1\n00:00:05,000 --> 00:00:06,000\nDepois\n\n2\n00:00:01,000 --> 00:00:02,000\nAntes\n");

            var subtitle = SubtitleSourceLoader.Load(new SubtitleSource(SubtitleSourceKind.File, "x", file));

            Assert.NotNull(subtitle);
            Assert.Equal("Antes", subtitle.Paragraphs[0].Text);
            Assert.Equal(1, subtitle.Paragraphs[0].Number);
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public void Loader_EmptyFile_ReturnsNull()
    {
        var file = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".srt");
        try
        {
            File.WriteAllText(file, "");
            Assert.Null(SubtitleSourceLoader.Load(new SubtitleSource(SubtitleSourceKind.File, "x", file)));
        }
        finally
        {
            File.Delete(file);
        }
    }
}
