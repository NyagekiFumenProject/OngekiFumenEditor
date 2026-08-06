#nullable enable

using OngekiFumenEditor.Avalonia.Utils.SimpleFileSystem;

namespace OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Base;

public static class EditorProjectPathResolver
{
    private static readonly char[] Separators = ['/', '\\'];

    public static IReadOnlyList<(string Locator, ISimpleFile File)> FindProjectFiles(
        ISimpleDirectory root,
        string extension)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentException.ThrowIfNullOrWhiteSpace(extension);

        var result = new List<(string Locator, ISimpleFile File)>();
        var pending = new Stack<ISimpleDirectory>();
        pending.Push(root);
        while (pending.Count > 0)
        {
            var directory = pending.Pop();
            foreach (var child in directory.ChildDictionaries)
                pending.Push(child);

            foreach (var file in directory.ChildFiles)
            {
                if (file.FileName.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
                    result.Add((GetRootRelativeLocator(file), file));
            }
        }

        return result
            .OrderBy(x => x.Locator, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.Locator, StringComparer.Ordinal)
            .ToArray();
    }

    public static bool TryNormalizeRootRelativeLocator(
        string? locator,
        out string normalized,
        out string error)
    {
        normalized = string.Empty;
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(locator))
        {
            error = "The file locator is empty.";
            return false;
        }

        if (IsAbsoluteLocator(locator))
        {
            error = "The file locator must be relative to the project folder.";
            return false;
        }

