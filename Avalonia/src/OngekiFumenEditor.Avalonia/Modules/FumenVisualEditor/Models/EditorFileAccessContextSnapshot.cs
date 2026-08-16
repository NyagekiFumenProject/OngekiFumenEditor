#nullable enable

using System.Text.Json;
using System.Text.Json.Serialization;
using Avalonia.Platform.Storage;
using OngekiFumenEditor.Avalonia.Utils.SimpleFileSystem;
using OngekiFumenEditor.Avalonia.Utils.SimpleFileSystem.Impl.AvaloniaStorageProvider;

namespace OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Models;

public sealed class EditorFileAccessContextSnapshot
{
    public string ProjectDirectoryBookmark { get; set; } = string.Empty;

    public List<string> AdditionDirectoryBookmarks { get; set; } = [];

    public string? ProjectFileBookmark { get; set; }

    public string FumenFileBookmark { get; set; } = string.Empty;

    public string AudioFileBookmark { get; set; } = string.Empty;

    public byte[] Serialize() => JsonSerializer.SerializeToUtf8Bytes(
        this,
        EditorFileAccessContextSnapshotJsonContext.Default.EditorFileAccessContextSnapshot);

    public static bool TryDeserialize(byte[]? data, out EditorFileAccessContextSnapshot? snapshot)
    {
        snapshot = null;
        if (data is not { Length: > 0 })
            return false;

        try
        {
            snapshot = JsonSerializer.Deserialize(
                data,
                EditorFileAccessContextSnapshotJsonContext.Default.EditorFileAccessContextSnapshot);
            return snapshot?.HasRequiredBookmarks() == true;
        }
        catch (JsonException)
        {
            return false;
        }
        catch (NotSupportedException)
        {
            return false;
        }
    }

    public async Task<EditorFileAccessContext> ToContextAsync(IStorageProvider storageProvider)
    {
        ArgumentNullException.ThrowIfNull(storageProvider);
        if (!HasRequiredBookmarks())
            throw new InvalidDataException("The editor file access snapshot is missing a required bookmark.");

        ISimpleDirectory? projectDirectory = null;
        var additionDirectories = new List<ISimpleDirectory>(AdditionDirectoryBookmarks.Count);
        ISimpleFile? projectFile = null;
        ISimpleFile? fumenFile = null;
        ISimpleFile? audioFile = null;
        try
        {
            projectDirectory = await OpenDirectoryAsync(
                storageProvider,
                ProjectDirectoryBookmark,
                nameof(ProjectDirectoryBookmark)).ConfigureAwait(false);
            foreach (var bookmark in AdditionDirectoryBookmarks)
            {
                additionDirectories.Add(await OpenDirectoryAsync(
                    storageProvider,
                    bookmark,
                    nameof(AdditionDirectoryBookmarks)).ConfigureAwait(false));
            }

            if (!string.IsNullOrWhiteSpace(ProjectFileBookmark))
            {
                projectFile = await OpenFileAsync(
                    storageProvider,
                    ProjectFileBookmark,
                    nameof(ProjectFileBookmark)).ConfigureAwait(false);
            }

            fumenFile = await OpenFileAsync(
                storageProvider,
                FumenFileBookmark,
                nameof(FumenFileBookmark)).ConfigureAwait(false);
            audioFile = await OpenFileAsync(
                storageProvider,
                AudioFileBookmark,
                nameof(AudioFileBookmark)).ConfigureAwait(false);

            var context = new EditorFileAccessContext
            {
                ProjectDirectory = projectDirectory,
                AdditionDirectories = additionDirectories,
                ProjectFile = projectFile,
                FumenFile = fumenFile,
                AudioFile = audioFile
            };
            projectDirectory = null;
            additionDirectories = [];
            projectFile = null;
            fumenFile = null;
            audioFile = null;
            return context;
        }
        catch
        {
            audioFile?.Dispose();
            fumenFile?.Dispose();
            projectFile?.Dispose();
            for (var i = additionDirectories.Count - 1; i >= 0; i--)
                additionDirectories[i].Dispose();
            projectDirectory?.Dispose();
            throw;
        }
    }

    internal static async Task<EditorFileAccessContextSnapshot> FromContextAsync(
        EditorFileAccessContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.ThrowIfDisposed();
        var projectDirectory = context.ProjectDirectory
            ?? throw new InvalidOperationException("The editor context does not have a project directory.");
        var fumenFile = context.FumenFile
            ?? throw new InvalidOperationException("The editor context does not have a fumen file.");
        var audioFile = context.AudioFile
            ?? throw new InvalidOperationException("The editor context does not have an audio file.");

        var additionBookmarks = new List<string>(context.AdditionDirectories.Count);
        foreach (var directory in context.AdditionDirectories)
            additionBookmarks.Add(await SaveRequiredBookmarkAsync(directory, "additional directory").ConfigureAwait(false));

        return new EditorFileAccessContextSnapshot
        {
            ProjectDirectoryBookmark = await SaveRequiredBookmarkAsync(
                projectDirectory,
                "project directory").ConfigureAwait(false),
            AdditionDirectoryBookmarks = additionBookmarks,
            ProjectFileBookmark = context.ProjectFile is null
                ? null
                : await SaveRequiredBookmarkAsync(context.ProjectFile, "project file").ConfigureAwait(false),
            FumenFileBookmark = await SaveRequiredBookmarkAsync(
                fumenFile,
                "fumen file").ConfigureAwait(false),
            AudioFileBookmark = await SaveRequiredBookmarkAsync(
                audioFile,
                "audio file").ConfigureAwait(false)
        };
    }

    private bool HasRequiredBookmarks() =>
        !string.IsNullOrWhiteSpace(ProjectDirectoryBookmark) &&
        AdditionDirectoryBookmarks is not null &&
        AdditionDirectoryBookmarks.All(bookmark => !string.IsNullOrWhiteSpace(bookmark)) &&
        !string.IsNullOrWhiteSpace(FumenFileBookmark) &&
        !string.IsNullOrWhiteSpace(AudioFileBookmark);

    private static async Task<string> SaveRequiredBookmarkAsync(object item, string role)
    {
        if (item is not IBookmarkableSimpleFileSystemItem bookmarkable || !bookmarkable.CanBookmark)
            throw new InvalidOperationException($"The {role} does not support bookmarks.");

        var bookmark = await bookmarkable.SaveBookmarkAsync().ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(bookmark))
            throw new InvalidOperationException($"The {role} did not produce a bookmark.");

        return bookmark;
    }

    private static async Task<ISimpleDirectory> OpenDirectoryAsync(
        IStorageProvider storageProvider,
        string bookmark,
        string propertyName)
    {
        var folder = await storageProvider.OpenFolderBookmarkAsync(bookmark).ConfigureAwait(false)
            ?? throw new IOException($"The directory bookmark in {propertyName} is no longer available.");
        return AvaloniaStorageProviderFileSystemBuilder.LoadRootFromAvaloniaStorageFolder(folder);
    }

    private static async Task<ISimpleFile> OpenFileAsync(
        IStorageProvider storageProvider,
        string bookmark,
        string propertyName)
    {
        var file = await storageProvider.OpenFileBookmarkAsync(bookmark).ConfigureAwait(false)
            ?? throw new IOException($"The file bookmark in {propertyName} is no longer available.");
        return await AvaloniaStorageProviderFileSystemBuilder
            .LoadFromAvaloniaStorageFile(file)
            .ConfigureAwait(false);
    }
}

[JsonSerializable(typeof(EditorFileAccessContextSnapshot))]
internal partial class EditorFileAccessContextSnapshotJsonContext : JsonSerializerContext;
