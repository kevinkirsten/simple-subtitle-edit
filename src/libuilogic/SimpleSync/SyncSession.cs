using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Core.SubtitleFormats;
using System.Globalization;

namespace Nikse.SubtitleEdit.UiLogic.SimpleSync;

/// <summary>
/// The state of the simple sync window without any UI: one subtitle, one offset in seconds, and
/// the questions the timeline asks ("which line is on screen now?", "where is there text?").
/// The original subtitle is never changed; the offset is applied when reading and when saving.
/// </summary>
public sealed class SyncSession
{
    public const double SmallStep = 0.1;
    public const double BigStep = 1.0;

    public SyncSession(Subtitle subtitle)
    {
        Original = subtitle;
    }

    public Subtitle Original { get; }

    /// <summary>Seconds added to every line. Positive = subtitle shows later.</summary>
    public double OffsetSeconds { get; private set; }

    public bool HasChanges => Math.Abs(OffsetSeconds) > 0.0005;

    public void SetOffset(double seconds) => OffsetSeconds = Math.Round(seconds, 3);

    public void Nudge(double seconds) => SetOffset(OffsetSeconds + seconds);

    public void ResetOffset() => OffsetSeconds = 0;

    public double StartOf(Paragraph p) => p.StartTime.TotalSeconds + OffsetSeconds;

    public double EndOf(Paragraph p) => p.EndTime.TotalSeconds + OffsetSeconds;

    /// <summary>The line on screen at <paramref name="positionSeconds"/>, or null in a gap.</summary>
    public Paragraph? ActiveAt(double positionSeconds)
    {
        var paragraphs = Original.Paragraphs;
        var target = positionSeconds - OffsetSeconds;

        // Binary search for the last line starting at or before the target.
        int lo = 0, hi = paragraphs.Count - 1, found = -1;
        while (lo <= hi)
        {
            var mid = (lo + hi) / 2;
            if (paragraphs[mid].StartTime.TotalSeconds <= target)
            {
                found = mid;
                lo = mid + 1;
            }
            else
            {
                hi = mid - 1;
            }
        }

        // Overlapping lines: walk back a little to find one that still covers the target.
        for (var i = found; i >= 0 && i > found - 5; i--)
        {
            if (paragraphs[i].EndTime.TotalSeconds > target)
            {
                return paragraphs[i];
            }
        }

        return null;
    }

    /// <summary>Lines that overlap the visible window, already shifted by the offset.</summary>
    public IEnumerable<(double Start, double End, Paragraph Paragraph)> VisibleBlocks(double fromSeconds, double toSeconds)
    {
        foreach (var p in Original.Paragraphs)
        {
            var start = StartOf(p);
            var end = EndOf(p);
            if (end < fromSeconds)
            {
                continue;
            }

            if (start > toSeconds)
            {
                yield break;
            }

            yield return (start, end, p);
        }
    }

    /// <summary>
    /// Splits <paramref name="durationSeconds"/> into <paramref name="buckets"/> slices and marks
    /// the ones that contain subtitle text. Used to paint the yellow marks on the overview bar.
    /// </summary>
    public bool[] Coverage(double durationSeconds, int buckets)
    {
        var result = new bool[Math.Max(buckets, 0)];
        if (buckets <= 0 || durationSeconds <= 0)
        {
            return result;
        }

        var secondsPerBucket = durationSeconds / buckets;
        foreach (var p in Original.Paragraphs)
        {
            var first = (int)Math.Floor(StartOf(p) / secondsPerBucket);
            var last = (int)Math.Floor(EndOf(p) / secondsPerBucket);
            for (var i = Math.Max(first, 0); i <= Math.Min(last, buckets - 1); i++)
            {
                result[i] = true;
            }
        }

        return result;
    }

    /// <summary>A copy of the subtitle with the offset applied, ready to save.</summary>
    public Subtitle BuildShifted()
    {
        var copy = new Subtitle(Original, generateNewId: false);
        if (HasChanges)
        {
            copy.AddTimeToAllParagraphs(TimeSpan.FromSeconds(OffsetSeconds));
        }

        // A line pushed before 0:00 cannot be shown; clamp it instead of writing negative times.
        foreach (var p in copy.Paragraphs)
        {
            if (p.StartTime.TotalMilliseconds < 0)
            {
                p.StartTime.TotalMilliseconds = 0;
            }

            if (p.EndTime.TotalMilliseconds < p.StartTime.TotalMilliseconds)
            {
                p.EndTime.TotalMilliseconds = p.StartTime.TotalMilliseconds;
            }
        }

        return copy;
    }

    /// <summary>Same folder and name as the video, with .srt: what Plex and players pick up.</summary>
    public static string OutputPathFor(string videoFileName) => Path.ChangeExtension(videoFileName, ".srt");

    /// <summary>
    /// Writes the shifted subtitle as SRT next to the video. An existing file with that name is
    /// first copied to "*.srt.bak", so a wrong save can always be undone.
    /// </summary>
    public SaveResult Save(string videoFileName)
    {
        var output = OutputPathFor(videoFileName);
        string? backup = null;
        if (File.Exists(output))
        {
            backup = output + ".bak";
            File.Copy(output, backup, overwrite: true);
        }

        var text = new SubRip().ToText(BuildShifted(), Path.GetFileNameWithoutExtension(videoFileName));
        File.WriteAllText(output, text, new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        return new SaveResult(output, backup);
    }

    public static string FormatOffset(double seconds)
    {
        var sign = seconds >= 0 ? "+" : "-";
        return sign + Math.Abs(seconds).ToString("0.000", CultureInfo.InvariantCulture) + "s";
    }

    public static string FormatTime(double seconds)
    {
        if (double.IsNaN(seconds) || seconds < 0)
        {
            seconds = 0;
        }

        var t = TimeSpan.FromSeconds(seconds);
        return t.TotalHours >= 1
            ? $"{(int)t.TotalHours}:{t.Minutes:00}:{t.Seconds:00}.{t.Milliseconds:000}"
            : $"{t.Minutes:00}:{t.Seconds:00}.{t.Milliseconds:000}";
    }
}

public sealed record SaveResult(string OutputFileName, string? BackupFileName);
