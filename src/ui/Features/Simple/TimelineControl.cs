using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Nikse.SubtitleEdit.Logic.Media;
using Nikse.SubtitleEdit.UiLogic.SimpleSync;
using System;
using System.Globalization;

namespace Nikse.SubtitleEdit.Features.Simple;

/// <summary>
/// Zoomed view of a few seconds: audio waveform on top, subtitle lines as yellow blocks below.
/// Click = seek. Drag the audio lane = scroll. Drag the text lane = move the whole subtitle (offset).
/// Wheel = scroll, Ctrl/Cmd + wheel = zoom. Ctrl/Cmd + click or drag = move the playback cursor.
/// </summary>
public class TimelineControl : Control
{
    public const double MinViewSeconds = 2;
    private const double DragThreshold = 4;
    private const double TimeAxisHeight = 18;

    private enum DragMode { None, Pan, Offset, Scrub }

    private DragMode _dragMode;
    private bool _dragStarted;
    private Point _pressPoint;
    private double _pressViewStart;
    private double _pressOffset;

    public WavePeakData2? Peaks { get; set; }
    public SyncSession? Session { get; set; }
    public double Duration { get; set; }
    public double ViewStart { get; set; }
    public double ViewSeconds { get; set; } = 20;
    public double Position { get; set; }

    public event Action<double>? SeekRequested;
    public event Action<double>? ScrubStarted;
    public event Action<double>? ScrubMoved;
    public event Action<double>? ScrubEnded;
    public event Action<double>? ViewStartChanged;
    public event Action<double>? OffsetDragged;
    public event Action? OffsetDragFinished;
    public event Action<double, double>? ZoomRequested; // factor, anchor seconds

    public TimelineControl()
    {
        ClipToBounds = true;
        Focusable = true;
        Cursor = BrutalTheme.Hand;
        Avalonia.Automation.AutomationProperties.SetAutomationId(this, "Timeline");
    }

    public double WaveLaneBottom => TimeAxisHeight + (Bounds.Height - TimeAxisHeight) * 0.55;

    public double SecondsPerPixel => Bounds.Width > 0 ? ViewSeconds / Bounds.Width : 0;

    public double XToSeconds(double x) => ViewStart + x * SecondsPerPixel;

    public double SecondsToX(double seconds) => SecondsPerPixel > 0 ? (seconds - ViewStart) / SecondsPerPixel : 0;

    public override void Render(DrawingContext context)
    {
        var width = Bounds.Width;
        var height = Bounds.Height;
        if (width <= 0 || height <= 0)
        {
            return;
        }

        context.FillRectangle(BrutalTheme.Paper, new Rect(0, 0, width, height));

        var waveTop = TimeAxisHeight;
        var waveBottom = WaveLaneBottom;
        var textTop = waveBottom;

        DrawTimeAxis(context, width);
        DrawWaveform(context, width, waveTop, waveBottom);

        context.FillRectangle(BrutalTheme.PaperDim, new Rect(0, textTop, width, height - textTop));
        DrawSubtitleBlocks(context, textTop + 6, height - 6);

        context.DrawLine(BrutalTheme.InkPen, new Point(0, waveTop), new Point(width, waveTop));
        context.DrawLine(BrutalTheme.InkPen, new Point(0, textTop), new Point(width, textTop));

        var cursorX = SecondsToX(Position);
        if (cursorX >= 0 && cursorX <= width)
        {
            context.DrawLine(BrutalTheme.CursorPen, new Point(cursorX, 0), new Point(cursorX, height));
        }
    }

    private void DrawTimeAxis(DrawingContext context, double width)
    {
        var step = PickTickStep(ViewSeconds);
        var first = Math.Floor(ViewStart / step) * step;
        var typeface = new Typeface(BrutalTheme.Mono);
        for (var t = first; t <= ViewStart + ViewSeconds; t += step)
        {
            var x = SecondsToX(t);
            if (x < 0)
            {
                continue;
            }

            context.DrawLine(BrutalTheme.ThinInkPen, new Point(x, TimeAxisHeight - 5), new Point(x, TimeAxisHeight));
            var text = new FormattedText(SyncSession.FormatTime(t)[..^4], CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, 10, BrutalTheme.Ink);
            context.DrawText(text, new Point(x + 3, 2));
        }
    }

    public static double PickTickStep(double viewSeconds)
    {
        foreach (var step in new[] { 0.5, 1, 2, 5, 10, 15, 30, 60, 120, 300, 600 })
        {
            if (viewSeconds / step <= 12)
            {
                return step;
            }
        }

        return 1200;
    }

