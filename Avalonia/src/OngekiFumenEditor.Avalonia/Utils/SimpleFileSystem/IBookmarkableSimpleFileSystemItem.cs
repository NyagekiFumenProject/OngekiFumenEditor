#nullable enable

namespace OngekiFumenEditor.Avalonia.Utils.SimpleFileSystem;

internal interface IBookmarkableSimpleFileSystemItem
{
    bool CanBookmark { get; }

    Task<string?> SaveBookmarkAsync();
}
