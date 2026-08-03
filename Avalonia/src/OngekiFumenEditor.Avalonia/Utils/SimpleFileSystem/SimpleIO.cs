#nullable enable

using System.Text;
using System.Text.RegularExpressions;

namespace OngekiFumenEditor.Avalonia.Utils.SimpleFileSystem;

public static class SimpleIO
{
    private static readonly char[] PathSeparators = ['/', '\\'];

    public static bool ExistDirectory(ISimpleDirectory root, string? path)
    {
        ArgumentNullException.ThrowIfNull(root);
        return FindDirectory(root, path) is not null;
    }

    public static bool ExistFile(ISimpleDirectory root, string? path)
    {
        ArgumentNullException.ThrowIfNull(root);
        return FindFile(root, path) is not null;
    }

    public static Task<Stream> OpenRead(ISimpleDirectory root, string path)
    {
        var file = FindFile(root, path);
        return file is null
            ? throw new FileNotFoundException($"File not found: {path}", path)
            : file.OpenRead();
    }

    public static ValueTask<string[]> ReadAllLines(ISimpleDirectory root, string path)
    {
        var file = FindFile(root, path);
        return file is null
            ? throw new FileNotFoundException($"File not found: {path}", path)
            : file.ReadAllLines();
    }

    public static ISimpleFile[] GetFiles(
        ISimpleDirectory root,
        string? path,
        string searchPattern = "*")
    {
        ArgumentNullException.ThrowIfNull(searchPattern);

        var directory = FindDirectory(root, path);
        if (directory is null)
            return [];

        var regex = WildcardToRegex(searchPattern);
        return [.. directory.ChildFiles.Where(file => regex.IsMatch(file.FileName))];
    }

    public static string[] GetFilePaths(
        ISimpleDirectory root,
        string? path,
        string searchPattern = "*")
    {
        return [.. GetFiles(root, path, searchPattern).Select(file => file.FullPath)];
    }

    public static ISimpleFile? FindFile(ISimpleDirectory root, string? path)
    {
        ArgumentNullException.ThrowIfNull(root);
        if (string.IsNullOrWhiteSpace(path))
            return null;

        var parts = SplitPath(path);
        if (parts.Length == 0)
            return null;

        var directoryPath = string.Join('/', parts.Take(parts.Length - 1));
        var directory = FindDirectory(root, directoryPath);
        if (directory is null)
            return null;

        var fileName = parts[^1];
        return directory.ChildFiles.FirstOrDefault(file =>
            file.FileName.Equals(fileName, StringComparison.OrdinalIgnoreCase));
    }

    public static ISimpleDirectory? FindDirectory(ISimpleDirectory root, string? path)
    {
        ArgumentNullException.ThrowIfNull(root);
        if (string.IsNullOrWhiteSpace(path))
            return root;

        ISimpleDirectory? current = root;
        foreach (var part in SplitPath(path))
        {
            if (current is null)
                return null;

            if (part == ".")
                continue;

            if (part == "..")
            {
                current = current.ParentDictionary;
                continue;
            }

            current = current.ChildDictionaries.FirstOrDefault(directory =>
                directory.DirectoryName.Equals(part, StringComparison.OrdinalIgnoreCase));
        }

        return current;
    }

    internal static Regex WildcardToRegex(string pattern)
    {
        ArgumentNullException.ThrowIfNull(pattern);

        var expression = new StringBuilder("^");
        foreach (var character in pattern)
        {
            expression.Append(character switch
            {
                '*' => ".*",
                '?' => ".",
                _ => Regex.Escape(character.ToString())
            });
        }

        expression.Append('$');
        return new Regex(
            expression.ToString(),
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    private static string[] SplitPath(string path)
    {
        return path.Split(PathSeparators, StringSplitOptions.RemoveEmptyEntries);
    }
}
