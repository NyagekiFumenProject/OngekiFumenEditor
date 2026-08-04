#nullable enable

namespace OngekiFumenEditor.Avalonia.Platforms.Services.FileSystem.Providers;

public interface ITemporaryEntry
{
    string Name { get; }

    /// <summary>
    /// Gets the provider-relative path using forward slashes. The provider root uses an empty path.
    /// </summary>
    string RelativePath { get; }

    /// <summary>
    /// Gets the native file-system path when the backing store exposes one.
    /// </summary>
    string? LocalPath { get; }
}
