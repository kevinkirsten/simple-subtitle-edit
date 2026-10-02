using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Nikse.SubtitleEdit.UiLogic.SimpleSync;
using System.Linq;
using System.Threading.Tasks;

namespace Nikse.SubtitleEdit.Features.Simple;

public enum SaveDestination
{
    Cancel,
    SideFile,
    InsideVideo,
}

/// <summary>What the save dialog needs: the tracks already in the mkv and the suggested language.</summary>
public sealed record SaveRequest(MkvInfo? Info, string Ietf, int? EditedTrackNumber, bool InsideIsDefault);

public sealed record SaveChoice(SaveDestination Destination, string Ietf);

/// <summary>
/// SAVE on an mkv: next to the video as .srt, or inside the video as a subtitle track in the
/// chosen language, warning about what is already inside in that language.
/// </summary>
public class SaveChoiceDialog : Window
{
    private SaveChoice _choice;

    public SaveChoiceDialog(SaveRequest request)
    {
        var strings = SimpleStrings.Current;
        _choice = new SaveChoice(SaveDestination.Cancel, request.Ietf);
        Title = strings.AppTitle;
        SizeToContent = SizeToContent.WidthAndHeight;
        CanResize = false;
        Background = BrutalTheme.PaperDim;
        FontFamily = BrutalTheme.Mono;
        RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Light;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Styles.Add(BrutalTheme.CreateStyles());

        var languages = SubtitleLanguages.All.ToList();
        var current = SubtitleLanguages.For(request.Ietf);
        if (!languages.Contains(current))
        {
            languages.Insert(0, current);
        }

        var language = BrutalTheme.ComboBox("SaveLanguage", 300);
        language.ItemsSource = languages;
        language.SelectedItem = current;

        var warning = BrutalTheme.Label(string.Empty, 12);
        warning.TextWrapping = Avalonia.Media.TextWrapping.Wrap;
        warning.MaxWidth = 600;
        Avalonia.Automation.AutomationProperties.SetAutomationId(warning, "SaveLanguageWarning");

        var side = BrutalTheme.Button(strings.SaveSideFile, "SaveSideFile");
        side.Click += (_, _) => CloseWith(SaveDestination.SideFile, language);
        var inside = BrutalTheme.Button(string.Empty, "SaveInsideVideo");
        inside.Click += (_, _) => CloseWith(SaveDestination.InsideVideo, language);
        var cancel = BrutalTheme.Button(strings.Cancel, "SaveCancel");
        cancel.Click += (_, _) => CloseWith(SaveDestination.Cancel, language);

        void Update()
        {
            var lang = (SubtitleLanguage?)language.SelectedItem ?? current;
            BrutalTheme.SetText(inside, string.Format(strings.SaveInsideVideo, lang.Name));
            warning.Text = DescribeExisting(request, lang.Ietf);
        }

        language.SelectionChanged += (_, _) => Update();
        Update();

        var primary = request.InsideIsDefault ? inside : side;
        primary.Background = BrutalTheme.Marker;
        primary.Classes.Add(BrutalTheme.PrimaryClass);

        var question = BrutalTheme.Label(strings.SaveWhere, 14);
        var help = BrutalTheme.Label(strings.SaveWhereHelp, 11);
        help.TextWrapping = Avalonia.Media.TextWrapping.Wrap;
        help.MaxWidth = 600;
        help.Opacity = 0.75;

        Content = new StackPanel
        {
            Margin = new Thickness(20),
            Spacing = 14,
            Children =
            {
                question,
                new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, Children = { BrutalTheme.Label(strings.SaveLanguageLabel, 12), language, BrutalTheme.InfoIcon(strings.SaveLanguageInfo, "SaveLanguageInfo") } },
                warning,
                help,
                new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right, Children = { cancel, side, inside } },
            },
        };
        Opened += (_, _) => primary.Focus();
    }

    /// <summary>The warning line under the language picker.</summary>
    public static string DescribeExisting(SaveRequest request, string ietf)
    {
        var strings = SimpleStrings.Current;
        if (request.Info == null)
        {
            return string.Empty;
        }

        var existing = ExistingTracks.For(request.Info, ietf, request.EditedTrackNumber);
        var parts = new System.Collections.Generic.List<string>();
        if (existing.Replaced.Count > 0)
        {
            parts.Add(string.Format(strings.SaveWillReplace, string.Join(", ", existing.Replaced.Select(t => ExistingTracks.Describe(t, strings.TrackText, strings.TrackImage)))));
        }

        if (existing.Kept.Count > 0)
        {
            parts.Add(string.Format(strings.SaveWillKeep, string.Join(", ", existing.Kept.Select(t => ExistingTracks.Describe(t, strings.TrackText, strings.TrackImage)))));
        }

        return parts.Count > 0 ? string.Join(" ", parts) : strings.SaveNothingInLanguage;
    }

    private void CloseWith(SaveDestination destination, ComboBox language)
    {
        _choice = new SaveChoice(destination, ((SubtitleLanguage?)language.SelectedItem)?.Ietf ?? _choice.Ietf);
        Close();
    }

    public static async Task<SaveChoice> AskAsync(Window owner, SaveRequest request)
    {
        var dialog = new SaveChoiceDialog(request);
        await dialog.ShowDialog(owner);
        return dialog._choice;
    }
}
