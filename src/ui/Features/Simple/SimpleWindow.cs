using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using Nikse.SubtitleEdit.Controls.VideoPlayer;
using Nikse.SubtitleEdit.Features.Main;
using Nikse.SubtitleEdit.Features.Main.Layout;
using Nikse.SubtitleEdit.Features.Shared;
using Nikse.SubtitleEdit.Logic;
using Nikse.SubtitleEdit.Logic.Config;
using Nikse.SubtitleEdit.Logic.VideoPlayers;
using Nikse.SubtitleEdit.Logic.VideoPlayers.LibMpvDynamic;
using Nikse.SubtitleEdit.UiLogic.SimpleSync;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Nikse.SubtitleEdit.Features.Simple;

/// <summary>
/// The default window of Simple Subtitle Edit: video, whole-video overview, zoomed audio +
/// subtitle lanes, offset buttons, subtitle picker and save. The full editor stays one click
/// away (ADVANCED MODE).
/// </summary>
public class SimpleWindow : Window
{
    private readonly SimpleViewModel _vm;
    private readonly VideoPlayerControl? _videoPlayer;
    private readonly TimelineControl _timeline = new();
    private readonly MinimapControl _minimap = new();
    private readonly Border _dropHint;
    private readonly DispatcherTimer _timer;
    private readonly Button _playButton;
    private readonly Grid _videoArea;

    public SimpleWindow() : this(createPlayer: true)
    {
    }

