using CommunityToolkit.Mvvm.ComponentModel;
using Nikse.SubtitleEdit.Logic.Media;
using Nikse.SubtitleEdit.Logic.VideoPlayers;
using Nikse.SubtitleEdit.UiLogic.SimpleSync;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Nikse.SubtitleEdit.Features.Simple;

/// <summary>
/// Everything the simple window does, without Avalonia controls: open a video, pick a subtitle,
/// move it with an offset, follow the playback cursor and save. The player is an interface so
/// tests can drive it with a fake.
/// </summary>
public partial class SimpleViewModel : ObservableObject
{
    private readonly IVideoPlayer? _player;
    private CancellationTokenSource? _waveformCancel;
    private bool _loadingSource;

    // Scrubbing and seek bookkeeping (see BeginScrub/Tick).
    private bool _scrubbing;
    private bool _resumeAfterScrub;
    private double? _pendingSeek;
    private long _lastSeekIssued;
    private double _seekTarget = -1;
    private long _seekGuardUntil;

    // Smooth cursor: mpv reports a new time once per video frame (24/s on film); between two
    // reports the cursor advances by the elapsed wall time, so it moves at the screen's rate.
    private double _lastRaw = -1;
    private long _lastRawAt;
    private object? _activeParagraph;
    private long _lastTimeTextAt;
    private OpenSubtitlesClient? _onlineClient;
    private OpenSubtitlesSettings _onlineSettings = new();
    private static readonly System.Net.Http.HttpClient SharedHttp = new() { Timeout = TimeSpan.FromSeconds(30) };

    [ObservableProperty] private string _videoFileName = string.Empty;
    [ObservableProperty] private string _statusText = string.Empty;
    [ObservableProperty] private string _currentLineText = string.Empty;
    [ObservableProperty] private string _offsetText = SyncSession.FormatOffset(0);
    [ObservableProperty] private string _timeText = SyncSession.FormatTime(0);
    [ObservableProperty] private bool _hasVideo;
    [ObservableProperty] private bool _isDirty;
    [ObservableProperty] private SubtitleSource? _selectedSource;
    [ObservableProperty] private string _playlistText = string.Empty;
    [ObservableProperty] private bool _hasPrevious;
    [ObservableProperty] private bool _hasNext;

    public SimpleViewModel(IVideoPlayer? player)
    {
        _player = player;
    }

    public SimpleStrings Strings => SimpleStrings.Current;

    public ObservableCollection<SubtitleSource> Sources { get; } = [];

    public SyncSession? Session { get; private set; }

    public WavePeakData2? Peaks { get; private set; }

    public double Duration { get; private set; }

    public double Position { get; private set; }

    public double ViewStart { get; private set; }

    public double ViewSeconds { get; private set; } = 20;

    public VideoPlaylist? Playlist { get; private set; }

    /// <summary>The background waveform extraction of the open video (done when it completes).</summary>
    public Task WaveformLoading { get; private set; } = Task.CompletedTask;

    /// <summary>Builds the OpenSubtitles client; tests swap in one backed by a fake server.</summary>
    public Func<OpenSubtitlesSettings, OpenSubtitlesClient> OnlineClientFactory { get; set; } =
        settings => new OpenSubtitlesClient(SharedHttp, settings, SimpleSettingsStore.OnlineCacheFolder);

    public OpenSubtitlesSettings OnlineSettings
    {
        get => _onlineSettings;
        set
        {
            _onlineSettings = value;
            _onlineClient = null; // new key/login: new session
        }
    }

    private OpenSubtitlesClient OnlineClient => _onlineClient ??= OnlineClientFactory(_onlineSettings);

