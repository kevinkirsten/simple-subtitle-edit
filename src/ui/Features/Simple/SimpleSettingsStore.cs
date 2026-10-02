using Nikse.SubtitleEdit.Logic.Config;
using Nikse.SubtitleEdit.UiLogic.SimpleSync;
using System;
using System.IO;
using System.Text.Json;

namespace Nikse.SubtitleEdit.Features.Simple;

/// <summary>
/// OpenSubtitles login of the simple window, in its own file in the app's data folder (never in
/// the repository). On macOS/Linux the file is readable by the user only.
/// </summary>
public static class SimpleSettingsStore
{
    public static string FileName => Path.Combine(Se.DataFolder, "simple-subtitle-edit.json");

    public static string OnlineCacheFolder => Path.Combine(Path.GetTempPath(), "simple-subtitle-edit", "opensubtitles");

    public static OpenSubtitlesSettings Load()
    {
        try
        {
            return File.Exists(FileName)
                ? JsonSerializer.Deserialize<OpenSubtitlesSettings>(File.ReadAllText(FileName)) ?? new OpenSubtitlesSettings()
                : new OpenSubtitlesSettings();
        }
        catch (Exception)
        {
            return new OpenSubtitlesSettings();
        }
    }

    public static void Save(OpenSubtitlesSettings settings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FileName)!);
        File.WriteAllText(FileName, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(FileName, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
    }
}
