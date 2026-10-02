using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Nikse.SubtitleEdit.UiLogic.SimpleSync;
using System.Linq;
using System.Threading.Tasks;

namespace Nikse.SubtitleEdit.Features.Simple;

/// <summary>OpenSubtitles.com API key and login, asked the first time FIND ONLINE is used.</summary>
public class OnlineSettingsDialog : Window
{
    private OpenSubtitlesSettings? _result;

    public OnlineSettingsDialog(OpenSubtitlesSettings current)
    {
        var strings = SimpleStrings.Current;
        Title = strings.OnlineSettingsTitle;
        SizeToContent = SizeToContent.WidthAndHeight;
        CanResize = false;
        Background = BrutalTheme.PaperDim;
        FontFamily = BrutalTheme.Mono;
        RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Light;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Styles.Add(BrutalTheme.CreateStyles());

        TextBox Field(string value, string id, char password = '\0')
        {
            var box = new TextBox
            {
                Text = value,
                Width = 360,
                FontFamily = BrutalTheme.Mono,
                Foreground = BrutalTheme.Ink,
                Background = BrutalTheme.Paper,
                BorderBrush = BrutalTheme.Ink,
                BorderThickness = BrutalTheme.Line,
                CornerRadius = new CornerRadius(0),
                PasswordChar = password,
            };
            Avalonia.Automation.AutomationProperties.SetAutomationId(box, id);
            return box;
        }

        var apiKey = Field(current.ApiKey, "OnlineApiKey");
        var appName = Field(current.AppName, "OnlineAppName");
        var username = Field(current.Username, "OnlineUsername");
        var password = Field(current.Password, "OnlinePassword", '•');
        var languages = SubtitleLanguages.All.ToList();
        var currentLanguage = SubtitleLanguages.FromOpenSubtitles(current.Language);
        if (!languages.Contains(currentLanguage))
        {
            languages.Insert(0, currentLanguage);
        }

        var language = BrutalTheme.ComboBox("OnlineLanguage", 330);
        language.ItemsSource = languages;
        language.SelectedItem = currentLanguage;

        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,Auto"), RowDefinitions = new RowDefinitions("Auto,Auto,Auto,Auto,Auto"), RowSpacing = 8, ColumnSpacing = 12 };
        void AddRow(int row, string label, Control field)
        {
            var text = BrutalTheme.Label(label, 12);
            Grid.SetRow(text, row);
            Grid.SetRow(field, row);
            Grid.SetColumn(field, 1);
            grid.Children.Add(text);
            grid.Children.Add(field);
        }

        AddRow(0, strings.OnlineApiKey, apiKey);
        AddRow(1, strings.OnlineAppName, appName);
        AddRow(2, strings.OnlineUsername, username);
        AddRow(3, strings.OnlinePassword, password);
        AddRow(4, strings.OnlineLanguage, new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 10,
            Children = { language, BrutalTheme.InfoIcon(strings.OnlineLanguageInfo, "OnlineLanguageInfo") },
        });

        var help = BrutalTheme.Label(strings.OnlineHelp, 11);
        help.TextWrapping = Avalonia.Media.TextWrapping.Wrap;
        help.MaxWidth = 520;
        help.Opacity = 0.75;

        var save = BrutalTheme.Button(strings.Save, "OnlineSave");
        save.Background = BrutalTheme.Marker;
        save.Classes.Add(BrutalTheme.PrimaryClass);
        save.Click += (_, _) =>
        {
            _result = current with
            {
                ApiKey = apiKey.Text?.Trim() ?? string.Empty,
                AppName = string.IsNullOrWhiteSpace(appName.Text) ? "SimpleSubtitleEdit" : appName.Text.Trim(),
                Username = username.Text?.Trim() ?? string.Empty,
                Password = password.Text ?? string.Empty,
                Language = SubtitleLanguages.ToOpenSubtitles(((SubtitleLanguage?)language.SelectedItem ?? currentLanguage).Ietf),
            };
            Close();
        };
        var cancel = BrutalTheme.Button(strings.Cancel, "OnlineCancel");
        cancel.Click += (_, _) => Close();

        Content = new StackPanel
        {
            Margin = new Thickness(20),
            Spacing = 14,
            Children =
            {
                grid,
                help,
                new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right, Children = { cancel, save } },
            },
        };
    }

    /// <summary>The new settings, or null when cancelled.</summary>
    public static async Task<OpenSubtitlesSettings?> AskAsync(Window owner, OpenSubtitlesSettings current)
    {
        var dialog = new OnlineSettingsDialog(current);
        await dialog.ShowDialog(owner);
        return dialog._result;
    }
}
