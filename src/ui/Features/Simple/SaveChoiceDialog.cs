using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using System.Threading.Tasks;

namespace Nikse.SubtitleEdit.Features.Simple;

public enum SaveDestination
{
    Cancel,
    SideFile,
    InsideVideo,
}

/// <summary>SAVE on an mkv: next to the video as .srt, or inside the video as a subtitle track.</summary>
public class SaveChoiceDialog : Window
{
    private SaveDestination _choice = SaveDestination.Cancel;

    public SaveChoiceDialog(string trackName, bool insideIsDefault)
    {
        var strings = SimpleStrings.Current;
        Title = strings.AppTitle;
        SizeToContent = SizeToContent.WidthAndHeight;
        CanResize = false;
        Background = BrutalTheme.PaperDim;
        FontFamily = BrutalTheme.Mono;
        RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Light;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Styles.Add(BrutalTheme.CreateStyles());

        var side = BrutalTheme.Button(strings.SaveSideFile, "SaveSideFile");
        side.Click += (_, _) => CloseWith(SaveDestination.SideFile);
        var inside = BrutalTheme.Button(string.Format(strings.SaveInsideVideo, trackName), "SaveInsideVideo");
        inside.Click += (_, _) => CloseWith(SaveDestination.InsideVideo);
        var cancel = BrutalTheme.Button(strings.Cancel, "SaveCancel");
        cancel.Click += (_, _) => CloseWith(SaveDestination.Cancel);

        var primary = insideIsDefault ? inside : side;
        primary.Background = BrutalTheme.Marker;
        primary.Classes.Add(BrutalTheme.PrimaryClass);

        var question = BrutalTheme.Label(strings.SaveWhere, 14);
        var help = BrutalTheme.Label(strings.SaveWhereHelp, 11);
        help.TextWrapping = Avalonia.Media.TextWrapping.Wrap;
        help.MaxWidth = 560;
        help.Opacity = 0.75;

        Content = new StackPanel
        {
            Margin = new Thickness(20),
            Spacing = 14,
            Children =
            {
                question,
                help,
                new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right, Children = { cancel, side, inside } },
            },
        };
        Opened += (_, _) => primary.Focus();
    }

    private void CloseWith(SaveDestination choice)
    {
        _choice = choice;
        Close();
    }

    public static async Task<SaveDestination> AskAsync(Window owner, string trackName, bool insideIsDefault)
    {
        var dialog = new SaveChoiceDialog(trackName, insideIsDefault);
        await dialog.ShowDialog(owner);
        return dialog._choice;
    }
}
