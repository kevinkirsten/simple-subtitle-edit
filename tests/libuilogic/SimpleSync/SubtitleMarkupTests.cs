using Nikse.SubtitleEdit.UiLogic.SimpleSync;

namespace LibUiLogicTests.SimpleSync;

public class SubtitleMarkupTests
{
    [Fact]
    public void Parse_ItalicLines_BecomeItalicRuns_WithoutTags()
    {
        var runs = SubtitleMarkup.Parse("<i>Na manhã em que passei mal</i>\n<i>eu pensava...</i>");

        var run = Assert.Single(runs);
        Assert.Equal("Na manhã em que passei mal\neu pensava...", run.Text);
        Assert.True(run.Italic);
    }

    [Fact]
    public void Parse_MixedStyles()
    {
        var runs = SubtitleMarkup.Parse("Ele disse <b>não</b>, <i>e <u>saiu</u></i>.");

        Assert.Equal(
        [
            new StyledRun("Ele disse ", false, false, false),
            new StyledRun("não", false, true, false),
            new StyledRun(", ", false, false, false),
            new StyledRun("e ", true, false, false),
            new StyledRun("saiu", true, false, true),
            new StyledRun(".", false, false, false),
        ], runs);
    }

    [Fact]
    public void Parse_AssTagsAndFontTags()
    {
        var runs = SubtitleMarkup.Parse("{\\an8}{\\i1}Topo{\\i0} <font color=\"#ffff00\">amarelo</font>");

        Assert.Equal([new StyledRun("Topo", true, false, false), new StyledRun(" amarelo", false, false, false)], runs);
    }

    [Theory]
    [InlineData("<i>Sei lá...</i>", "Sei lá...")]
    [InlineData("<font color=\"red\">Oi</font>", "Oi")]
    [InlineData("", "")]
    public void Plain_RemovesTags(string input, string expected) => Assert.Equal(expected, SubtitleMarkup.Plain(input));
}
