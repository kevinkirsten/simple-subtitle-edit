using Nikse.SubtitleEdit.UiLogic.SimpleSync;

namespace LibUiLogicTests.SimpleSync;

public class VideoPlaylistTests : IDisposable
{
    private readonly DirectoryInfo _root = Directory.CreateTempSubdirectory("sse-playlist-");

    public void Dispose() => _root.Delete(recursive: true);

    private string Touch(string relative)
    {
        var path = Path.Combine(_root.FullName, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "");
        return Path.GetFullPath(path);
    }

    [Fact]
    public void FromFolder_FindsVideosInSeasonFolders_InEpisodeOrder()
    {
        Touch("Season 02/Show - S02E01.mkv");
        Touch("Season 01/Show - S01E10.mkv");
        Touch("Season 01/Show - S01E02.mkv");
        Touch("Season 01/Show - S01E02.srt");
        Touch("Season 01/._Show - S01E02.mkv");
        Touch("Season 10/Show - S10E01.mp4");

        var playlist = VideoPlaylist.FromFolder(_root.FullName);

        Assert.Equal(
            ["Show - S01E02.mkv", "Show - S01E10.mkv", "Show - S02E01.mkv", "Show - S10E01.mp4"],
            playlist.Files.Select(Path.GetFileName));
        Assert.Equal(0, playlist.Index);
        Assert.Equal("1/4 · " + Path.Combine("Season 01", "Show - S01E02.mkv"), playlist.Describe());
    }

    [Fact]
    public void NextAndPrevious_StopAtTheEnds()
    {
        Touch("a1.mkv");
        Touch("a2.mkv");
        var playlist = VideoPlaylist.FromFolder(_root.FullName);

        Assert.False(playlist.HasPrevious);
        Assert.EndsWith("a2.mkv", playlist.MoveNext());
        Assert.False(playlist.HasNext);
        Assert.EndsWith("a2.mkv", playlist.MoveNext());
        Assert.EndsWith("a1.mkv", playlist.MovePrevious());
        Assert.EndsWith("a1.mkv", playlist.MovePrevious());
    }

    [Fact]
    public void FromVideo_PositionsOnThatVideo_WithoutSubfolders()
    {
        Touch("ep1.mkv");
        var second = Touch("ep2.mkv");
        Touch("extras/making-of.mkv");

        var playlist = VideoPlaylist.FromVideo(second);

        Assert.Equal(2, playlist.Files.Count);
        Assert.Equal(1, playlist.Index);
        Assert.Equal(second, playlist.Current);
    }

    [Fact]
    public void FromFolder_EmptyOrMissing_HasNoCurrent()
    {
        Assert.Null(VideoPlaylist.FromFolder(_root.FullName).Current);
        Assert.Null(VideoPlaylist.FromFolder(Path.Combine(_root.FullName, "nope")).Current);
    }

    [Theory]
    [InlineData("E2", "E10", -1)]
    [InlineData("E10", "E2", 1)]
    [InlineData("e02", "E2", 0)]
    [InlineData("Season 9", "Season 10", -1)]
    [InlineData("abc", "abd", -1)]
    public void NaturalCompare_OrdersNumbersByValue(string a, string b, int sign)
    {
        Assert.Equal(sign, Math.Sign(VideoPlaylist.NaturalCompare(a, b)));
    }
}
