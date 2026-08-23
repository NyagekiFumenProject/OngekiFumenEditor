#nullable enable

using OngekiFumenEditor.Avalonia.Utils.SimpleFileSystem;

namespace OngekiFumenEditor.Avalonia.Platforms.Services.FileSystem.Providers;

public static class SimpleFileSystemEntryExtensions
{
    public static string GetRequiredLocalPath(this ISimpleFile entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        return entry.LocalPath ?? throw new PlatformNotSupportedException(
            $"File '{entry.FullPath}' is not backed by a local file-system path on this platform.");
    }

    public static string GetRequiredLocalPath(this ISimpleDirectory entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        return entry.LocalPath ?? throw new PlatformNotSupportedException(
            $"Directory '{entry.FullPath}' is not backed by a local file-system path on this platform.");
    }
}
