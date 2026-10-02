using System.Diagnostics;
using Nikse.SubtitleEdit.UiLogic.SimpleSync;

namespace LibUiLogicTests.SimpleSync;

public class MatroskaEmbedderTests
{
    private const string IdentifyJson = """
        {"container":{"properties":{"duration":3601557000000}},"tracks":[
          {"id":0,"type":"video","codec":"HEVC/H.265/MPEG-H","properties":{"number":1,"language":"und"}},
          {"id":1,"type":"audio","codec":"AAC","properties":{"number":2,"language":"eng"}},
          {"id":2,"type":"subtitles","codec":"HDMV PGS","properties":{"number":3,"language":"por"}},
          {"id":3,"type":"subtitles","codec":"SubRip/SRT","properties":{"number":4,"language":"por","language_ietf":"pt-BR","track_name":"Português (Brasil)"}},
          {"id":4,"type":"subtitles","codec":"SubRip/SRT","properties":{"number":5,"language":"eng","language_ietf":"en"}}
        ]}
        """;

    [Theory]
    [InlineData("pt-br", "pt-BR")]
    [InlineData("pt_BR", "pt-BR")]
    [InlineData("EN", "en")]
    [InlineData("", "und")]
    public void ToIetf_NormalizesCase(string input, string expected) => Assert.Equal(expected, MatroskaEmbedder.ToIetf(input));

    [Fact]
    public void TrackNameFor_UsesTheLanguagesOwnName()
    {
        Assert.Equal("Português (Brasil)", MatroskaEmbedder.TrackNameFor("pt-BR"));
        Assert.Equal("English", MatroskaEmbedder.TrackNameFor("en"));
    }

    [Fact]
    public void ParseIdentify_ReadsDurationAndTracks()
    {
        var info = MatroskaEmbedder.ParseIdentify(IdentifyJson);

        Assert.Equal(3601557000000, info.DurationNs);
        Assert.Equal(3, info.Count("subtitles"));
        Assert.False(info.Tracks[2].IsTextSubtitle); // PGS is an image track
        Assert.True(info.Tracks[3].IsTextSubtitle);
    }

    [Fact]
    public void TracksToReplace_SameLanguageTextTrack_NeverTheImageOne()
    {
        var info = MatroskaEmbedder.ParseIdentify(IdentifyJson);

        var remove = MatroskaEmbedder.TracksToReplace(info, "pt-BR", editedTrackNumber: null);

        Assert.Equal([4], remove.Select(t => t.Number)); // the pt-BR SRT; the PGS "por" stays
    }

    [Fact]
    public void TracksToReplace_EditingAnotherLanguageTrack_KeepsIt()
    {
        var info = MatroskaEmbedder.ParseIdentify(IdentifyJson);

        var remove = MatroskaEmbedder.TracksToReplace(info, "pt-BR", editedTrackNumber: 5); // 5 is English

        Assert.Equal([4], remove.Select(t => t.Number));
    }

    [Fact]
    public void TracksToReplace_EditedTrackWithoutLanguage_IsReplaced()
    {
        var info = new MkvInfo(1, [new MkvTrack(2, 3, "subtitles", "SubRip/SRT", "und", "", "")]);

        Assert.Equal([3], MatroskaEmbedder.TracksToReplace(info, "pt-BR", editedTrackNumber: 3).Select(t => t.Number));
        Assert.Empty(MatroskaEmbedder.TracksToReplace(info, "pt-BR", editedTrackNumber: null));
    }

    [Fact]
    public void BuildArguments_DropsReplacedTracks_AndTagsTheNewOne()
    {
        var info = MatroskaEmbedder.ParseIdentify(IdentifyJson);

        var args = MatroskaEmbedder.BuildArguments("out.mkv", "in.mkv", "sub.srt", "pt-BR", "Português (Brasil)", [info.Tracks[3]]);

        Assert.Equal(
        [
            "--output", "out.mkv", "--subtitle-tracks", "!3", "in.mkv",
            "--language", "0:pt-BR", "--track-name", "0:Português (Brasil)", "--default-track-flag", "0:yes", "--sub-charset", "0:UTF-8", "sub.srt",
        ], args);
    }

    private sealed class SyncProgress(Action<int> report) : IProgress<int>
    {
        public void Report(int value) => report(value);
    }

    [Theory]
    [InlineData("#GUI#progress 45%", 45)]
    [InlineData("Progress: 100%", 100)]
    [InlineData("#GUI#warning something", null)]
    public void ParseProgress_ReadsMkvmergeOutput(string line, int? expected)
    {
        Assert.Equal(expected, MatroskaEmbedder.ParseProgress(line));
    }

    [Fact]
    public void CanEmbedInto_OnlyMkv()
    {
        Assert.True(MatroskaEmbedder.CanEmbedInto("a.MKV"));
        Assert.False(MatroskaEmbedder.CanEmbedInto("a.mp4"));
    }

    [Fact]
    public async Task Embed_RealMkv_AddsPtBrTrack_AndSavingAgainReplacesIt()
    {
        var mkvmerge = MatroskaEmbedder.FindMkvmerge();
        var ffmpeg = new[] { "/opt/homebrew/bin/ffmpeg", "/usr/local/bin/ffmpeg", "/usr/bin/ffmpeg" }.FirstOrDefault(File.Exists);
        if (mkvmerge == null || ffmpeg == null)
        {
            Assert.Skip("mkvmerge and ffmpeg are needed for this test");
        }

        var dir = Directory.CreateTempSubdirectory("sse-embed-");
        try
        {
            var video = Path.Combine(dir.FullName, "ep.mkv");
            var make = Process.Start(ffmpeg, ["-v", "error", "-y", "-f", "lavfi", "-i", "testsrc=size=160x90:rate=10", "-f", "lavfi", "-i", "sine", "-t", "6", "-c:v", "libx264", "-c:a", "aac", video])!;
            await make.WaitForExitAsync(TestContext.Current.CancellationToken);
            var srt = Path.Combine(dir.FullName, "sub.srt");
            File.WriteAllText(srt, "1\n00:00:01,000 --> 00:00:02,000\nOlá\n");

            var reported = new List<int>();
            var first = await MatroskaEmbedder.EmbedAsync(mkvmerge, video, srt, "pt-br", null, TestContext.Current.CancellationToken, new SyncProgress(reported.Add));
            File.WriteAllText(srt, "1\n00:00:02,000 --> 00:00:03,000\nDe novo\n");
            var second = await MatroskaEmbedder.EmbedAsync(mkvmerge, video, srt, "pt-br", first.TrackNumber, TestContext.Current.CancellationToken);

            var info = await MatroskaEmbedder.IdentifyAsync(mkvmerge, video, TestContext.Current.CancellationToken);
            var subtitles = info.Tracks.Where(t => t.Type == "subtitles").ToList();
            var track = Assert.Single(subtitles);
            Assert.Equal("pt-BR", track.LanguageIetf);
            Assert.Equal("Português (Brasil)", track.Name);
            Assert.Equal([first.TrackNumber], second.RemovedTrackNumbers);
            Assert.Contains(100, reported); // mkvmerge reported progress up to the end
            Assert.Empty(Directory.GetFiles(dir.FullName, "*sse-*", SearchOption.AllDirectories)); // no temp leftovers

            // The text inside is the second version, readable by the app's own loader.
            var loaded = SubtitleSourceLoader.Load(new SubtitleSource(SubtitleSourceKind.Matroska, "x", video, track.Number));
            Assert.Equal("De novo", loaded!.Paragraphs[0].Text);
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }
}
