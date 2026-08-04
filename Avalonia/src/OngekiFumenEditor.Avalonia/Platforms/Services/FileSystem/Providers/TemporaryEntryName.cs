#nullable enable

namespace OngekiFumenEditor.Avalonia.Platforms.Services.FileSystem.Providers;

internal static class TemporaryEntryName
{
    private const int MaxNameLength = 255;
    private static readonly char[] InvalidCharacters = ['<', '>', ':', '"', '/', '\\', '|', '?', '*'];
    private static readonly HashSet<string> ReservedWindowsNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
    };

    public static string Validate(string name, string parameterName = "name")
    {
        ArgumentNullException.ThrowIfNull(name, parameterName);

        if (name.Length == 0 || name.Length > MaxNameLength || string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("A temporary entry name must contain between 1 and 255 characters.", parameterName);

        if (!string.Equals(name, name.Trim(), StringComparison.Ordinal))
            throw new ArgumentException("A temporary entry name cannot start or end with whitespace.", parameterName);

        if (name is "." or ".." || Path.IsPathRooted(name))
            throw new ArgumentException("A temporary entry name must be a single relative path segment.", parameterName);

        if (name.EndsWith(".", StringComparison.Ordinal))
            throw new ArgumentException("A temporary entry name cannot end with a period.", parameterName);

        foreach (char character in name)
        {
            if (char.IsControl(character) || Array.IndexOf(InvalidCharacters, character) >= 0)
                throw new ArgumentException("A temporary entry name contains an invalid character.", parameterName);
        }

        int extensionSeparator = name.IndexOf('.');
        string stem = extensionSeparator < 0 ? name : name[..extensionSeparator];
        if (ReservedWindowsNames.Contains(stem))
            throw new ArgumentException("A temporary entry name uses a reserved device name.", parameterName);

        return name;
    }

    public static string NormalizeExtension(string extension)
    {
        ArgumentNullException.ThrowIfNull(extension);
        if (extension.Length == 0)
            return string.Empty;

        string normalized = extension[0] == '.' ? extension : $".{extension}";
        foreach (char character in normalized)
        {
            if (char.IsControl(character) || Array.IndexOf(InvalidCharacters, character) >= 0)
                throw new ArgumentException("A temporary file extension contains an invalid character.", nameof(extension));
        }

        return normalized;
    }

    public static string Combine(string parentRelativePath, string name)
    {
        Validate(name);
        return parentRelativePath.Length == 0 ? name : $"{parentRelativePath}/{name}";
    }
}
