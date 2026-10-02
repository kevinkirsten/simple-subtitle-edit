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

    public static string PlexFileName => Path.Combine(Se.DataFolder, "simple-subtitle-edit-plex.json");

    public static PlexSettings LoadPlex()
    {
        try
        {
            return File.Exists(PlexFileName)
                ? JsonSerializer.Deserialize<PlexSettings>(File.ReadAllText(PlexFileName)) ?? new PlexSettings()
                : new PlexSettings();
        }
        catch (Exception)
        {
            return new PlexSettings();
        }
    }

    public static void SavePlex(PlexSettings settings) => WritePrivate(PlexFileName, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));

    private static void WritePrivate(string fileName, string json)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(fileName)!);
        File.WriteAllText(fileName, json);
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(fileName, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
    }

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

    public static void Save(OpenSubtitlesSettings settings) =>
        WritePrivate(FileName, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
}