    private void DrawWaveform(DrawingContext context, double width, double top, double bottom)
    {
        var peaks = Peaks;
        if (peaks == null || peaks.Peaks.Count == 0 || peaks.HighestPeak <= 0)
        {
            return;
        }

        var mid = (top + bottom) / 2;
        var half = (bottom - top) / 2 - 2;
        var pen = new Pen(BrutalTheme.Wave, 1);
        var span = peaks.AsSpan();
        var peaksPerSecond = peaks.SampleRate;
        for (var x = 0; x < (int)width; x++)
        {
            var from = (int)(XToSeconds(x) * peaksPerSecond);
            var to = Math.Max(from + 1, (int)(XToSeconds(x + 1) * peaksPerSecond));
            if (from < 0 || from >= span.Length)
            {
                continue;
            }

            to = Math.Min(to, span.Length);
            int max = 0, min = 0;
            for (var i = from; i < to; i++)
            {
                max = Math.Max(max, span[i].Max);
                min = Math.Min(min, span[i].Min);
            }

            var yMax = mid - max * half / peaks.HighestPeak;
            var yMin = mid - min * half / peaks.HighestPeak;
            context.DrawLine(pen, new Point(x + 0.5, yMax), new Point(x + 0.5, Math.Max(yMin, yMax + 1)));
        }
    }

    private void DrawSubtitleBlocks(DrawingContext context, double top, double bottom)
    {
        var session = Session;
        if (session == null)
        {
            return;
        }

        var active = session.ActiveAt(Position);
        var typeface = new Typeface(BrutalTheme.Mono);
        foreach (var (start, end, paragraph) in session.VisibleBlocks(ViewStart, ViewStart + ViewSeconds))
        {
            var x1 = SecondsToX(start);
            var x2 = Math.Max(SecondsToX(end), x1 + 3);
            var rect = new Rect(x1, top, x2 - x1, bottom - top);
            context.FillRectangle(ReferenceEquals(paragraph, active) ? BrutalTheme.MarkerActive : BrutalTheme.Marker, rect);
            context.DrawRectangle(BrutalTheme.InkPen, rect);

            if (rect.Width > 30)
            {
                var text = new FormattedText(paragraph.Text.Replace(Environment.NewLine, " ").Replace("\n", " "), CultureInfo.CurrentCulture, FlowDirection.LeftToRight, typeface, 11, BrutalTheme.Ink)
                {
                    MaxTextWidth = rect.Width - 8,
                    MaxTextHeight = rect.Height - 6,
                    Trimming = TextTrimming.CharacterEllipsis,
                };
                using (context.PushClip(rect))
                {
                    context.DrawText(text, new Point(rect.X + 4, rect.Y + 3));
                }
            }
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        var point = e.GetPosition(this);
        _pressPoint = point;
        _pressViewStart = ViewStart;
        _pressOffset = Session?.OffsetSeconds ?? 0;
        _dragStarted = false;
        if (IsScrubModifier(e.KeyModifiers))
        {
            // Cmd (macOS) / Ctrl (Windows, Linux): the red cursor jumps here and follows the mouse.
            _dragMode = DragMode.Scrub;
            ScrubStarted?.Invoke(Math.Max(0, XToSeconds(point.X)));
        }
        else
        {
            _dragMode = point.Y >= WaveLaneBottom && Session != null ? DragMode.Offset : DragMode.Pan;
        }

        e.Pointer.Capture(this);
        Focus();
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_dragMode == DragMode.None)
        {
            return;
        }

        if (_dragMode == DragMode.Scrub)
        {
            _dragStarted = true;
            var x = Math.Clamp(e.GetPosition(this).X, 0, Bounds.Width);
            ScrubMoved?.Invoke(Math.Max(0, XToSeconds(x)));
            return;
        }

        var dx = e.GetPosition(this).X - _pressPoint.X;
        if (!_dragStarted && Math.Abs(dx) < DragThreshold)
        {
            return;
        }

        _dragStarted = true;
        if (_dragMode == DragMode.Pan)
        {
            ViewStartChanged?.Invoke(_pressViewStart - dx * SecondsPerPixel);
        }
        else
        {
            OffsetDragged?.Invoke(_pressOffset + dx * SecondsPerPixel);
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_dragMode == DragMode.None)
        {
            return;
        }

        if (_dragMode == DragMode.Scrub)
        {
            var x = Math.Clamp(e.GetPosition(this).X, 0, Bounds.Width);
            ScrubEnded?.Invoke(Math.Max(0, XToSeconds(x)));
        }
        else if (!_dragStarted)
        {
            SeekRequested?.Invoke(Math.Max(0, XToSeconds(_pressPoint.X)));
        }
        else if (_dragMode == DragMode.Offset)
        {
            OffsetDragFinished?.Invoke();
        }

        _dragMode = DragMode.None;
        e.Pointer.Capture(null);
    }

    /// <summary>Cmd on macOS (Ctrl+click there is a right click), Ctrl on Windows and Linux.</summary>
    public static bool IsScrubModifier(KeyModifiers modifiers) =>
        OperatingSystem.IsMacOS() ? modifiers.HasFlag(KeyModifiers.Meta) || modifiers.HasFlag(KeyModifiers.Control) : modifiers.HasFlag(KeyModifiers.Control);

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        var zoom = e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Meta);
        if (zoom)
        {
            ZoomRequested?.Invoke(e.Delta.Y > 0 ? 0.8 : 1.25, XToSeconds(e.GetPosition(this).X));
        }
        else
        {
            var delta = Math.Abs(e.Delta.X) > Math.Abs(e.Delta.Y) ? e.Delta.X : e.Delta.Y;
            ViewStartChanged?.Invoke(ViewStart - delta * ViewSeconds * 0.1);
        }

        e.Handled = true;
    }
}