    /// <param name="createPlayer">False in headless tests, where no native player can load.</param>
    public SimpleWindow(bool createPlayer, IVideoPlayer? player = null)
    {
        var strings = SimpleStrings.Current;
        Title = strings.AppTitle;
        Width = 1100;
        Height = 820;
        MinWidth = 720;
        MinHeight = 560;
        Background = BrutalTheme.PaperDim;
        FontFamily = BrutalTheme.Mono;
        RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Light;
        Styles.Add(BrutalTheme.CreateStyles());

        if (createPlayer)
        {
            LibMpvDynamicPlayer.MpvPath = Se.DataFolder;
            _videoPlayer = InitVideoPlayer.MakeVideoPlayer();
            _videoPlayer.HideVideoControls();
            player = _videoPlayer.VideoPlayer;
        }

        _vm = new SimpleViewModel(player);
        DataContext = _vm;
        AskLeave = () => UnsavedDialog.AskAsync(this);

        // --- top bar -------------------------------------------------------------------
        var openFolder = BrutalTheme.Button(strings.OpenFolder, "OpenFolder");
        openFolder.Click += async (_, _) => await PickFolderAsync();
        var openVideo = BrutalTheme.Button(strings.OpenVideo, "OpenVideo");
        openVideo.Click += async (_, _) => await PickVideoAsync();
        var previous = BrutalTheme.Button(strings.Previous, "PreviousVideo");
        previous.Click += async (_, _) => await GoAsync(next: false);
        previous.Bind(IsEnabledProperty, new Binding(nameof(SimpleViewModel.HasPrevious)));
        var next = BrutalTheme.Button(strings.Next, "NextVideo");
        next.Click += async (_, _) => await GoAsync(next: true);
        next.Bind(IsEnabledProperty, new Binding(nameof(SimpleViewModel.HasNext)));
        var fileLabel = BrutalTheme.Label(string.Empty);
        fileLabel.Bind(TextBlock.TextProperty, new Binding(nameof(SimpleViewModel.PlaylistText)) { TargetNullValue = strings.AppTitle });
        fileLabel.TextTrimming = TextTrimming.CharacterEllipsis;
        fileLabel.TextAlignment = TextAlignment.Center;
        fileLabel.Margin = new Thickness(8, 0);
        Avalonia.Automation.AutomationProperties.SetAutomationId(fileLabel, "PlaylistText");
        var advanced = BrutalTheme.Button(strings.AdvancedMode, "AdvancedMode");
        advanced.Click += (_, _) => OpenAdvancedMode();
        var topBar = Row(new Control[] { openFolder, openVideo, previous, fileLabel, next, advanced }, stretchIndex: 3);

        // --- video ---------------------------------------------------------------------
        var caption = new TextBlock
        {
            FontFamily = BrutalTheme.Mono,
            FontSize = 18,
            FontWeight = FontWeight.Bold,
            Foreground = BrutalTheme.Ink,
            TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
            MinHeight = 48,
            Margin = new Thickness(8, 6),
        };
        caption.Bind(TextBlock.TextProperty, new Binding(nameof(SimpleViewModel.CurrentLineText)));
        Avalonia.Automation.AutomationProperties.SetAutomationId(caption, "Caption");

        _dropHint = new Border
        {
            Background = BrutalTheme.Paper,
            Child = new TextBlock
            {
                Text = strings.DropHint,
                FontFamily = BrutalTheme.Mono,
                FontSize = 22,
                FontWeight = FontWeight.Bold,
                Foreground = BrutalTheme.Ink,
                TextAlignment = TextAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            },
        };
        _dropHint.Cursor = BrutalTheme.Hand;
        _dropHint.PointerPressed += async (_, _) => await PickVideoAsync();

        var videoArea = _videoArea = new Grid { Background = Brushes.Black };
        if (_videoPlayer != null)
        {
            videoArea.Children.Add(_videoPlayer);
        }

        videoArea.Children.Add(_dropHint);
        var videoWithCaption = new Grid { RowDefinitions = new RowDefinitions("*,Auto") };
        videoWithCaption.Children.Add(videoArea);
        var captionBox = new Border { Background = BrutalTheme.Paper, BorderBrush = BrutalTheme.Ink, BorderThickness = new Thickness(0, 2, 0, 0), Child = caption };
        Grid.SetRow(captionBox, 1);
        videoWithCaption.Children.Add(captionBox);

        // --- transport + offset --------------------------------------------------------
        _playButton = BrutalTheme.Button("▶", "PlayPause");
        _playButton.Click += (_, _) => _vm.PlayOrPause();
        var timeLabel = BrutalTheme.Label(string.Empty);
        timeLabel.Bind(TextBlock.TextProperty, new Binding(nameof(SimpleViewModel.TimeText)));
        timeLabel.Margin = new Thickness(10, 0);
        Avalonia.Automation.AutomationProperties.SetAutomationId(timeLabel, "Time");

        var offsetLabel = BrutalTheme.Label(strings.Offset);
        offsetLabel.Margin = new Thickness(0, 0, 8, 0);
        var offsetValue = BrutalTheme.Label(string.Empty, 16);
        offsetValue.MinWidth = 96;
        offsetValue.TextAlignment = TextAlignment.Center;
        offsetValue.Bind(TextBlock.TextProperty, new Binding(nameof(SimpleViewModel.OffsetText)));
        Avalonia.Automation.AutomationProperties.SetAutomationId(offsetValue, "OffsetValue");
        var minusBig = NudgeButton("−1s", -SyncSession.BigStep, "OffsetMinusBig");
        var minusSmall = NudgeButton("−0.1", -SyncSession.SmallStep, "OffsetMinusSmall");
        var plusSmall = NudgeButton("+0.1", SyncSession.SmallStep, "OffsetPlusSmall");
        var plusBig = NudgeButton("+1s", SyncSession.BigStep, "OffsetPlusBig");
        var reset = BrutalTheme.Button(strings.Reset, "OffsetReset");
        reset.Click += (_, _) => _vm.ResetOffset();
        var dirty = BrutalTheme.Label(strings.UnsavedChanges, 11);
        dirty.Foreground = BrutalTheme.Cursor;
        dirty.Margin = new Thickness(8, 0, 0, 0);
        dirty.Bind(IsVisibleProperty, new Binding(nameof(SimpleViewModel.IsDirty)));
        var transport = Row(new Control[] { _playButton, timeLabel, new Control(), offsetLabel, minusBig, minusSmall, offsetValue, plusSmall, plusBig, reset, dirty }, stretchIndex: 2);

        // --- overview + timeline -------------------------------------------------------
        _minimap.Height = 46;
        _minimap.JumpRequested += s => _vm.CenterOn(s);
        _timeline.SeekRequested += s => _vm.Seek(s);
        _timeline.ViewStartChanged += s => _vm.SetViewStart(s);
        _timeline.OffsetDragged += s => _vm.SetOffset(s);
        _timeline.ZoomRequested += (f, a) => _vm.Zoom(f, a);

        var zoomIn = BrutalTheme.Button("+", "ZoomIn");
        zoomIn.Click += (_, _) => _vm.ZoomIn();
        var zoomOut = BrutalTheme.Button("−", "ZoomOut");
        zoomOut.Click += (_, _) => _vm.ZoomOut();
        var zoomColumn = new StackPanel { Spacing = 6, Margin = new Thickness(6, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center, Children = { zoomOut, zoomIn } };

        var laneLabels = new Grid { RowDefinitions = new RowDefinitions("18,55*,45*"), Width = 64 };
        var audioLabel = BrutalTheme.Label(strings.Audio, 11);
        Grid.SetRow(audioLabel, 1);
        var textLabel = BrutalTheme.Label(strings.Text, 11);
        Grid.SetRow(textLabel, 2);
        laneLabels.Children.Add(audioLabel);
        laneLabels.Children.Add(textLabel);

        var timelineRow = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), Height = 190 };
        timelineRow.Children.Add(laneLabels);
        var timelineBox = BrutalTheme.Box(_timeline);
        Grid.SetColumn(timelineBox, 1);
        timelineRow.Children.Add(timelineBox);
        Grid.SetColumn(zoomColumn, 2);
        timelineRow.Children.Add(zoomColumn);