    /// <summary>
    /// Searches OpenSubtitles for the open video and adds the results to the picker as ONLINE.
    /// Nothing is downloaded until one of them is picked.
    /// </summary>
    public async Task<int> FindOnlineAsync()
    {
        if (string.IsNullOrEmpty(VideoFileName))
        {
            return 0;
        }

        var video = VideoFileName;
        StatusText = Strings.OnlineSearching;
        try
        {
            var query = await Task.Run(() => VideoQuery.FromFile(video));
            var results = await OnlineClient.SearchAsync(query, CancellationToken.None);
            if (video != VideoFileName)
            {
                return 0; // switched episode while searching
            }

            foreach (var old in Sources.Where(s => s.Kind == SubtitleSourceKind.Online).ToList())
            {
                Sources.Remove(old);
            }

            foreach (var r in results)
            {
                Sources.Add(new SubtitleSource(SubtitleSourceKind.Online, r.Describe(), OnlineClient.CachePathFor(r.FileId), r.FileId, r.Language));
            }

            StatusText = results.Count == 0 ? Strings.OnlineNone : string.Format(Strings.OnlineFound, results.Count);
            return results.Count;
        }
        catch (Exception ex)
        {
            StatusText = ex.Message;
            return 0;
        }
    }

    private async Task DownloadAndLoadAsync(SubtitleSource source)
    {
        StatusText = string.Format(Strings.OnlineDownloading, source.DisplayName);
        try
        {
            var result = await OnlineClient.DownloadAsync(source.TrackNumber, CancellationToken.None);
            if (!ReferenceEquals(SelectedSource, source))
            {
                return; // picked something else meanwhile
            }

            if (LoadSource(source) && result.RemainingDownloads != null)
            {
                StatusText = string.Format(Strings.OnlineDownloaded, result.RemainingDownloads);
            }
        }
        catch (Exception ex)
        {
            StatusText = ex.Message;
        }
    }

    /// <summary>Opens every video in a folder (and its season folders) and shows the first one.</summary>
    public async Task<bool> OpenFolderAsync(string folder)
    {
        var playlist = VideoPlaylist.FromFolder(folder);
        if (playlist.Current == null)
        {
            StatusText = Strings.NoVideosInFolder;
            return false;
        }

        Playlist = playlist;
        await LoadVideoAsync(playlist.Current);
        return true;
    }

    public async Task GoNextAsync()
    {
        if (Playlist is { HasNext: true })
        {
            await LoadVideoAsync(Playlist.MoveNext()!);
        }
    }

    public async Task GoPreviousAsync()
    {
        if (Playlist is { HasPrevious: true })
        {
            await LoadVideoAsync(Playlist.MovePrevious()!);
        }
    }

    private void UpdatePlaylistState()
    {
        PlaylistText = Playlist?.Describe() ?? string.Empty;
        HasPrevious = Playlist?.HasPrevious ?? false;
        HasNext = Playlist?.HasNext ?? false;
    }

    /// <summary>The timeline/overview content changed (view, offset, subtitle, waveform): repaint them.</summary>
    public event Action? Redraw;

    /// <summary>Only the playback cursor moved: move the cursor layers, repaint nothing else.</summary>
    public event Action? CursorMoved;

    public bool IsPlaying => _player?.IsPlaying ?? false;

    /// <summary>Opens one video; PREV/NEXT then walk the other videos in its folder.</summary>
    public async Task OpenVideoAsync(string fileName)
    {
        if (!File.Exists(fileName))
        {
            return;
        }

        Playlist = VideoPlaylist.FromVideo(fileName);
        await LoadVideoAsync(fileName);
    }

    private async Task LoadVideoAsync(string fileName)
    {
        UpdatePlaylistState();

        _waveformCancel?.Cancel();
        VideoFileName = fileName;
        HasVideo = true;
        Peaks = null;
        Position = 0;
        ViewStart = 0;
        ViewSeconds = 20;
        Duration = 0;

        if (_player != null)
        {
            await _player.LoadFile(fileName);
            Duration = _player.Duration;
        }

        Sources.Clear();
        foreach (var source in SubtitleSourceFinder.Find(fileName))
        {
            Sources.Add(source);
        }

        if (Sources.Count > 0)
        {
            SelectedSource = Sources[0];
        }
        else
        {
            Session = null;
            StatusText = Strings.NoSubtitleFound;
        }

        UpdateDurationFallback();
        WaveformLoading = LoadWaveformAsync(fileName);
        RaiseRedraw();
    }