        return TryNormalizeSegments([], locator, out normalized, out error);
    }

    public static bool TryResolveDependency(
        ISimpleDirectory root,
        string projectFileLocator,
        string dependencyLocator,
        out ISimpleFile? file,
        out string rootRelativeLocator,
        out string projectRelativeLocator,
        out string error)
    {
        ArgumentNullException.ThrowIfNull(root);
        file = null;
        rootRelativeLocator = string.Empty;
        projectRelativeLocator = string.Empty;
        error = string.Empty;

        if (!TryNormalizeRootRelativeLocator(projectFileLocator, out var normalizedProject, out error))
            return false;

        var projectDirectoryParts = Split(normalizedProject).SkipLast(1).ToArray();
        if (string.IsNullOrWhiteSpace(dependencyLocator))
        {
            error = "The dependency locator is empty.";
            return false;
        }

        if (IsAbsoluteLocator(dependencyLocator))
        {
            if (!TryConvertAbsoluteLocator(root, dependencyLocator, out rootRelativeLocator, out error))
                return false;
        }
        else if (!TryNormalizeSegments(projectDirectoryParts, dependencyLocator, out rootRelativeLocator, out error))
        {
            return false;
        }

        if (!TryFindFile(root, rootRelativeLocator, out file, out var actualLocator, out error))
            return false;

        rootRelativeLocator = actualLocator;
        projectRelativeLocator = GetRelativeLocator(projectDirectoryParts, Split(actualLocator));
        return true;
    }

    public static bool TryResolveRootResource(
        ISimpleDirectory root,
        string resourceLocator,
        out ISimpleFile? file,
        out string rootRelativeLocator,
        out string error)
    {
        ArgumentNullException.ThrowIfNull(root);
        file = null;
        rootRelativeLocator = string.Empty;
        error = string.Empty;

        if (IsAbsoluteLocator(resourceLocator))
        {
            if (!TryConvertAbsoluteLocator(root, resourceLocator, out rootRelativeLocator, out error))
                return false;
        }
        else if (!TryNormalizeRootRelativeLocator(resourceLocator, out rootRelativeLocator, out error))
        {
            return false;
        }

        if (!TryFindFile(root, rootRelativeLocator, out file, out var actualLocator, out error))
            return false;

        rootRelativeLocator = actualLocator;
        return true;
    }

    public static bool TryFindFile(
        ISimpleDirectory root,
        string locator,
        out ISimpleFile? file,
        out string actualLocator,
        out string error)
    {
        ArgumentNullException.ThrowIfNull(root);
        file = null;
        actualLocator = string.Empty;
        if (!TryNormalizeRootRelativeLocator(locator, out var normalized, out error))
            return false;

        var parts = Split(normalized);
        var actualParts = new List<string>(parts.Length);
        ISimpleDirectory current = root;
        for (var i = 0; i < parts.Length - 1; i++)
        {
            var matches = current.ChildDictionaries
                .Where(x => x.DirectoryName.Equals(parts[i], StringComparison.OrdinalIgnoreCase))
                .ToArray();
            if (matches.Length == 0)
            {
                error = $"Directory '{string.Join('/', parts.Take(i + 1))}' does not exist in the project folder.";
                return false;
            }

            if (matches.Length > 1)
            {
                error = $"Directory locator '{string.Join('/', parts.Take(i + 1))}' has a case-insensitive name conflict.";
                return false;
            }

            current = matches[0];
            actualParts.Add(current.DirectoryName);
        }

        var fileMatches = current.ChildFiles
            .Where(x => x.FileName.Equals(parts[^1], StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (fileMatches.Length == 0)
        {
            error = $"File '{normalized}' does not exist in the project folder.";
            return false;
        }

        if (fileMatches.Length > 1)
        {
            error = $"File locator '{normalized}' has a case-insensitive name conflict.";
            return false;
        }

        file = fileMatches[0];
        actualParts.Add(file.FileName);
        actualLocator = string.Join('/', actualParts);
        error = string.Empty;
        return true;
    }

    public static string GetRootRelativeLocator(ISimpleFile file)
    {
        ArgumentNullException.ThrowIfNull(file);
        var parts = new Stack<string>();
        parts.Push(file.FileName);
        for (var directory = file.ParentDictionary;
             directory?.ParentDictionary is not null;
             directory = directory.ParentDictionary)
        {
            parts.Push(directory.DirectoryName);
        }

        return string.Join('/', parts);
    }

    private static bool TryConvertAbsoluteLocator(
        ISimpleDirectory root,
        string locator,
        out string normalized,
        out string error)
    {
        normalized = string.Empty;
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(root.LocalPath))
        {
            error = "An absolute dependency locator cannot be migrated on this platform.";
            return false;
        }

        try
        {
            var rootPath = Path.GetFullPath(root.LocalPath);
            var targetPath = Path.GetFullPath(locator);
            var relative = Path.GetRelativePath(rootPath, targetPath);
            if (!TryNormalizeRootRelativeLocator(relative, out normalized, out error))
                return false;
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            error = $"The absolute dependency locator is invalid: {exception.Message}";
            return false;
        }
    }

    private static bool TryNormalizeSegments(
        IEnumerable<string> initialParts,
        string locator,
        out string normalized,
        out string error)
    {
        var parts = new List<string>(initialParts);
        foreach (var part in Split(locator))
        {
            if (part == ".")
                continue;

            if (part == "..")
            {
                if (parts.Count == 0)
                {
                    normalized = string.Empty;
                    error = "The file locator escapes the selected project folder.";
                    return false;
                }

                parts.RemoveAt(parts.Count - 1);
                continue;
            }

            if (part.Contains(':'))
            {
                normalized = string.Empty;
                error = $"The file locator contains an invalid path segment: '{part}'.";
                return false;
            }

            parts.Add(part);
        }

        if (parts.Count == 0)
        {
            normalized = string.Empty;
            error = "The file locator does not identify a file.";
            return false;
        }

        normalized = string.Join('/', parts);
        error = string.Empty;
        return true;
    }

    private static string GetRelativeLocator(
        IReadOnlyList<string> fromDirectory,
        IReadOnlyList<string> target)
    {
        var common = 0;
        while (common < fromDirectory.Count &&
               common < target.Count &&
               fromDirectory[common].Equals(target[common], StringComparison.OrdinalIgnoreCase))
        {
            common++;
        }

        var result = new List<string>();
        result.AddRange(Enumerable.Repeat("..", fromDirectory.Count - common));
        result.AddRange(target.Skip(common));
        return string.Join('/', result);
    }

    private static bool IsAbsoluteLocator(string locator)
    {
        if (locator.StartsWith('/') || locator.StartsWith('\\') || Path.IsPathFullyQualified(locator))
            return true;

        return Uri.TryCreate(locator, UriKind.Absolute, out var uri) && !string.IsNullOrEmpty(uri.Scheme);
    }

    private static string[] Split(string locator) =>
        locator.Split(Separators, StringSplitOptions.RemoveEmptyEntries);
}
