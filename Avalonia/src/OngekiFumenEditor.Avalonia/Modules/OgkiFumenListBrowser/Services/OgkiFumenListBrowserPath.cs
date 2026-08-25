#nullable enable

using OngekiFumenEditor.Avalonia.Utils.SimpleFileSystem;

namespace OngekiFumenEditor.Avalonia.Modules.OgkiFumenListBrowser.Services;

/// <summary>
/// Resolves resource locators without ever turning them into host file-system paths.
/// </summary>
public static class OgkiFumenListBrowserPath
{
    private static readonly char[] Separators = ['/','\\'];

    public static bool TryNormalizeRelative(string? value, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var trimmed = value.Trim();
        if (trimmed[0] is '/' or '\\' ||
            trimmed.IndexOf(':') >= 0 ||
            (Uri.TryCreate(trimmed, UriKind.Absolute, out var uri) &&
             !string.IsNullOrEmpty(uri.Scheme)))
        {
            return false;
        }

        var parts = trimmed.Split(Separators, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
            return false;

        var result = new List<string>(parts.Length);
        foreach (var part in parts)
        {
            if (part is ".")
                continue;
            if (part is "..")
            {
                if (result.Count == 0)
                    return false;
                result.RemoveAt(result.Count - 1);
                continue;
            }

            if (part.IndexOf('\0') >= 0)
                return false;
            result.Add(part);
        }

        normalized = string.Join('/', result);
        return result.Count > 0;
    }

    public static bool TryCombineRelative(
        string? parentLocator,
        string? childLocator,
        out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(childLocator))
            return false;

        var child = childLocator.Trim();
        if (child[0] is '/' or '\\' ||
            child.IndexOf(':') >= 0 ||
            (Uri.TryCreate(child, UriKind.Absolute, out var uri) && !string.IsNullOrEmpty(uri.Scheme)))
        {
            return false;
        }

        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(parentLocator))
        {
            if (!TryNormalizeRelative(parentLocator, out var parent))
                return false;
            parts.AddRange(parent.Split('/'));
        }

        foreach (var part in child.Split(Separators, StringSplitOptions.RemoveEmptyEntries))
        {
            if (part is ".")
                continue;
            if (part is "..")
            {
                if (parts.Count == 0)
                    return false;
                parts.RemoveAt(parts.Count - 1);
                continue;
            }
            if (part.IndexOf('\0') >= 0)
                return false;
            parts.Add(part);
        }

        normalized = string.Join('/', parts);
        return parts.Count > 0;
    }

    public static ISimpleDirectory? ResolveDirectory(
        ISimpleDirectory root,
        string relativeLocator)
    {
        ArgumentNullException.ThrowIfNull(root);
        if (!TryNormalizeRelative(relativeLocator, out var normalized))
            return null;

        var current = root;
        foreach (var part in normalized.Split('/'))
        {
            current = current.ChildDictionaries.FirstOrDefault(x =>
                x.DirectoryName.Equals(part, StringComparison.OrdinalIgnoreCase));
            if (current is null)
                return null;
        }

        return current;
    }

    public static ISimpleFile? ResolveFile(
        ISimpleDirectory root,
        string? relativeLocator)
    {
        ArgumentNullException.ThrowIfNull(root);
        if (!TryNormalizeRelative(relativeLocator, out var normalized))
            return null;

        var parts = normalized.Split('/');
        var directory = root;
        for (var index = 0; index < parts.Length - 1; index++)
        {
            directory = directory.ChildDictionaries.FirstOrDefault(x =>
                x.DirectoryName.Equals(parts[index], StringComparison.OrdinalIgnoreCase));
            if (directory is null)
                return null;
        }

        return directory.ChildFiles.FirstOrDefault(x =>
            x.FileName.Equals(parts[^1], StringComparison.OrdinalIgnoreCase));
    }

    public static string Combine(string? parent, string? child)
    {
        return TryCombineRelative(parent, child, out var combined) ? combined : string.Empty;
    }
}
