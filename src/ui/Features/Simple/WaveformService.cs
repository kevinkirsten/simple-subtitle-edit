using Nikse.SubtitleEdit.Logic.Config;
using Nikse.SubtitleEdit.Logic.Media;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Nikse.SubtitleEdit.Features.Simple;

/// <summary>
/// Turns the audio of any video ffmpeg can read into waveform peaks. Peaks are cached in the
/// same folder the full editor uses, so opening the same episode again is instant.
/// </summary>
public static class WaveformService
{
    public static async Task<WavePeakData2?> LoadAsync(string videoFileName, CancellationToken token)
    {
        var peakFile = WavePeakGenerator2.GetPeakWaveFileName(videoFileName);
        if (File.Exists(peakFile))
        {
            try
            {
                return WavePeakData2.FromDisk(peakFile);
            }
            catch
            {
                File.Delete(peakFile); // corrupt cache: regenerate below
            }
        }

        var ffmpeg = FindFfmpeg();
        if (ffmpeg == null)
        {
            return null;
        }

        var wavFile = Path.Combine(Path.GetTempPath(), "sse-" + Guid.NewGuid() + ".wav");
        try
        {
            // Mono 16 kHz is plenty for a waveform and keeps a 1 h episode around 115 MB of temp wav.
            var psi = new ProcessStartInfo(ffmpeg)
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
            };
            foreach (var arg in new[] { "-nostdin", "-y", "-i", videoFileName, "-vn", "-ac", "1", "-ar", "16000", "-af", "aresample=async=1:first_pts=0", "-f", "wav", wavFile })
            {
                psi.ArgumentList.Add(arg);
            }

            using var process = Process.Start(psi);
            if (process == null)
            {
                return null;
            }

            // Drain stderr so ffmpeg never blocks on a full pipe.
            var drain = process.StandardError.ReadToEndAsync(token);
            try
            {
                await process.WaitForExitAsync(token);
            }
            catch (OperationCanceledException)
            {
                try { process.Kill(entireProcessTree: true); } catch { /* already gone */ }
                throw;
            }

            await drain;
            if (process.ExitCode != 0 || !File.Exists(wavFile))
            {
                return null;
            }

            return await Task.Run(() =>
            {
                using var generator = new WavePeakGenerator2(wavFile);
                return generator.IsSupported ? generator.GeneratePeaks(0, peakFile) : null;
            }, token);
        }
        finally
        {
            try { File.Delete(wavFile); } catch { /* temp file, ignore */ }
        }
    }

    /// <summary>
    /// The configured ffmpeg, then the usual install locations. Apps started from the Finder do
    /// not get the shell PATH, so Homebrew's folders are checked explicitly.
    /// </summary>
    public static string? FindFfmpeg()
    {
        var exe = OperatingSystem.IsWindows() ? "ffmpeg.exe" : "ffmpeg";
        var candidates = new[]
        {
            Se.Settings.General.FfmpegPath,
            Path.Combine(AppContext.BaseDirectory, exe),
            "/opt/homebrew/bin/ffmpeg",
            "/usr/local/bin/ffmpeg",
            "/usr/bin/ffmpeg",
        };

        foreach (var candidate in candidates)
        {
            if (!string.IsNullOrWhiteSpace(candidate) && File.Exists(candidate))
            {
                return candidate;
            }
        }

        var path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        foreach (var dir in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var full = Path.Combine(dir, exe);
            if (File.Exists(full))
            {
                return full;
            }
        }

        return null;
    }
}
