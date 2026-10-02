using Nikse.SubtitleEdit.Core.Common;
using System.Text.RegularExpressions;

namespace Nikse.SubtitleEdit.UiLogic.SimpleSync;

/// <summary>A piece of subtitle text with the style the SRT tags give it.</summary>
public sealed record StyledRun(string Text, bool Italic, bool Bold, bool Underline);

/// <summary>
/// Turns subtitle text with formatting tags (&lt;i&gt;, &lt;b&gt;, &lt;u&gt;, &lt;font&gt;, {\i1}…) into
/// styled pieces for display. Saving keeps the original text and tags untouched.
/// </summary>
public static class SubtitleMarkup
{
    private static readonly Regex Tags = new(@"<\s*(/?)\s*(i|b|u)\s*>|\{\\(i|b|u)([01])\}", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>Plain text, all tags removed: for the timeline blocks.</summary>
    public static string Plain(string text) => HtmlUtil.RemoveHtmlTags(text ?? string.Empty, alsoSsaTags: true);

    public static List<StyledRun> Parse(string text)
    {
        var runs = new List<StyledRun>();
        if (string.IsNullOrEmpty(text))
        {
            return runs;
        }

        bool italic = false, bold = false, underline = false;
        var position = 0;
        foreach (Match m in Tags.Matches(text))
        {
            Add(text[position..m.Index]);
            position = m.Index + m.Length;

            var tag = (m.Groups[2].Success ? m.Groups[2].Value : m.Groups[3].Value).ToLowerInvariant();
            var on = m.Groups[2].Success ? m.Groups[1].Value != "/" : m.Groups[4].Value == "1";
            switch (tag)
            {
                case "i": italic = on; break;
                case "b": bold = on; break;
                case "u": underline = on; break;
            }
        }

        Add(text[position..]);
        return runs;

        void Add(string chunk)
        {
            // Other tags (<font color=…>, {\an8}, …) are dropped from the display, not shown raw.
            var clean = HtmlUtil.RemoveHtmlTags(chunk, alsoSsaTags: true);
            if (clean.Length == 0)
            {
                return;
            }

            // A line break or space between two styled pieces ("</i>\n<i>") joins the previous piece.
            if (runs.Count > 0 && runs[^1] is var last &&
                (string.IsNullOrWhiteSpace(clean) || (last.Italic == italic && last.Bold == bold && last.Underline == underline)))
            {
                runs[^1] = last with { Text = last.Text + clean };
            }
            else
            {
                runs.Add(new StyledRun(clean, italic, bold, underline));
            }
        }
    }
}
