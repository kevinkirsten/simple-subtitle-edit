using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Nikse.SubtitleEdit;
using Nikse.SubtitleEdit.Features.Simple;
using Xunit;

namespace UITests.Features.Simple;

/// <summary>ADVANCED MODE hides the simple window; the editor's "◀ Simple mode" (or closing it) brings it back.</summary>
public class SimpleModeSwitchTests
{
    [AvaloniaFact]
    public void BackToSimple_ShowsTheHiddenSimpleWindow_AndClosesTheEditor()
    {
        var simple = new SimpleWindow(createPlayer: false, new FakeVideoPlayer());
        var previous = Program.SimpleWindowInstance;
        Program.SimpleWindowInstance = simple;
        try
        {
            simple.Show();
            simple.Hide(); // what ADVANCED MODE does
            var editor = new Window();
            editor.Show();

            SimpleModeSwitch.BackToSimple(editor);
            Dispatcher.UIThread.RunJobs();

            Assert.True(simple.IsVisible);
            Assert.False(editor.IsVisible);
            simple.Close();
        }
        finally
        {
            Program.SimpleWindowInstance = previous;
        }
    }

    [AvaloniaFact]
    public void LastEditorClosed_ReturnsToSimple_OnlyIfItIsStillOpen()
    {
        var simple = new SimpleWindow(createPlayer: false, new FakeVideoPlayer());
        var previous = Program.SimpleWindowInstance;
        Program.SimpleWindowInstance = simple;
        try
        {
            simple.Show();
            simple.Hide();

            Assert.True(SimpleModeSwitch.TryReturnToSimple());
            Assert.True(simple.IsVisible);

            simple.Close();
            Assert.True(simple.IsClosed);
            Assert.False(SimpleModeSwitch.TryReturnToSimple()); // closed for good: the app may exit
        }
        finally
        {
            Program.SimpleWindowInstance = previous;
        }
    }
}
