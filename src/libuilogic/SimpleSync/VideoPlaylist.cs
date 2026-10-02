using Nikse.SubtitleEdit.Core.Common;
using System.Text.RegularExpressions;

namespace Nikse.SubtitleEdit.UiLogic.SimpleSync;

/// <summary>
/// The videos the PREV/NEXT buttons walk through: every video in a folder (and its season
/// subfolders), in episode order.
/// </summary>
public sealed class VideoPlaylist
{
    private VideoPlaylist(string rootFolder, List<string> files, int index)
    {
        RootFolder = rootFolder;
        Files = files;
        Index = index;
    }

    public string RootFolder { get; }

    public IReadOnlyList<string> Files { get; }

    public int Index { get; private set; }

    public string? Current => Index >= 0 && Index < Files.Count ? Files[Index] : null;

    public bool HasPrevious => Index > 0;

    public bool HasNext => Index < Files.Count - 1;

    /// <summary>All videos under <paramref name="folder"/>, recursively; starts at the first one.</summary>
    public static VideoPlaylist FromFolder(string folder)
    {
        var files = FindVideos(folder, SearchOption.AllDirectories);
        return new VideoPlaylist(folder, files, files.Count > 0 ? 0 : -1);
    }

    /// <summary>The videos next to <paramref name="videoFileName"/>, positioned on it.</summary>
    public static VideoPlaylist FromVideo(string videoFileName)
    {
        var folder = Path.GetDirectoryName(Path.GetFullPath(videoFileName))!;
        var files = FindVideos(folder, SearchOption.TopDirectoryOnly);
        var index = files.FindIndex(f => string.Equals(f, Path.GetFullPath(videoFileName), StringComparison.Ordinal));
        if (index < 0)
        {
            files.Insert(0, Path.GetFullPath(videoFileName));
            index = 0;
        }

        return new VideoPlaylist(folder, files, index);
    }

    public string? MoveNext()
    {
        if (HasNext)
        {
            Index++;
        }

        return Current;
    }

    public string? MovePrevious()
    {
        if (HasPrevious)
        {
            Index--;
        }

        return Current;
    }

    /// <summary>"3/86 · Season 01/Show - S01E03.mkv"</summary>
    public string Describe()
    {
        if (Current == null)
        {
            return string.Empty;
        }

        var relative = Path.GetRelativePath(RootFolder, Current);
        return $"{Index + 1}/{Files.Count} · {relative}";
    }

    private static List<string> FindVideos(string folder, SearchOption option)
    {
        if (!Directory.Exists(folder))
        {
            return [];
        }

        var extensions = new HashSet<string>(Utilities.VideoFileExtensions, StringComparer.OrdinalIgnoreCase);
        var files = new List<string>();
        try
        {
            foreach (var file in Directory.EnumerateFiles(folder, "*", new EnumerationOptions
                     {
                         RecurseSubdirectories = option == SearchOption.AllDirectories,
                         IgnoreInaccessible = true,
                         AttributesToSkip = FileAttributes.Hidden | FileAttributes.System,
                     }))
            {
                var name = Path.GetFileName(file);
                if (name.StartsWith("._", StringComparison.Ordinal) || !extensions.Contains(Path.GetExtension(file)))
                {
                    continue;
                }

                files.Add(Path.GetFullPath(file));
            }
        }
        catch (IOException)
        {
            // An unplugged drive mid-scan: keep what was found.
        }

        files.Sort((a, b) => NaturalCompare(Path.GetRelativePath(folder, a), Path.GetRelativePath(folder, b)));
        return files;
    }

    private static readonly Regex Chunks = new(@"\d+|\D+", RegexOptions.Compiled);

    /// <summary>Compares "E2" before "E10" (numbers by value, text ignoring case).</summary>
    public static int NaturalCompare(string a, string b)
    {
        var x = Chunks.Matches(a);
        var y = Chunks.Matches(b);
        for (var i = 0; i < Math.Min(x.Count, y.Count); i++)
        {
            var cx = x[i].Value;
            var cy = y[i].Value;
            int result;
            if (char.IsDigit(cx[0]) && char.IsDigit(cy[0]))
            {
                var nx = cx.TrimStart('0');
                var ny = cy.TrimStart('0');
                result = nx.Length != ny.Length ? nx.Length.CompareTo(ny.Length) : string.CompareOrdinal(nx, ny);
            }
            else
            {
                result = string.Compare(cx, cy, StringComparison.OrdinalIgnoreCase);
            }

            if (result != 0)
            {
                return result;
            }
        }

        return x.Count.CompareTo(y.Count);
    }
}
