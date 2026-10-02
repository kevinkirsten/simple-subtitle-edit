using Avalonia.Controls;

namespace Nikse.SubtitleEdit.Features.Simple;

/// <summary>Going between the simple window and the full editor (ADVANCED MODE).</summary>
public static class SimpleModeSwitch
{
    /// <summary>
    /// Shows the simple window (creating it if it was closed) and closes the given editor window.
    /// If the editor has unsaved changes it asks first, and cancelling keeps both open.
    /// </summary>
    public static void BackToSimple(Window? editor)
    {
        ShowSimpleWindow();
        editor?.Close();
    }

    /// <summary>The simple window, brought to front; a new one if the old one was closed.</summary>
    public static SimpleWindow ShowSimpleWindow()
    {
        var window = Program.SimpleWindowInstance;
        if (window == null || window.IsClosed)
        {
            window = new SimpleWindow { Icon = Logic.UiUtil.GetSeIcon() };
            Logic.UiTheme.ApplyScaleToWindow(window);
            Program.SimpleWindowInstance = window;
            Program.HookSimpleWindowExit(null, window);
        }

        window.Show();
        window.Activate();
        return window;
    }

    /// <summary>When the last editor window closes: back to the simple window if there is one.</summary>
    public static bool TryReturnToSimple()
    {
        if (Program.SimpleWindowInstance is { IsClosed: false } window)
        {
            window.Show();
            window.Activate();
            return true;
        }

        return false;
    }
}
