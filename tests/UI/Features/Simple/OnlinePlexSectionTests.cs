using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Nikse.SubtitleEdit.Features.Simple;
using Nikse.SubtitleEdit.UiLogic.SimpleSync;
using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace UITests.Features.Simple;

/// <summary>The PLEX section of ⚙, with plex.tv and the server replaced by fakes.</summary>
public class OnlinePlexSectionTests
{
    private sealed class FakePlex : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r, CancellationToken cancellationToken)
        {
            static HttpResponseMessage Xml(string xml) => new(HttpStatusCode.OK) { Content = new StringContent(xml, Encoding.UTF8, "application/xml") };
            var token = r.Headers.TryGetValues("X-Plex-Token", out var t) ? t.First() : null;
            return Task.FromResult((r.RequestUri!.Host, r.RequestUri.AbsolutePath) switch
            {
                ("plex.tv", "/api/v2/pins") => Xml("""<pin id="7" code="CODE7" authToken=""/>"""),
                ("plex.tv", "/api/v2/pins/7") => Xml("""<pin id="7" authToken="USER"/>"""),
                ("plex.tv", "/api/v2/resources") => Xml("""<resources><resource name="Living room NAS" provides="server" owned="1" accessToken="SERVERTOKEN"><connections><connection uri="http://192.168.1.50:32400" local="1" relay="0"/></connections></resource></resources>"""),
                ("192.168.1.50", "/") when token == "SERVERTOKEN" => Xml("""<MediaContainer friendlyName="Living room NAS"/>"""),
                ("192.168.1.50", "/library/sections") when token == "SERVERTOKEN" => Xml("<MediaContainer/>"),
                _ => new HttpResponseMessage(HttpStatusCode.Unauthorized),
            });
        }
    }

    private static T Find<T>(Window w, string id) where T : Control =>
        w.GetVisualDescendants().OfType<T>().First(c => AutomationProperties.GetAutomationId(c) == id);

    [AvaloniaFact]
    public async Task SignInWithPlex_FillsTheServer_ShowsConnected_AndSaveKeepsIt()
    {
        SimpleStrings.Current = SimpleStrings.English;
        PlexSettings? saved = null;
        string? openedUrl = null;
        var dialog = new OnlineSettingsDialog(new OpenSubtitlesSettings(), new PlexSettings { ClientId = "client-1" })
        {
            PlexHttp = new HttpClient(new FakePlex()),
            SavePlex = p => saved = p,
        };
        dialog.OpenBrowser = url => { openedUrl = url; return Task.CompletedTask; };
        dialog.Show();
        Dispatcher.UIThread.RunJobs();

        await dialog.SignInWithPlexAsync();

        Assert.Contains("code=CODE7", openedUrl);
        Assert.Equal("http://192.168.1.50:32400", Find<TextBox>(dialog, "PlexUrl").Text);
        Assert.Equal("✓ Connected to Plex: Living room NAS", Find<TextBlock>(dialog, "PlexStatus").Text);

        Find<Button>(dialog, "OnlineSave").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert.NotNull(saved);
        Assert.Equal("SERVERTOKEN", saved!.Token);
        Assert.Equal("http://192.168.1.50:32400", saved.Url);
        Assert.Equal("Living room NAS", saved.ServerName);
    }

    [AvaloniaFact]
    public async Task WrongAddress_SaysNotConnected()
    {
        SimpleStrings.Current = SimpleStrings.English;
        var dialog = new OnlineSettingsDialog(new OpenSubtitlesSettings(), new PlexSettings { Url = "http://192.168.1.99:32400", Token = "OLD" })
        {
            PlexHttp = new HttpClient(new FakePlex()),
        };
        dialog.Show();

        Assert.False(await dialog.CheckPlexAsync());

        Assert.StartsWith("Not connected:", Find<TextBlock>(dialog, "PlexStatus").Text);
        dialog.Close();
    }
}
