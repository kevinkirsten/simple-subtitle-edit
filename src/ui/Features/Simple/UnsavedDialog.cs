using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using System.Threading.Tasks;

namespace Nikse.SubtitleEdit.Features.Simple;

public enum LeaveChoice
{
    Cancel,
    Save,
    Discard,
}

/// <summary>"Offset changed and not saved": save, discard or stay. Same square look as the main window.</summary>
public class UnsavedDialog : Window
{
    private LeaveChoice _choice = LeaveChoice.Cancel;

    public UnsavedDialog()
    {
        var strings = SimpleStrings.Current;
        Title = strings.AppTitle;
        SizeToContent = SizeToContent.WidthAndHeight;
        CanResize = false;
        Background = BrutalTheme.PaperDim;
        FontFamily = BrutalTheme.Mono;
        RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Light;
        Styles.Add(BrutalTheme.CreateStyles());
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var save = BrutalTheme.Button(strings.SaveAndGo, "DialogSave");
        save.Background = BrutalTheme.Marker;
        save.Classes.Add(BrutalTheme.PrimaryClass);
        save.Click += (_, _) => CloseWith(LeaveChoice.Save);
        var discard = BrutalTheme.Button(strings.DiscardAndGo, "DialogDiscard");
        discard.Click += (_, _) => CloseWith(LeaveChoice.Discard);
        var cancel = BrutalTheme.Button(strings.Cancel, "DialogCancel");
        cancel.Click += (_, _) => CloseWith(LeaveChoice.Cancel);

        var message = BrutalTheme.Label(strings.UnsavedQuestion, 14);
        message.MaxWidth = 460;
        message.TextWrapping = Avalonia.Media.TextWrapping.Wrap;

        Content = new StackPanel
        {
            Margin = new Thickness(20),
            Spacing = 16,
            Children =
            {
                message,
                new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right, Children = { cancel, discard, save } },
            },
        };
    }

    private void CloseWith(LeaveChoice choice)
    {
        _choice = choice;
        Close();
    }

    public static async Task<LeaveChoice> AskAsync(Window owner)
    {
        var dialog = new UnsavedDialog();
        await dialog.ShowDialog(owner);
        return dialog._choice;
    }
}
