#nullable enable

using Avalonia.Platform.Storage;
using OngekiFumenEditor.Avalonia.Utils;
using OngekiFumenEditor.Avalonia.Utils.SimpleFileSystem;
using OngekiFumenEditor.Avalonia.Utils.SimpleFileSystem.Impl.AvaloniaStorageProvider;

namespace OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Setup;

/// <summary>
/// Avalonia StorageProvider adapter used by the platform-specific Provider composition
/// roots. The Setup ViewModel only sees capability objects and can be tested without it.
/// </summary>
public sealed class AvaloniaEditorProjectSetupFilePicker : IEditorProjectSetupFilePicker
{
    public async Task<EditorProjectDirectorySelection?> PickProjectDirectoryAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var storageFolder = await FileDialogHelper.OpenStorageFolderAsync("Select project folder");
        if (storageFolder is null)
            return null;

        var ownershipTransferred = false;
        try
        {
            var displayName = storageFolder.Name;
            cancellationToken.ThrowIfCancellationRequested();
            // The builder takes ownership immediately and disposes the folder if indexing fails.
            ownershipTransferred = true;
            var directory = await AvaloniaStorageProviderFileSystemBuilder
                .LoadFromAvaloniaStorageFolder(storageFolder, cancellationToken);
            return new EditorProjectDirectorySelection(directory, displayName);
        }
        catch
        {
            if (!ownershipTransferred)
                storageFolder.Dispose();
            throw;
        }
    }

    public async Task<ISimpleFile?> PickAudioAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var extensions = FileDialogHelper.GetSupportAudioFileExtensionFilterList();
        var file = await FileDialogHelper.OpenFileAsync("Select audio", extensions);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            return file;
        }
        catch
        {
            file?.Dispose();
            throw;
        }
    }

    public async Task<ISimpleFile?> PickExistingFumenAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var file = await FileDialogHelper.OpenFileAsync(
            "Select existing fumen",
            FileDialogHelper.GetSupportFumenOpenFileExtensionFilterList());
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            return file;
        }
        catch
        {
            file?.Dispose();
            throw;
        }
    }

    public async Task<ISimpleFile?> PickExternalAwbAsync(
        string expectedFileName,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedFileName);
        cancellationToken.ThrowIfCancellationRequested();
        var file = await FileDialogHelper.OpenFileAsync(
            $"Select external AWB ({expectedFileName})",
            [(".awb", "AWB audio archive")]);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            return file;
        }
        catch
        {
            file?.Dispose();
            throw;
        }
    }
}
