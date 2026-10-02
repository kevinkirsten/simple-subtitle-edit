using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace Nikse.SubtitleEdit.Features.Simple;

/// <summary>
/// The red playback cursor as its own layer over the timeline or the overview bar. Moving it only
/// changes a translate transform, so the waveform and subtitle blocks underneath are not redrawn
/// on every frame of playback.
/// </summary>
public class PlayheadOverlay : Border
{
    private readonly TranslateTransform _translate = new();

    public PlayheadOverlay()
    {
        Width = 2;
        Background = BrutalTheme.Cursor;
        HorizontalAlignment = HorizontalAlignment.Left;
        VerticalAlignment = VerticalAlignment.Stretch;
        IsHitTestVisible = false;
        RenderTransform = _translate;
        Avalonia.Automation.AutomationProperties.SetAutomationId(this, "Playhead");
    }

    /// <summary>Horizontal position in pixels of the hosting area; hidden when outside it.</summary>
    public void MoveTo(double x, double hostWidth)
    {
        var visible = x >= -1 && x <= hostWidth + 1;
        if (IsVisible != visible)
        {
            IsVisible = visible;
        }

        if (visible && System.Math.Abs(_translate.X - (x - 1)) > 0.01)
        {
            _translate.X = x - 1;
        }
    }

    public double X => _translate.X + 1;
}