        var overviewLabel = BrutalTheme.Label(strings.Overview, 11);
        overviewLabel.Width = 64;
        overviewLabel.TextWrapping = TextWrapping.Wrap;
        var overviewRow = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto") };
        overviewRow.Children.Add(overviewLabel);
        var minimapBox = BrutalTheme.Box(_minimap);
        Grid.SetColumn(minimapBox, 1);
        overviewRow.Children.Add(minimapBox);
        var spacer = new Control { Width = 50 };
        Grid.SetColumn(spacer, 2);
        overviewRow.Children.Add(spacer);

        // --- subtitle picker + save ----------------------------------------------------
        var subtitleLabel = BrutalTheme.Label(strings.Subtitle + ":");
        subtitleLabel.Margin = new Thickness(0, 0, 10, 0);
        var combo = new ComboBox
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            FontFamily = BrutalTheme.Mono,
            BorderBrush = BrutalTheme.Ink,
            BorderThickness = BrutalTheme.Line,
            CornerRadius = new CornerRadius(0),
            Background = BrutalTheme.Paper,
            MinHeight = 38,
            Cursor = BrutalTheme.Hand,
        };
        combo.Classes.Add(BrutalTheme.ButtonClass);
        combo.ItemTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<SubtitleSource>((source, _) => SourceItem(source));
        combo.Bind(ItemsControl.ItemsSourceProperty, new Binding(nameof(SimpleViewModel.Sources)));
        combo.Bind(SelectingItemsControl.SelectedItemProperty, new Binding(nameof(SimpleViewModel.SelectedSource)) { Mode = BindingMode.TwoWay });
        Avalonia.Automation.AutomationProperties.SetAutomationId(combo, "SubtitlePicker");
        var otherFile = BrutalTheme.Button(strings.OpenSubtitle, "OpenSubtitle");
        otherFile.Click += async (_, _) => await PickSubtitleAsync();
        var save = BrutalTheme.Button(strings.Save, "Save");
        save.Background = BrutalTheme.Marker;
        save.Classes.Add(BrutalTheme.PrimaryClass);
        save.Click += (_, _) => SaveNow();
        var subtitleRow = Row(new Control[] { subtitleLabel, combo, otherFile, save }, stretchIndex: 1);

        // --- status --------------------------------------------------------------------
        var status = BrutalTheme.Label(string.Empty, 12);
        status.Bind(TextBlock.TextProperty, new Binding(nameof(SimpleViewModel.StatusText)));
        status.TextTrimming = TextTrimming.CharacterEllipsis;
        Avalonia.Automation.AutomationProperties.SetAutomationId(status, "Status");
        var help = BrutalTheme.Label(strings.Help, 10);
        help.Opacity = 0.7;
        help.TextWrapping = TextWrapping.Wrap;

        var root = new Grid
        {
            Margin = new Thickness(12),
            RowDefinitions = new RowDefinitions("Auto,*,Auto,Auto,Auto,Auto,Auto,Auto"),
            RowSpacing = 10,
        };
        AddRow(root, topBar, 0);
        AddRow(root, BrutalTheme.Box(videoWithCaption), 1);
        AddRow(root, transport, 2);
        AddRow(root, overviewRow, 3);
        AddRow(root, timelineRow, 4);
        AddRow(root, subtitleRow, 5);
        AddRow(root, status, 6);
        AddRow(root, help, 7);
        Content = root;

        _vm.Redraw += Redraw;
        _vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(SimpleViewModel.HasVideo))
            {
                _dropHint.IsVisible = !_vm.HasVideo;
            }
        };

        if (createPlayer && _videoPlayer?.VideoPlayer is EmptyVideoPlayer)
        {
            _vm.StatusText = strings.NoVideoPlayer;
            if (OperatingSystem.IsWindows())
            {
                Opened += async (_, _) => await OfferLibMpvDownloadAsync();
            }
        }

        DragDrop.SetAllowDrop(this, true);
        AddHandler(DragDrop.DragOverEvent, OnDragOver, RoutingStrategies.Bubble);
        AddHandler(DragDrop.DropEvent, OnDrop, RoutingStrategies.Bubble);
        AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(40) };
        _timer.Tick += (_, _) =>
        {
            _vm.Tick();
            _playButton.Content = _vm.IsPlaying ? "❚❚" : "▶";
        };
        _timer.Start();
        Closed += (_, _) =>
        {
            _timer.Stop();
            _videoPlayer?.CloseAndDisposePlayer();
        };
    }

    public SimpleViewModel ViewModel => _vm;

    public TimelineControl Timeline => _timeline;

    public MinimapControl Minimap => _minimap;

    public Task OpenVideoAsync(string fileName) => _vm.OpenVideoAsync(fileName);

    /// <summary>
    /// Shows a still image where the video goes. Only for runs without a native player
    /// (headless tests and the README screenshots); a real player draws its own frames.
    /// </summary>
    public void ShowStillFrame(IImage image)
    {
        _videoArea.Children.Insert(0, new Image { Source = image, Stretch = Stretch.Uniform });
    }

    /// <summary>Asks before leaving a subtitle with an unsaved offset. Replaceable in tests.</summary>
    public System.Func<Task<LeaveChoice>> AskLeave { get; set; }

    private async Task<bool> ConfirmLeaveAsync()
    {
        if (!_vm.IsDirty)
        {
            return true;
        }

        switch (await AskLeave())
        {
            case LeaveChoice.Save:
                SaveNow();
                return true;
            case LeaveChoice.Discard:
                return true;
            default:
                return false;
        }
    }

    private async Task GoAsync(bool next)
    {
        if (!await ConfirmLeaveAsync())
        {
            return;
        }

        if (next)
        {
            await _vm.GoNextAsync();
        }
        else
        {
            await _vm.GoPreviousAsync();
        }
    }

    private static Control SourceItem(SubtitleSource? source)
    {
        if (source == null)
        {
            return new TextBlock();
        }

        var strings = SimpleStrings.Current;
        var tag = source.Kind switch
        {
            SubtitleSourceKind.File => strings.TagLocal,
            SubtitleSourceKind.Online => strings.TagOnline,
            _ => strings.TagEmbedded,
        };

        var badge = new Border
        {
            Background = source.Kind == SubtitleSourceKind.File ? BrutalTheme.Marker : BrutalTheme.PaperDim,
            BorderBrush = BrutalTheme.Ink,
            BorderThickness = new Thickness(1),
            Padding = new Thickness(5, 1),
            Margin = new Thickness(0, 0, 8, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Child = BrutalTheme.Label(tag, 10),
        };
        return new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Children = { badge, new TextBlock { Text = source.DisplayName, FontFamily = BrutalTheme.Mono, VerticalAlignment = VerticalAlignment.Center } },
        };
    }

    private async Task PickFolderAsync()
    {
        if (!await ConfirmLeaveAsync())
        {
            return;
        }

        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { AllowMultiple = false });
        var path = folders.FirstOrDefault()?.TryGetLocalPath();
        if (path != null)
        {
            await _vm.OpenFolderAsync(path);
        }
    }

    private Button NudgeButton(string text, double seconds, string id)
    {
        var button = BrutalTheme.Button(text, id);
        button.Click += (_, _) => _vm.Nudge(seconds);
        return button;
    }

    private static Grid Row(Control[] children, int stretchIndex)
    {
        var grid = new Grid { ColumnSpacing = 6 };
        for (var i = 0; i < children.Length; i++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition(i == stretchIndex ? GridLength.Star : GridLength.Auto));
            Grid.SetColumn(children[i], i);
            grid.Children.Add(children[i]);
        }

        return grid;
    }

    private static void AddRow(Grid grid, Control control, int row)
    {
        Grid.SetRow(control, row);
        grid.Children.Add(control);
    }

    private void Redraw()
    {
        _timeline.Peaks = _minimap.Peaks = _vm.Peaks;
        _timeline.Session = _minimap.Session = _vm.Session;
        _timeline.Duration = _minimap.Duration = _vm.Duration;
        _timeline.ViewStart = _minimap.ViewStart = _vm.ViewStart;
        _timeline.ViewSeconds = _minimap.ViewSeconds = _vm.ViewSeconds;
        _timeline.Position = _minimap.Position = _vm.Position;

        _timeline.InvalidateVisual();
        _minimap.InvalidateVisual();
    }

    private async Task PickVideoAsync()
    {
        if (!await ConfirmLeaveAsync())
        {
            return;
        }

        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Video") { Patterns = Nikse.SubtitleEdit.Core.Common.Utilities.VideoFileExtensions.Select(e => "*" + e).ToList() },
                FilePickerFileTypes.All,
            ],
        });
        var path = files.FirstOrDefault()?.TryGetLocalPath();
        if (path != null)
        {
            await _vm.OpenVideoAsync(path);
        }
    }

    private async Task PickSubtitleAsync()
    {
        var start = string.IsNullOrEmpty(_vm.VideoFileName) ? null : await StorageProvider.TryGetFolderFromPathAsync(Path.GetDirectoryName(_vm.VideoFileName)!);
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            AllowMultiple = false,
            SuggestedStartLocation = start,
            FileTypeFilter =
            [
                new FilePickerFileType("Subtitle") { Patterns = SubtitleSourceFinder.SubtitleFileExtensions.Select(e => "*" + e).ToList() },
                FilePickerFileTypes.All,
            ],
        });
        var path = files.FirstOrDefault()?.TryGetLocalPath();
        if (path != null)
        {
            _vm.AddSubtitleFile(path);
        }
    }

    private void SaveNow()
    {
        try
        {
            _vm.Save();
        }
        catch (Exception ex)
        {
            _vm.StatusText = ex.Message;
        }
    }

    private void OpenAdvancedMode()
    {
        if (!string.IsNullOrEmpty(_vm.VideoFileName))
        {
            Program.PendingVideoToOpen = _vm.VideoFileName;
            if (_vm.SelectedSource is { Kind: SubtitleSourceKind.File } source)
            {
                Program.PendingFileToOpen = source.Path;
            }
        }

        MainWindowFactory.OpenNewWindow();
    }

    private async Task OfferLibMpvDownloadAsync()
    {
        var vm = Locator.Services.GetRequiredService<DownloadLibMpvViewModel>();
        await new DownloadLibMpvWindow(vm).ShowDialog(this);
        if (!string.IsNullOrEmpty(vm.LibMpvFileName))
        {
            _vm.StatusText = SimpleStrings.Current.AppTitle + ": restart the app to use the new player.";
        }
    }

    private static void OnDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = e.DataTransfer.Contains(DataFormat.File) ? DragDropEffects.Copy : DragDropEffects.None;
    }

    private async void OnDrop(object? sender, DragEventArgs e)
    {
        try
        {
            var paths = e.DataTransfer.TryGetFiles()?.Select(f => f.TryGetLocalPath()?.TrimEnd('/', '\\')).OfType<string>().ToList() ?? [];
            await HandleDroppedFilesAsync(paths);
        }
        catch (Exception ex)
        {
            _vm.StatusText = ex.Message;
        }
    }

    /// <summary>A video opens; a subtitle file is added to the picker. Both can be dropped together.</summary>
    public async Task HandleDroppedFilesAsync(System.Collections.Generic.IReadOnlyList<string> paths)
    {
        var folder = paths.FirstOrDefault(Directory.Exists);
        if (folder != null)
        {
            if (await ConfirmLeaveAsync())
            {
                await _vm.OpenFolderAsync(folder);
            }

            return;
        }

        var video = paths.FirstOrDefault(p => !IsSubtitleFile(p));
        // macOS delivers a command-line file both as an argument and as an "open file" event.
        if (video != null && !string.Equals(video, _vm.VideoFileName, StringComparison.Ordinal))
        {
            await _vm.OpenVideoAsync(video);
        }

        foreach (var subtitle in paths.Where(IsSubtitleFile))
        {
            _vm.AddSubtitleFile(subtitle);
        }
    }

    private static bool IsSubtitleFile(string path) =>
        SubtitleSourceFinder.SubtitleFileExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Source is TextBox or ComboBox)
        {
            return;
        }

        var command = e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Meta);
        switch (e.Key)
        {
            case Key.PageDown:
                _ = GoAsync(next: true);
                break;
            case Key.PageUp:
                _ = GoAsync(next: false);
                break;
            case Key.Space:
                _vm.PlayOrPause();
                break;
            case Key.Left:
                _vm.SeekRelative(-1);
                break;
            case Key.Right:
                _vm.SeekRelative(1);
                break;
            case Key.OemComma when e.KeyModifiers.HasFlag(KeyModifiers.Shift):
                _vm.Nudge(-SyncSession.BigStep);
                break;
            case Key.OemPeriod when e.KeyModifiers.HasFlag(KeyModifiers.Shift):
                _vm.Nudge(SyncSession.BigStep);
                break;
            case Key.OemComma:
                _vm.Nudge(-SyncSession.SmallStep);
                break;
            case Key.OemPeriod:
                _vm.Nudge(SyncSession.SmallStep);
                break;
            case Key.OemPlus or Key.Add:
                _vm.ZoomIn();
                break;
            case Key.OemMinus or Key.Subtract:
                _vm.ZoomOut();
                break;
            case Key.S when command:
                SaveNow();
                break;
            default:
                return;
        }

        e.Handled = true;
    }

    private sealed class FileNameOnly : Avalonia.Data.Converters.IValueConverter
    {
        public static readonly FileNameOnly Instance = new();

        public object? Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture) =>
            value is string s && s.Length > 0 ? Path.GetFileName(s) : SimpleStrings.Current.AppTitle;

        public object? ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture) =>
            throw new NotSupportedException();
    }
}
