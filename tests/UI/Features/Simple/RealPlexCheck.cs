using Avalonia.Headless.XUnit;
using Nikse.SubtitleEdit.Features.Simple;
using Nikse.SubtitleEdit.UiLogic.SimpleSync;
using System;
using System.Threading.Tasks;
using Xunit;

namespace UITests.Features.Simple;

/// <summary>Against a real local Plex. Runs only with SSE_REAL_PLEX_VIDEO set to a video in its library.</summary>
public class RealPlexCheck
{
    [AvaloniaFact]
    public async Task DetectCheckAndRefresh()
    {
        var video = Environment.GetEnvironmentVariable("SSE_REAL_PLEX_VIDEO");
        if (string.IsNullOrEmpty(video))
        {
            Assert.Skip("set SSE_REAL_PLEX_VIDEO to run against a real Plex");
        }

        var local = PlexNotifier.DetectLocal();
        Assert.NotNull(local);
        var client = new PlexClient(PlexNotifier.Http, local!.Url, local.Token);
        var name = await client.CheckAsync(TestContext.Current.CancellationToken);
        Assert.False(string.IsNullOrEmpty(name));
        Assert.True(await client.RefreshAsync(video!, TestContext.Current.CancellationToken), "episode not found in Plex");
        TestContext.Current.SendDiagnosticMessage("Connected to: " + name);
    }
}
