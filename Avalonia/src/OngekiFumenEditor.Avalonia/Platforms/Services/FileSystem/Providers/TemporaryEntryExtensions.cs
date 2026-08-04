#nullable enable

namespace OngekiFumenEditor.Avalonia.Platforms.Services.FileSystem.Providers;

public static class TemporaryEntryExtensions
{
    public static string GetRequiredLocalPath(this ITemporaryEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        return entry.LocalPath ?? throw new PlatformNotSupportedException(
            $"Temporary entry '{entry.RelativePath}' is not backed by a local file-system path on this platform.");
    }
}
