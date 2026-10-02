using Nikse.SubtitleEdit.Logic.VideoPlayers;
using Nikse.SubtitleEdit.Logic.VideoPlayers.LibMpvDynamic;
using System.Threading.Tasks;

namespace UITests.Features.Simple;

/// <summary>A player with no video output: remembers what the window asked it to do.</summary>
public sealed class FakeVideoPlayer : IVideoPlayer
{
    public FakeVideoPlayer(double duration = 90)
    {
        Duration = duration;
    }

    public string Name => "fake";
    public string FileName { get; private set; } = string.Empty;
    public bool CanLoad() => true;

    public Task LoadFile(string fileName, double startPositionSeconds = 0)
    {
        FileName = fileName;
        Position = startPositionSeconds;
        return Task.CompletedTask;
    }

    public void CloseFile() => FileName = string.Empty;
    public void Play() => IsPlaying = true;
    public void PlayOrPause() => IsPlaying = !IsPlaying;
    public void Pause() => IsPlaying = false;
    public void Stop() => IsPlaying = false;
    public AudioTrackInfo? ToggleAudioTrack() => null;
    public bool IsPlaying { get; private set; }
    public bool IsPaused => !IsPlaying;
    public double Position { get; set; }
    public double Duration { get; }
    public int VolumeMaximum => 100;
    public double Volume { get; set; } = 100;
    public double Speed { get; set; } = 1;
}