    private async Task LoadWaveformAsync(string fileName)
    {
        var cancel = new CancellationTokenSource();
        _waveformCancel = cancel;
        var previousStatus = StatusText;
        StatusText = Strings.LoadingWaveform;
        try
        {
            var peaks = await WaveformService.LoadAsync(fileName, cancel.Token);
            if (cancel.IsCancellationRequested || fileName != VideoFileName)
            {
                return;
            }

            Peaks = peaks;
            // Only touch the status if nothing newer (e.g. "Saved") was written meanwhile.
            if (StatusText == Strings.LoadingWaveform)
            {
                StatusText = peaks == null ? Strings.NoWaveform : previousStatus;
            }
            UpdateDurationFallback();
            RaiseRedraw();
        }
        catch (OperationCanceledException)
        {
            // a newer video was opened
        }
        catch (Exception ex)
        {
            if (StatusText == Strings.LoadingWaveform)
            {
                StatusText = Strings.NoWaveform + " " + ex.Message;
            }
        }
    }

    partial void OnSelectedSourceChanged(SubtitleSource? value)
    {
        if (value == null || _loadingSource)
        {
            return;
        }

        if (value.Kind == SubtitleSourceKind.Online && !File.Exists(value.Path))
        {
            _ = DownloadAndLoadAsync(value);
            return;
        }

        LoadSource(value);
    }

    public bool LoadSource(SubtitleSource source)
    {
        var subtitle = SubtitleSourceLoader.Load(source);
        if (subtitle == null)
        {
            Session = null;
            StatusText = Strings.CouldNotLoadSubtitle;
            RaiseRedraw();
            return false;
        }

        Session = new SyncSession(subtitle);
        IsDirty = false;
        OffsetText = SyncSession.FormatOffset(0);
        StatusText = string.Format(Strings.LinesCount, source.DisplayName, subtitle.Paragraphs.Count);
        UpdateDurationFallback();
        UpdateCurrentLine();
        RaiseRedraw();
        return true;
    }

    /// <summary>Adds a subtitle file picked by hand (or dropped) to the list and selects it.</summary>
    public bool AddSubtitleFile(string fileName)
    {
        var existing = Sources.FirstOrDefault(s => s.Kind == SubtitleSourceKind.File && s.Path == fileName);
        var source = existing ?? new SubtitleSource(SubtitleSourceKind.File, Path.GetFileName(fileName), fileName);
        if (existing == null)
        {
            Sources.Insert(0, source);
        }

        _loadingSource = true;
        SelectedSource = source;
        _loadingSource = false;
        return LoadSource(source);
    }

    public void Nudge(double seconds) => SetOffset((Session?.OffsetSeconds ?? 0) + seconds);

    public void SetOffset(double seconds)
    {
        if (Session == null)
        {
            return;
        }

        Session.SetOffset(seconds);
        OffsetText = SyncSession.FormatOffset(Session.OffsetSeconds);
        IsDirty = Session.HasChanges;
        UpdateCurrentLine();
        RaiseRedraw();
    }

    public void ResetOffset() => SetOffset(0);

    public SaveResult? Save()
    {
        if (Session == null || string.IsNullOrEmpty(VideoFileName))
        {
            return null;
        }

        var result = Session.Save(VideoFileName, Duration);
        StatusText = result.BackupFileName == null
            ? string.Format(Strings.Saved, Path.GetFileName(result.OutputFileName))
            : string.Format(Strings.SavedWithBackup, Path.GetFileName(result.OutputFileName), Path.GetFileName(result.BackupFileName));

        // The saved file is the new reference: offset back to zero on top of it.
        var saved = new SubtitleSource(SubtitleSourceKind.File, Path.GetFileName(result.OutputFileName), result.OutputFileName);
        var old = Sources.FirstOrDefault(s => s.Kind == SubtitleSourceKind.File && s.Path == result.OutputFileName);
        if (old != null)
        {
            Sources.Remove(old);
        }

        Sources.Insert(0, saved);
        _loadingSource = true;
        SelectedSource = saved;
        _loadingSource = false;
        var status = StatusText;
        LoadSource(saved);
        StatusText = status;
        return result;
    }

