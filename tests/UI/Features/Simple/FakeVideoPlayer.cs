using Nikse.SubtitleEdit.Logic.VideoPlayers;
using Nikse.SubtitleEdit.Logic.VideoPlayers.LibMpvDynamic;
using System.Collections.Generic;
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
        _position = startPositionSeconds; // opening is not a seek
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
    private double _position;

    /// <summary>Every seek the window sent, in order.</summary>
    public List<double> Seeks { get; } = [];

    /// <summary>Like mpv: the window may wait for "seek landed" before sending the next one.</summary>
    public bool SupportsPlaybackRestartEvents { get; set; }

    /// <summary>With restart events on: whether the last seek has landed.</summary>
    public bool SeekLanded { get; set; } = true;

    /// <summary>What a still-seeking mpv reports: the old time, until the seek lands.</summary>
    public double ReportedPositionWhileSeeking { get; set; } = -1;

    public bool HasPlaybackRestartedSince(long stopwatchTimestamp) => SeekLanded;

    public double Position
    {
        get => !SeekLanded && ReportedPositionWhileSeeking >= 0 ? ReportedPositionWhileSeeking : _position;
        set
        {
            Seeks.Add(value);
            _position = value;
            if (SupportsPlaybackRestartEvents)
            {
                SeekLanded = false;
            }
        }
    }
    public double Duration { get; }
    public int VolumeMaximum => 100;
    public double Volume { get; set; } = 100;
    public double Speed { get; set; } = 1;
}
