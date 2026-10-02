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
    private PlexSettings _plex;
    private readonly TextBox _plexUrl;
    private readonly TextBlock _plexStatus;
    private System.Threading.CancellationTokenSource? _plexWork;

    /// <summary>Where the Plex settings are written on SAVE. Tests replace it.</summary>
    public System.Action<PlexSettings> SavePlex { get; set; } = SimpleSettingsStore.SavePlex;

    /// <summary>HTTP for Plex and plex.tv. Tests replace it with a fake.</summary>
    public System.Net.Http.HttpClient PlexHttp { get; set; } = PlexNotifier.Http;

    /// <summary>Opens the plex.tv approval page. Tests replace it.</summary>
    public System.Func<string, Task> OpenBrowser { get; set; }

    public OnlineSettingsDialog(OpenSubtitlesSettings current, PlexSettings? plex = null)
    {
        _plex = plex ?? new PlexSettings();
        OpenBrowser = url => Launcher.LaunchUriAsync(new System.Uri(url));
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

        // --- Plex ---------------------------------------------------------------------
        _plexUrl = new TextBox
        {
            Text = _plex.Url,
            Width = 330,
            FontFamily = BrutalTheme.Mono,
            Foreground = BrutalTheme.Ink,
            Background = BrutalTheme.Paper,
            BorderBrush = BrutalTheme.Ink,
            BorderThickness = BrutalTheme.Line,
            CornerRadius = new CornerRadius(0),
        };
        Avalonia.Automation.AutomationProperties.SetAutomationId(_plexUrl, "PlexUrl");
        var detect = BrutalTheme.Button(strings.PlexDetect, "PlexDetect");
        detect.Click += async (_, _) => await DetectPlexAsync();
        var signIn = BrutalTheme.Button(strings.PlexSignIn, "PlexSignIn");
        signIn.Click += async (_, _) => await SignInWithPlexAsync();
        _plexStatus = BrutalTheme.Label(string.Empty, 12);
        _plexStatus.TextWrapping = Avalonia.Media.TextWrapping.Wrap;
        _plexStatus.MaxWidth = 560;
        Avalonia.Automation.AutomationProperties.SetAutomationId(_plexStatus, "PlexStatus");

        var plexTitle = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 10,
            Margin = new Thickness(0, 8, 0, 0),
            Children = { BrutalTheme.Label(strings.PlexSection, 14), BrutalTheme.InfoIcon(strings.PlexInfo, "PlexInfo") },
        };
        var plexServer = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 12,
            Children = { BrutalTheme.Label(strings.PlexServer, 12), _plexUrl },
        };
        var plexButtons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { detect, signIn } };
        Opened += async (_, _) =>
        {
            if (_plex.IsConfigured)
            {
                await CheckPlexAsync();
            }
        };

        var help = BrutalTheme.Label(strings.OnlineHelp, 11);
        help.TextWrapping = Avalonia.Media.TextWrapping.Wrap;
        help.MaxWidth = 520;
        help.Opacity = 0.75;

        var save = BrutalTheme.Button(strings.Save, "OnlineSave");
        save.Background = BrutalTheme.Marker;
        save.Classes.Add(BrutalTheme.PrimaryClass);
        save.Click += (_, _) =>
        {
            var plexNow = _plex with { Url = (_plexUrl.Text ?? string.Empty).Trim() };
            if (plexNow != plex)
            {
                try
                {
                    SavePlex(plexNow);
                }
                catch (System.Exception)
                {
                    // keeps working without Plex
                }
            }

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
                plexTitle,
                plexServer,
                plexButtons,
                _plexStatus,
                new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right, Children = { cancel, save } },
            },
        };
    }

    /// <summary>The new settings, or null when cancelled.</summary>
    private System.Threading.CancellationToken RestartPlexWork()
    {
        _plexWork?.Cancel();
        _plexWork = new System.Threading.CancellationTokenSource();
        return _plexWork.Token;
    }

    /// <returns>True when connected.</returns>
    public async Task<bool> CheckPlexAsync()
    {
        var strings = SimpleStrings.Current;
        var token = RestartPlexWork();
        var url = (_plexUrl.Text ?? string.Empty).Trim();
        _plexStatus.Text = strings.PlexChecking;
        try
        {
            var name = await new PlexClient(PlexHttp, url, _plex.Token).CheckAsync(token);
            _plex = _plex with { Url = url, ServerName = name };
            _plexStatus.Text = string.Format(strings.PlexConnected, name);
            return true;
        }
        catch (PlexException ex)
        {
            _plexStatus.Text = string.Format(strings.PlexNotConnected, ex.Message);
            return false;
        }
        catch (System.OperationCanceledException)
        {
            return false;
        }
    }

    public async Task DetectPlexAsync()
    {
        var found = PlexNotifier.DetectLocal();
        if (found == null)
        {
            _plexStatus.Text = SimpleStrings.Current.PlexDetectFailed;
            return;
        }

        _plex = _plex with { Url = found.Url, Token = found.Token };
        _plexUrl.Text = found.Url;
        await CheckPlexAsync();
    }

    public async Task SignInWithPlexAsync()
    {
        var strings = SimpleStrings.Current;
        var token = RestartPlexWork();
        try
        {
            var signIn = new PlexSignIn(PlexHttp, _plex.ClientId);
            var pin = await signIn.CreatePinAsync(token);
            await OpenBrowser(signIn.AuthUrl(pin));
            _plexStatus.Text = strings.PlexWaitingBrowser;

            var userToken = await signIn.WaitForTokenAsync(pin, System.TimeSpan.FromMinutes(3), System.TimeSpan.FromSeconds(2), token);
            if (userToken == null)
            {
                _plexStatus.Text = strings.PlexSignInTimeout;
                return;
            }

            var server = (await signIn.ServersAsync(userToken, token)).FirstOrDefault();
            if (server == null)
            {
                _plexStatus.Text = strings.PlexNoServer;
                return;
            }

            _plex = _plex with { Url = server.Url, Token = server.AccessToken, ServerName = server.Name };
            _plexUrl.Text = server.Url;
            await CheckPlexAsync();
        }
        catch (PlexException ex)
        {
            _plexStatus.Text = string.Format(strings.PlexNotConnected, ex.Message);
        }
        catch (System.Net.Http.HttpRequestException ex)
        {
            _plexStatus.Text = string.Format(strings.PlexNotConnected, ex.Message);
        }
        catch (System.OperationCanceledException)
        {
            // closed or restarted
        }
    }

    public static async Task<OpenSubtitlesSettings?> AskAsync(Window owner, OpenSubtitlesSettings current)
    {
        var dialog = new OnlineSettingsDialog(current, SimpleSettingsStore.LoadPlex());
        dialog.Closed += (_, _) => dialog._plexWork?.Cancel();
        await dialog.ShowDialog(owner);
        return dialog._result;
    }
}