    public void PlayOrPause() => _player?.PlayOrPause();

    public void PlayOrPauseIfPlaying()
    {
        if (_player?.IsPlaying == true)
        {
            _player.Pause();
        }
    }

    public void Seek(double seconds)
    {
        seconds = ClampToVideo(seconds);
        IssueSeek(seconds);

        Position = seconds;
        EnsureVisible(seconds);
        CursorChanged();
    }

    public void SeekRelative(double seconds) => Seek(Position + seconds);

    /// <summary>Called by the window timer: follows the player and keeps the cursor in view while playing.</summary>
    public void Tick()
    {
        if (_player == null)
        {
            return;
        }

        if (Duration <= 0 && _player.Duration > 0)
        {
            Duration = _player.Duration;
        }

        if (_scrubbing)
        {
            // The UI already shows where the mouse is; only feed mpv the newest target.
            FlushPendingSeek();
            return;
        }

        var position = _player.Position;

        // Until mpv has landed the last seek it still reports the old time; reading it then
        // made the cursor and the picture jump back and forth.
        if (_seekTarget >= 0 && Environment.TickCount64 < _seekGuardUntil && Math.Abs(position - _seekTarget) > 0.3)
        {
            return;
        }

        _seekTarget = -1;
        var now = System.Diagnostics.Stopwatch.GetTimestamp();
        if (Math.Abs(position - _lastRaw) > 0.0001)
        {
            _lastRaw = position;
            _lastRawAt = now;
        }

        if (_player.IsPlaying)
        {
            var elapsed = System.Diagnostics.Stopwatch.GetElapsedTime(_lastRawAt, now).TotalSeconds;
            var estimate = _lastRaw + Math.Min(elapsed, 0.25) * (_player.Speed > 0 ? _player.Speed : 1);

            // Never step back a hair when the next real report lands slightly behind the estimate.
            position = estimate < Position && Position - estimate < 0.1 ? Position : estimate;
        }

        if (Math.Abs(position - Position) < 0.0005)
        {
            return;
        }

        Position = position;
        var viewChanged = false;
        if (_player.IsPlaying && (position > ViewStart + ViewSeconds * 0.9 || position < ViewStart))
        {
            ViewStart = ClampViewStart(position - ViewSeconds * 0.1);
            viewChanged = true;
        }

        var active = Session?.ActiveAt(Position);
        var lineChanged = !ReferenceEquals(active, _activeParagraph);
        if (lineChanged)
        {
            _activeParagraph = active;
            CurrentLineText = active?.Text ?? string.Empty;
        }

        // The time label re-lays out text: 10 updates a second are plenty.
        if (System.Diagnostics.Stopwatch.GetElapsedTime(_lastTimeTextAt, now).TotalMilliseconds >= 100)
        {
            _lastTimeTextAt = now;
            TimeText = SyncSession.FormatTime(Position) + " / " + SyncSession.FormatTime(Duration);
        }

        if (viewChanged || lineChanged)
        {
            RaiseRedraw(); // the highlighted block changed, or the view paged forward
        }
        else
        {
            CursorMoved?.Invoke();
        }
    }

    /// <summary>Cmd/Ctrl + press on the timeline: pause, and move the cursor with the mouse.</summary>
    public void BeginScrub(double seconds)
    {
        _scrubbing = true;
        _resumeAfterScrub = _player?.IsPlaying ?? false;
        if (_resumeAfterScrub)
        {
            _player!.Pause();
        }

        ScrubTo(seconds);
    }

    public void ScrubTo(double seconds)
    {
        seconds = ClampToVideo(seconds);
        Position = seconds;
        _pendingSeek = seconds;
        FlushPendingSeek();
        CursorChanged();
    }

    public void EndScrub(double seconds)
    {
        ScrubTo(seconds);
        _scrubbing = false;
        if (_pendingSeek is { } last)
        {
            // The final position always reaches the player, even if a seek is still in flight.
            _pendingSeek = null;
            IssueSeek(last);
        }

        if (_resumeAfterScrub)
        {
            _player?.Play();
        }
    }

