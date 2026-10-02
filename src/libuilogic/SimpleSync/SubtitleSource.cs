namespace Nikse.SubtitleEdit.UiLogic.SimpleSync;

public enum SubtitleSourceKind
{
    File,
    Matroska,
    Mp4,
    Online,
}

/// <summary>
/// One subtitle the simple sync window can offer for a video: a file next to it, or a text
/// track embedded in the video container.
/// </summary>
public sealed record SubtitleSource(
    SubtitleSourceKind Kind,
    string DisplayName,
    string Path,
    int TrackNumber = -1,
    string Language = "")
{
    public override string ToString() => DisplayName;
}
