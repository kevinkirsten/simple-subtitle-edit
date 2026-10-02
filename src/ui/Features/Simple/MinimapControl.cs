using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Nikse.SubtitleEdit.Logic.Media;
using Nikse.SubtitleEdit.UiLogic.SimpleSync;
using System;

namespace Nikse.SubtitleEdit.Features.Simple;

/// <summary>
/// The whole video in one bar: audio overview, yellow marks where there is subtitle text, and a
/// blue box showing which part the timeline is zoomed into. Click or drag to jump there.
/// </summary>
public class MinimapControl : Control
{
    public WavePeakData2? Peaks { get; set; }
    public SyncSession? Session { get; set; }
    public double Duration { get; set; }
    public double ViewStart { get; set; }
    public double ViewSeconds { get; set; }
    public double Position { get; set; }

    /// <summary>Asks to center the timeline on this second.</summary>
    public event Action<double>? JumpRequested;

    private bool _dragging;
    private int[]? _overview;
    private WavePeakData2? _overviewSource;
    private int _overviewWidth;

    public MinimapControl()
    {
        ClipToBounds = true;
        Cursor = BrutalTheme.Hand;
        Avalonia.Automation.AutomationProperties.SetAutomationId(this, "Minimap");
    }

    public double SecondsToX(double seconds) => Duration > 0 ? seconds / Duration * Bounds.Width : 0;

    public double XToSeconds(double x) => Bounds.Width > 0 ? Math.Clamp(x / Bounds.Width, 0, 1) * Duration : 0;

    public override void Render(DrawingContext context)
    {
        var width = Bounds.Width;
        var height = Bounds.Height;
        if (width <= 0 || height <= 0)
        {
            return;
        }

        context.FillRectangle(BrutalTheme.Paper, new Rect(0, 0, width, height));
        if (Duration <= 0)
        {
            return;
        }

        var markTop = height * 0.6;
        DrawOverview(context, (int)width, 2, markTop - 2);

        context.FillRectangle(BrutalTheme.PaperDim, new Rect(0, markTop, width, height - markTop));
        if (Session != null)
        {
            var coverage = Session.Coverage(Duration, (int)width);
            for (var x = 0; x < coverage.Length; x++)
            {
                if (coverage[x])
                {
                    context.FillRectangle(BrutalTheme.Marker, new Rect(x, markTop + 2, 1, height - markTop - 4));
                }
            }
        }

        context.DrawLine(BrutalTheme.ThinInkPen, new Point(0, markTop), new Point(width, markTop));

        var vx = SecondsToX(ViewStart);
        var vw = Math.Max(SecondsToX(ViewStart + ViewSeconds) - vx, 4);
        var viewport = new Rect(vx, 0, vw, height);
        context.FillRectangle(BrutalTheme.Viewport, viewport);
        context.DrawRectangle(new Pen(new SolidColorBrush(Color.Parse("#0057FF")), 2), viewport);

        RenderCount++;
    }

    public int RenderCount { get; private set; }

    private void DrawOverview(DrawingContext context, int width, double top, double bottom)
    {
        var peaks = Peaks;
        if (peaks == null || peaks.Peaks.Count == 0 || peaks.HighestPeak <= 0 || width <= 0)
        {
            return;
        }

        if (_overview == null || !ReferenceEquals(_overviewSource, peaks) || _overviewWidth != width)
        {
            _overview = BuildOverview(peaks, width);
            _overviewSource = peaks;
            _overviewWidth = width;
            _overviewGeometry = null;
        }

        if (_overviewGeometry == null || _overviewGeometryHeight != bottom)
        {
            var mid = (top + bottom) / 2;
            var half = (bottom - top) / 2;
            var geometry = new StreamGeometry();
            using (var ctx = geometry.Open())
            {
                for (var x = 0; x < _overview.Length; x++)
                {
                    var h = _overview[x] * half / peaks.HighestPeak;
                    ctx.BeginFigure(new Point(x + 0.5, mid - h), false);
                    ctx.LineTo(new Point(x + 0.5, mid + h + 1));
                    ctx.EndFigure(false);
                }
            }

            _overviewGeometry = geometry;
            _overviewGeometryHeight = bottom;
        }

        context.DrawGeometry(null, OverviewPen, _overviewGeometry);
    }

    private static readonly Pen OverviewPen = new(BrutalTheme.Wave, 1);
    private StreamGeometry? _overviewGeometry;
    private double _overviewGeometryHeight;

    /// <summary>Loudest peak per pixel column, computed once per width.</summary>
    public static int[] BuildOverview(WavePeakData2 peaks, int width)
    {
        var result = new int[width];
        var span = peaks.AsSpan();
        for (var x = 0; x < width; x++)
        {
            var from = (int)((long)span.Length * x / width);
            var to = Math.Max(from + 1, (int)((long)span.Length * (x + 1) / width));
            var max = 0;
            for (var i = from; i < to && i < span.Length; i++)
            {
                max = Math.Max(max, span[i].Abs);
            }

            result[x] = max;
        }

        return result;
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        _dragging = true;
        e.Pointer.Capture(this);
        JumpRequested?.Invoke(XToSeconds(e.GetPosition(this).X));
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_dragging)
        {
            JumpRequested?.Invoke(XToSeconds(e.GetPosition(this).X));
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        _dragging = false;
        e.Pointer.Capture(null);
    }
}
