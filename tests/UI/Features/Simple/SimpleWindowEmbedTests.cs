using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Features.Simple;
using Nikse.SubtitleEdit.UiLogic.SimpleSync;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace UITests.Features.Simple;

/// <summary>SAVE → INSIDE THE VIDEO on a real mkv, with real mkvmerge and ffmpeg (skipped without them).</summary>
public sealed class SimpleWindowEmbedTests : IDisposable
{
    private readonly DirectoryInfo _dir = Directory.CreateTempSubdirectory("sse-embed-ui-");

    public void Dispose() => _dir.Delete(recursive: true);

    private async Task<string> MakeMkvAsync()
    {
        var ffmpeg = WaveformService.FindFfmpeg();
        if (ffmpeg == null || MatroskaEmbedder.FindMkvmerge() == null)
        {
            Assert.Skip("ffmpeg and mkvmerge are needed");
        }

        var video = Path.Combine(_dir.FullName, "Show - S01E01.mkv");
        var make = Process.Start(ffmpeg!, ["-v", "error", "-y", "-f", "lavfi", "-i", "testsrc=size=160x90:rate=10", "-f", "lavfi", "-i", "sine", "-t", "20", "-c:v", "libx264", "-c:a", "aac", video])!;
        await make.WaitForExitAsync(TestContext.Current.CancellationToken);
        File.WriteAllText(Path.ChangeExtension(video, ".srt"), "1\n00:00:03,000 --> 00:00:04,000\nAtrasada\n\n2\n00:00:30,000 --> 00:00:31,000\nDepois do fim\n");
        return video;
    }

    [AvaloniaFact]
    public async Task SaveInsideVideo_EmbedsPtBr_DeletesTheSrt_AndEditsTheEmbeddedTrackNextTime()
    {
        var video = await MakeMkvAsync();
        var player = new FakeVideoPlayer(20);
        var plexCalls = 0;
        var window = new SimpleWindow(createPlayer: false, player)
        {
            AskSaveDestination = _ => Task.FromResult(new SaveChoice(SaveDestination.InsideVideo, "pt-BR")),
        };
        window.ViewModel.PlexRefresh = _ => { plexCalls++; return Task.FromResult(true); };
        window.Show();
        await window.OpenVideoAsync(video);
        Assert.Equal(SubtitleSourceKind.File, window.ViewModel.SelectedSource!.Kind);

        window.ViewModel.SetOffset(-1);
        Assert.True(await window.SaveNowAsync());
        Dispatcher.UIThread.RunJobs();

        Assert.False(File.Exists(Path.ChangeExtension(video, ".srt")));
        var embedded = window.ViewModel.SelectedSource!;
        Assert.Equal(SubtitleSourceKind.Matroska, embedded.Kind);
        Assert.Contains("Português (Brasil)", embedded.DisplayName);
        Assert.Equal(1, plexCalls);
        Assert.Contains("Plex", window.ViewModel.StatusText);
        Assert.Equal(video, player.FileName); // the player reopened the rewritten file
        var lines = window.ViewModel.Session!.Original.Paragraphs;
        Assert.Equal("Atrasada", Assert.Single(lines).Text); // "Depois do fim" was after the 20 s video
        Assert.Equal(2000, lines[0].StartTime.TotalMilliseconds);

        // Second round: editing the embedded track and saving replaces it (no duplicate).
        window.ViewModel.SetOffset(0.5);
        Assert.True(await window.SaveNowAsync());
        var info = await MatroskaEmbedder.IdentifyAsync(MatroskaEmbedder.FindMkvmerge()!, video, TestContext.Current.CancellationToken);
        Assert.Single(info.Tracks, t => t.Type == "subtitles");
        Assert.Equal(2500, window.ViewModel.Session!.Original.Paragraphs[0].StartTime.TotalMilliseconds);
        window.Close();
    }

