using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Core.ContainerFormats.Matroska;
using Nikse.SubtitleEdit.Core.ContainerFormats.Mp4;

namespace Nikse.SubtitleEdit.UiLogic.SimpleSync;

public static class SubtitleSourceLoader
{
    /// <summary>Loads a subtitle from a file or an embedded track. Returns null when nothing readable is there.</summary>
    public static Subtitle? Load(SubtitleSource source)
    {
        var subtitle = source.Kind switch
        {
            SubtitleSourceKind.File or SubtitleSourceKind.Online => Subtitle.Parse(source.Path),
            SubtitleSourceKind.Matroska => LoadMatroska(source),
            SubtitleSourceKind.Mp4 => LoadMp4(source),
            _ => null,
        };

        if (subtitle == null || subtitle.Paragraphs.Count == 0)
        {
            return null;
        }

        subtitle.Paragraphs.Sort((a, b) => a.StartTime.TotalMilliseconds.CompareTo(b.StartTime.TotalMilliseconds));
        subtitle.Renumber();
        return subtitle;
    }

    private static Subtitle? LoadMatroska(SubtitleSource source)
    {
        using var matroska = new MatroskaFile(source.Path);
        if (!matroska.IsValid)
        {
            return null;
        }

        var track = matroska.GetTracks(subtitleOnly: true).FirstOrDefault(t => t.TrackNumber == source.TrackNumber);
        if (track == null)
        {
            return null;
        }

        var packets = matroska.GetSubtitle(track.TrackNumber, null);
        var subtitle = new Subtitle();
        Utilities.LoadMatroskaTextSubtitle(track, matroska, packets, subtitle);
        return subtitle;
    }

    private static Subtitle? LoadMp4(SubtitleSource source)
    {
        var mp4 = new MP4Parser(source.Path);
        var tracks = mp4.GetSubtitleTracks();
        if (source.TrackNumber < 0 || source.TrackNumber >= tracks.Count)
        {
            return null;
        }

        var paragraphs = tracks[source.TrackNumber].Mdia.Minf.Stbl.GetParagraphs();
        return new Subtitle(paragraphs.Select(p => new Paragraph(p)).ToList());
    }
}
