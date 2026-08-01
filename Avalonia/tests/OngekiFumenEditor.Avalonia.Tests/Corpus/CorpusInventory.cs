using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OngekiFumenEditor.Avalonia.Tests.Corpus;

internal sealed record CorpusInventory(
    IReadOnlyList<string> Charts,
    IReadOnlyList<string> Projects,
    IReadOnlyList<string> Scripts,
    IReadOnlyList<string> AudioFiles,
    IReadOnlyList<string> Images,
    IReadOnlyList<string> OtherFiles)
{
    public static CorpusInventory Discover(string rootPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);

        var charts = new List<string>();
        var projects = new List<string>();
        var scripts = new List<string>();
        var audioFiles = new List<string>();
        var images = new List<string>();
        var otherFiles = new List<string>();

        foreach (var filePath in Directory.EnumerateFiles(rootPath, "*", SearchOption.AllDirectories))
        {
            var relativeSegments = Path.GetRelativePath(rootPath, filePath)
                .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (relativeSegments.Any(static segment => segment is ".git" or ".svn" or ".hg"))
                continue;

            switch (Path.GetExtension(filePath).ToLowerInvariant())
            {
                case ".nyageki":
                    charts.Add(filePath);
                    break;
                case ".nyagekiproj":
                    projects.Add(filePath);
                    break;
                case ".nyagekiscript":
                    scripts.Add(filePath);
                    break;
                case ".wav":
                    audioFiles.Add(filePath);
                    break;
                case ".png":
                    images.Add(filePath);
                    break;
                default:
                    otherFiles.Add(filePath);
                    break;
            }
        }

        static string[] Sort(IEnumerable<string> paths) =>
            paths.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray();

        return new CorpusInventory(
            Sort(charts),
            Sort(projects),
            Sort(scripts),
            Sort(audioFiles),
            Sort(images),
            Sort(otherFiles));
    }
}

internal static class NyagekiCommandInventory
{
    public static IReadOnlyDictionary<string, int> ReadCommandCounts(string chartPath)
    {
        var commands = new SortedDictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var line in File.ReadLines(chartPath))
        {
            var separatorIndex = line.IndexOf(':');
            if (separatorIndex <= 0)
                continue;

            var commandName = line[..separatorIndex].Trim();
            if (commandName.Length == 0)
                continue;

            commands.TryGetValue(commandName, out var count);
            commands[commandName] = count + 1;
        }

        return commands;
    }
}