    [AvaloniaFact]
    public async Task SaveInsideVideo_InAnotherLanguage_AndTheDialogSeesWhatIsInside()
    {
        var video = await MakeMkvAsync();
        SaveRequest? lastRequest = null;
        var language = "en";
        var window = new SimpleWindow(createPlayer: false, new FakeVideoPlayer(20))
        {
            AskSaveDestination = r => { lastRequest = r; return Task.FromResult(new SaveChoice(SaveDestination.InsideVideo, language)); },
        };
        window.Show();
        await window.OpenVideoAsync(video);

        Assert.True(await window.SaveNowAsync());
        Assert.Equal("pt-BR", lastRequest!.Ietf); // suggestion before the user picked another
        Assert.Equal("en", window.ViewModel.EmbedLanguage); // remembered for next time
        Assert.Contains("English", window.ViewModel.SelectedSource!.DisplayName);

        // Second save, Portuguese: the dialog is told the English track is inside, and it stays.
        language = "pt-BR";
        Assert.True(await window.SaveNowAsync());
        Assert.Contains(lastRequest!.Info!.Tracks, t => t.LanguageIetf == "en");
        Assert.Equal("Nothing in this language inside the video yet.", SaveChoiceDialog.DescribeExisting(lastRequest, "pt-BR"));
        Assert.StartsWith("⚠ Already inside in this language, will be REPLACED: #", SaveChoiceDialog.DescribeExisting(lastRequest, "en"));

        var info = await MatroskaEmbedder.IdentifyAsync(MatroskaEmbedder.FindMkvmerge()!, video, TestContext.Current.CancellationToken);
        Assert.Equal(["en", "pt-BR"], info.Tracks.Where(t => t.Type == "subtitles").Select(t => t.LanguageIetf).OrderBy(x => x));
        window.Close();
    }

    [AvaloniaFact]
    public void SaveDialog_ShowsTheLanguagePicker_AndUpdatesTheWarning()
    {
        SimpleStrings.Current = SimpleStrings.English;
        var info = new MkvInfo(1, [new MkvTrack(13, 14, "subtitles", "HDMV PGS", "por", "", ""), new MkvTrack(14, 15, "subtitles", "SubRip/SRT", "por", "pt-BR", "Português (Brasil)")]);
        var dialog = new SaveChoiceDialog(new SaveRequest(info, "pt-BR", null, InsideIsDefault: false));
        dialog.Show();
        Dispatcher.UIThread.RunJobs();
        var picker = Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(dialog).OfType<Avalonia.Controls.ComboBox>().Single();
        var warning = Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(dialog).OfType<Avalonia.Controls.TextBlock>()
            .First(t => Avalonia.Automation.AutomationProperties.GetAutomationId(t) == "SaveLanguageWarning");

        Assert.Equal("pt-BR", ((SubtitleLanguage)picker.SelectedItem!).Ietf);
        Assert.Contains("REPLACED: #15 Português (Brasil) (text)", warning.Text);
        Assert.Contains("kept as they are: #14 por (image)", warning.Text);

        picker.SelectedItem = SubtitleLanguages.All.First(l => l.Ietf == "es");
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("Nothing in this language inside the video yet.", warning.Text);
        dialog.Close();
    }

    [AvaloniaFact]
    public async Task SaveChoice_Cancel_ChangesNothing()
    {
        var video = await MakeMkvAsync();
        var window = new SimpleWindow(createPlayer: false, new FakeVideoPlayer(20))
        {
            AskSaveDestination = _ => Task.FromResult(new SaveChoice(SaveDestination.Cancel, "pt-BR")),
        };
        window.Show();
        await window.OpenVideoAsync(video);
        var before = File.GetLastWriteTimeUtc(video);
        window.ViewModel.SetOffset(-1);

        Assert.False(await window.SaveNowAsync());

        Assert.True(File.Exists(Path.ChangeExtension(video, ".srt")));
        Assert.Equal(before, File.GetLastWriteTimeUtc(video));
        Assert.True(window.ViewModel.IsDirty);
        window.Close();
    }
}
