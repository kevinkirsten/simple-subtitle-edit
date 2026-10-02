using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.UiLogic.SimpleSync;

namespace LibUiLogicTests.SimpleSync;

public class SyncSessionTests
{
    private static Subtitle MakeSubtitle() => new(
    [
        new Paragraph("Primeira", 1000, 3000),
        new Paragraph("Segunda", 5000, 7000),
        new Paragraph("Terceira", 20000, 22500),
    ]);

    [Fact]
    public void Nudge_AccumulatesAndRoundsToMilliseconds()
    {
        var session = new SyncSession(MakeSubtitle());

        session.Nudge(SyncSession.SmallStep);
        session.Nudge(SyncSession.SmallStep);
        session.Nudge(SyncSession.SmallStep);

        Assert.Equal(0.3, session.OffsetSeconds);
        Assert.True(session.HasChanges);
    }

    [Fact]
    public void ResetOffset_ClearsChanges()
    {
        var session = new SyncSession(MakeSubtitle());
        session.Nudge(-1.5);

        session.ResetOffset();

        Assert.Equal(0, session.OffsetSeconds);
        Assert.False(session.HasChanges);
    }

    [Theory]
    [InlineData(0.5, null)]
    [InlineData(1.0, "Primeira")]
    [InlineData(2.999, "Primeira")]
    [InlineData(4.0, null)]
    [InlineData(6.0, "Segunda")]
    [InlineData(21.0, "Terceira")]
    [InlineData(30.0, null)]
    public void ActiveAt_FindsTheLineOnScreen(double position, string? expected)
    {
        var session = new SyncSession(MakeSubtitle());

        Assert.Equal(expected, session.ActiveAt(position)?.Text);
    }

    [Fact]
    public void ActiveAt_FollowsTheOffset()
    {
        var session = new SyncSession(MakeSubtitle());
        session.SetOffset(2);

        Assert.Null(session.ActiveAt(1.5));
        Assert.Equal("Primeira", session.ActiveAt(3.5)?.Text);
    }

    [Fact]
    public void ActiveAt_OverlappingLines_ReturnsTheOneStillOnScreen()
    {
        var session = new SyncSession(new Subtitle(
        [
            new Paragraph("Longa", 0, 10000),
            new Paragraph("Curta", 2000, 3000),
        ]));

        Assert.Equal("Longa", session.ActiveAt(5).Text);
    }

    [Fact]
    public void VisibleBlocks_ReturnsOnlyLinesInsideTheWindow_Shifted()
    {
        var session = new SyncSession(MakeSubtitle());
        session.SetOffset(1);

        var blocks = session.VisibleBlocks(5, 10).ToList();

        var block = Assert.Single(blocks);
        Assert.Equal("Segunda", block.Paragraph.Text);
        Assert.Equal(6, block.Start);
        Assert.Equal(8, block.End);
    }

    [Fact]
    public void Coverage_MarksBucketsWithText()
    {
        var session = new SyncSession(MakeSubtitle());

        // 25 s in 5 buckets of 5 s: text at 1-3 s, 5-7 s and 20-22.5 s.
        var coverage = session.Coverage(25, 5);

        Assert.Equal([true, true, false, false, true], coverage);
    }

    [Fact]
    public void Coverage_InvalidInput_ReturnsEmptyOrAllFalse()
    {
        var session = new SyncSession(MakeSubtitle());

        Assert.Empty(session.Coverage(25, 0));
        Assert.All(session.Coverage(0, 3), Assert.False);
    }

    [Fact]
    public void BuildShifted_AppliesOffsetWithoutTouchingTheOriginal()
    {
        var original = MakeSubtitle();
        var session = new SyncSession(original);
        session.SetOffset(1.25);

        var shifted = session.BuildShifted();

        Assert.Equal(2250, shifted.Paragraphs[0].StartTime.TotalMilliseconds);
        Assert.Equal(1000, original.Paragraphs[0].StartTime.TotalMilliseconds);
    }

    [Fact]
    public void BuildShifted_NegativeOffset_ClampsAtZero()
    {
        var session = new SyncSession(MakeSubtitle());
        session.SetOffset(-2);

        var shifted = session.BuildShifted();

        Assert.Equal(0, shifted.Paragraphs[0].StartTime.TotalMilliseconds);
        Assert.Equal(1000, shifted.Paragraphs[0].EndTime.TotalMilliseconds);
    }

    [Fact]
    public void OutputPathFor_SameFolderSameNameSrt()
    {
        var video = Path.Combine("pasta", "Série - S01E01.mkv");

        Assert.Equal(Path.Combine("pasta", "Série - S01E01.srt"), SyncSession.OutputPathFor(video));
    }

    [Fact]
    public void Save_WritesSrtNextToVideo_AndBacksUpExistingFile()
    {
        var dir = Directory.CreateTempSubdirectory("sse-save-");
        try
        {
            var video = Path.Combine(dir.FullName, "ep.mkv");
            File.WriteAllText(video, "");
            File.WriteAllText(Path.Combine(dir.FullName, "ep.srt"), "antiga");

            var session = new SyncSession(MakeSubtitle());
            session.SetOffset(0.5);
            var result = session.Save(video);

            Assert.Equal(Path.Combine(dir.FullName, "ep.srt"), result.OutputFileName);
            Assert.Equal("antiga", File.ReadAllText(result.BackupFileName!));
            var saved = Subtitle.Parse(result.OutputFileName);
            Assert.Equal(3, saved.Paragraphs.Count);
            Assert.Equal(1500, saved.Paragraphs[0].StartTime.TotalMilliseconds);
            Assert.Equal("Primeira", saved.Paragraphs[0].Text);
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    [Theory]
    [InlineData(0, "+0.000s")]
    [InlineData(1.25, "+1.250s")]
    [InlineData(-0.1, "-0.100s")]
    public void FormatOffset_IsSignedAndInvariant(double seconds, string expected)
    {
        Assert.Equal(expected, SyncSession.FormatOffset(seconds));
    }

    [Theory]
    [InlineData(0, "00:00.000")]
    [InlineData(75.5, "01:15.500")]
    [InlineData(3725.042, "1:02:05.042")]
    [InlineData(-3, "00:00.000")]
    public void FormatTime_ShowsHoursOnlyWhenNeeded(double seconds, string expected)
    {
        Assert.Equal(expected, SyncSession.FormatTime(seconds));
    }
}

public class SyncSessionTrimTests
{
    [Fact]
    public void BuildShifted_WithDuration_DropsLinesAfterTheEnd_AndCutsTheLastOne()
    {
        var session = new SyncSession(new Subtitle(
        [
            new Paragraph("Dentro", 1000, 2000),
            new Paragraph("Cortada", 9000, 12000),
            new Paragraph("Depois do fim", 15000, 16000),
        ]));

        var shifted = session.BuildShifted(videoDurationSeconds: 10);

        Assert.Equal(["Dentro", "Cortada"], shifted.Paragraphs.Select(p => p.Text));
        Assert.Equal(10000, shifted.Paragraphs[1].EndTime.TotalMilliseconds);
        Assert.Equal(2, shifted.Paragraphs[1].Number);
    }

    [Fact]
    public void BuildShifted_WithoutDuration_KeepsEverything()
    {
        var session = new SyncSession(new Subtitle([new Paragraph("Longe", 99000, 100000)]));

        Assert.Single(session.BuildShifted().Paragraphs);
    }
}