    /// <summary>
    /// One seek at a time: the next goes out only once mpv reports the previous one landed, and it
    /// goes to the newest mouse position. Stacked seeks made mpv fall back to keyframe seeks, and
    /// on long-GOP video (HEVC) the picture hopped between keyframes behind and ahead of the mouse.
    /// </summary>
    private void FlushPendingSeek()
    {
        if (_pendingSeek is not { } target || _player == null)
        {
            return;
        }

        var landed = _player.SupportsPlaybackRestartEvents
            ? _player.HasPlaybackRestartedSince(_lastSeekIssued)
            : System.Diagnostics.Stopwatch.GetElapsedTime(_lastSeekIssued).TotalMilliseconds > 80;
        var stuck = System.Diagnostics.Stopwatch.GetElapsedTime(_lastSeekIssued).TotalMilliseconds > 400;
        if (_lastSeekIssued == 0 || landed || stuck)
        {
            _pendingSeek = null;
            IssueSeek(target);
        }
    }

    private void IssueSeek(double seconds)
    {
        _lastSeekIssued = System.Diagnostics.Stopwatch.GetTimestamp();
        _seekTarget = seconds;
        _seekGuardUntil = Environment.TickCount64 + 1000;
        if (_player != null)
        {
            _player.Position = seconds;
        }
    }

    private double ClampToVideo(double seconds) => Math.Clamp(seconds, 0, Duration > 0 ? Duration : double.MaxValue);

    public void SetViewStart(double seconds)
    {
        ViewStart = ClampViewStart(seconds);
        RaiseRedraw();
    }

    public void CenterOn(double seconds) => SetViewStart(seconds - ViewSeconds / 2);

    public void Zoom(double factor, double? anchorSeconds = null)
    {
        var anchor = anchorSeconds ?? Position;
        var ratio = ViewSeconds > 0 ? (anchor - ViewStart) / ViewSeconds : 0.5;
        var max = Duration > 0 ? Duration : 3600;
        ViewSeconds = Math.Clamp(ViewSeconds * factor, TimelineControl.MinViewSeconds, Math.Max(max, TimelineControl.MinViewSeconds));
        ViewStart = ClampViewStart(anchor - ratio * ViewSeconds);
        RaiseRedraw();
    }

    public void ZoomIn() => Zoom(0.5);

    public void ZoomOut() => Zoom(2);

    private void EnsureVisible(double seconds)
    {
        if (seconds < ViewStart || seconds > ViewStart + ViewSeconds)
        {
            CenterOn(seconds);
        }
    }

    private double ClampViewStart(double seconds)
    {
        var max = Math.Max(0, Duration - ViewSeconds);
        return Duration > 0 ? Math.Clamp(seconds, 0, max) : Math.Max(0, seconds);
    }

    private void UpdateDurationFallback()
    {
        if (Duration > 0)
        {
            return;
        }

        var fromPeaks = Peaks?.LengthInSeconds ?? 0;
        var fromSubtitle = Session?.Original.Paragraphs.LastOrDefault()?.EndTime.TotalSeconds ?? 0;
        Duration = Math.Max(fromPeaks, fromSubtitle);
    }

    /// <returns>True when the line on screen changed (its block is highlighted, so repaint).</returns>
    private bool UpdateCurrentLine()
    {
        TimeText = SyncSession.FormatTime(Position) + " / " + SyncSession.FormatTime(Duration);
        var active = Session?.ActiveAt(Position);
        CurrentLineText = active?.Text ?? string.Empty;
        var changed = !ReferenceEquals(active, _activeParagraph);
        _activeParagraph = active;
        return changed;
    }

    /// <summary>After the cursor moved: full repaint only if the highlighted line changed.</summary>
    private void CursorChanged()
    {
        if (UpdateCurrentLine())
        {
            RaiseRedraw();
        }
        else
        {
            CursorMoved?.Invoke();
        }
    }

    private void RaiseRedraw()
    {
        Redraw?.Invoke();
        CursorMoved?.Invoke();
    }
}
