#nullable enable

using Avalonia;
using Avalonia.Platform.Storage;
using DereTore.Exchange.Archive.ACB;
using Gekimini.Avalonia.Framework.Dialogs;
using Gekimini.Avalonia.Framework.RecentFiles;
using Gekimini.Avalonia.Platforms.Services.Window;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Base;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Models;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.ViewModels;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.ViewModels.Dialogs;
using OngekiFumenEditor.Avalonia.Utils;
using OngekiFumenEditor.Avalonia.Utils.SimpleFileSystem;
using OngekiFumenEditor.Avalonia.Utils.SimpleFileSystem.Impl.AvaloniaStorageProvider;

namespace OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor;

internal partial class FumenVisualEditorProvider
{
    private IDialogManager DialogManager => IoC.Get<IDialogManager>();

    private async Task<bool> OpenFromFolderAsync(FumenVisualEditorViewModel editor)
    {
        IStorageFolder? selectedFolder = await FileDialogHelper.OpenStorageFolderAsync("Open project folder");
        if (selectedFolder is null)
            return false;

        ISimpleDirectory? projectRoot = null;
        try
        {
            var folderDisplayName = selectedFolder.Name;

            using (await EditorProjectIoGate.EnterAsync())
            {
                var transferredFolder = selectedFolder;
                selectedFolder = null;
                projectRoot = await AvaloniaStorageProviderFileSystemBuilder
                    .LoadFromAvaloniaStorageFolder(transferredFolder);
            }

            var candidates = EditorProjectPathResolver.FindProjectFiles(
                projectRoot,
                FILE_EXTENSION_NAME);
            if (candidates.Count == 0)
            {
                await DialogManager.ShowMessageDialog(
                    "The selected project folder does not contain a .nyagekiProj file.",
                    DialogMessageType.Error);
                return false;
            }

            var selectedLocator = await SelectProjectLocatorAsync(candidates);
            if (selectedLocator is null)
                return false;

            var selectedProject = candidates.Single(x => x.Locator == selectedLocator);
            var selectedFiles = await SelectProjectFilesAsync(
                projectRoot,
                selectedLocator);
            if (selectedFiles is null)
                return false;

            EditorFileAccessContext? fileAccessContext = new EditorFileAccessContext
            {
                ProjectDirectory = projectRoot,
                ProjectFile = selectedProject.File,
                FumenFile = selectedFiles.Value.FumenFile,
                AudioFile = selectedFiles.Value.AudioFile
            };
            projectRoot = null;

            EditorContext projectContext;
            try
            {
                if (!await TryBindExternalAwbAsync(fileAccessContext))
                    return false;

                using (await EditorProjectIoGate.EnterAsync())
                {
                    var contextForLoad = fileAccessContext;
                    fileAccessContext = null;
                    projectContext = await EditorProjectDataUtils.TryLoadFromContextAsync(contextForLoad);
                    if (!await TryTransferContextToEditorAsync(
                            editor,
                            projectContext,
                            selectedProject.File.FileName))
                    {
                        return false;
                    }
                }
            }
            finally
            {
                fileAccessContext?.Dispose();
            }

            await TryStoreRecentFromContextAsync(
                projectContext,
                selectedProject.File.FileName,
                BuildLocationDescription(folderDisplayName, selectedLocator));
            return true;
        }
        catch (Exception exception)
        {
            Log.LogError($"Failed to open a project folder: {exception.Message}");
            await DialogManager.ShowMessageDialog(
                $"Unable to open the selected project: {exception.Message}",
                DialogMessageType.Error);
            return false;
        }
        finally
        {
            projectRoot?.Dispose();
            selectedFolder?.Dispose();
        }
    }

    private async Task<bool> OpenFromRecentAsync(
        FumenVisualEditorViewModel editor,
        RecentRecordInfo recordInfo)
    {
        if (!TryReadSnapshot(recordInfo, out var snapshot))
        {
            MarkPermanentlyInvalid(recordInfo);
            await ShowInvalidRecentProjectAsync();
            return false;
        }

        var storageProvider = TryGetStorageProvider();
        if (storageProvider is null)
            return false;

        try
        {
            using (await EditorProjectIoGate.EnterAsync())
            {
                EditorFileAccessContext fileAccessContext;
                try
                {
                    fileAccessContext = await snapshot!.ToContextAsync(storageProvider);
                }
                catch (Exception exception) when (exception is IOException or InvalidDataException)
                {
                    Log.LogWarn($"Recent project {recordInfo.RecordId:N} can no longer be restored: {exception.Message}");
                    MarkPermanentlyInvalid(recordInfo);
                    await ShowInvalidRecentProjectAsync();
                    return false;
                }

                // TryLoadFromContextAsync consumes the context: it disposes the context on its own
                // failure and transfers ownership into the returned EditorContext on success. From
                // that point the EditorContext dispose releases the restored handles.
                var projectContext = await EditorProjectDataUtils.TryLoadFromContextAsync(fileAccessContext);
                if (!await TryTransferContextToEditorAsync(
                        editor,
                        projectContext,
                        projectContext.ProjectFile?.FileName ?? string.Empty))
                {
                    return false;
                }

                TryUpdateRecentProject(recordInfo, snapshot, projectContext);
                return true;
            }
        }
        catch (Exception exception)
        {
            Log.LogError($"Failed to open recent project record {recordInfo.RecordId:N}: {exception.Message}");
            await DialogManager.ShowMessageDialog(
                $"Unable to open the recent project: {exception.Message}",
                DialogMessageType.Error);
            return false;
        }
    }

    public async Task<bool> CheckIsValid(RecentRecordInfo recordInfo)
    {
        if (!TryReadSnapshot(recordInfo, out var snapshot))
        {
            MarkPermanentlyInvalid(recordInfo);
            return false;
        }

        var storageProvider = TryGetStorageProvider();
        if (storageProvider is null)
            return false;

        try
        {
            using var ioLease = await EditorProjectIoGate.EnterAsync();
            using var context = await snapshot!.ToContextAsync(storageProvider);
            return true;
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException)
        {
            Log.LogWarn($"Recent project {recordInfo.RecordId:N} is no longer valid: {exception.Message}");
            MarkPermanentlyInvalid(recordInfo);
            return false;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch (Exception exception)
        {
            Log.LogWarn($"Recent project validation temporarily failed for record {recordInfo.RecordId:N}: {exception.Message}");
            return false;
        }
    }

    private static async Task<bool> TryTransferContextToEditorAsync(
        FumenVisualEditorViewModel editor,
        EditorContext context,
        string sourcePath)
    {
        try
        {
            if (await editor.LoadProjectAsync(context, sourcePath))
                return true;
        }
        catch
        {
            context.Dispose();
            throw;
        }

        context.Dispose();
        return false;
    }

    private async Task TryStoreRecentFromContextAsync(
        EditorContext projectContext,
        string projectName,
        string locationDescription)
    {
        var context = projectContext.FileAccessContext;
        if (context is null)
            return;

        try
        {
            var snapshot = await context.ToSnapshotAsync();
            projectContext.RecentRecordId = StoreRecentProject(projectName, locationDescription, snapshot);
        }
        catch (Exception exception)
        {
            // Bookmarks are unavailable on this platform or for this folder; per the recent-project
            // policy this does not fail the open, it only skips creating a recent record.
            // Recent-list persistence failures follow the same non-fatal policy.
            Log.LogWarn($"The opened project could not be stored in the recent list: {exception.Message}");
        }
    }

    private void TryUpdateRecentProject(
        RecentRecordInfo recordInfo,
        EditorFileAccessContextSnapshot snapshot,
        EditorContext projectContext)
    {
        try
        {
            var updated = RecentFilesManager.UpdateRecent(
                recordInfo.RecordId,
                projectContext.ProjectFile?.FileName ?? recordInfo.LocationDescription,
                recordInfo.LocationDescription,
                snapshot.Serialize());
            projectContext.RecentRecordId = updated.RecordId;
        }
        catch (Exception exception)
        {
            Log.LogWarn($"Unable to update recent project record {recordInfo.RecordId:N}: {exception.Message}");
        }
    }

    private async Task<string?> SelectProjectLocatorAsync(
        IReadOnlyList<(string Locator, ISimpleFile File)> candidates)
    {
        if (candidates.Count == 1)
            return candidates[0].Locator;

        var dialog = new ProjectFileSelectionDialogViewModel(candidates.Select(x => x.Locator));
        var result = await IoC.Get<IWindowManager>().ShowDialogAsync(dialog);
        return result == true ? dialog.SelectedProjectLocator : null;
    }

    private async Task<(ISimpleFile FumenFile, ISimpleFile AudioFile)?> SelectProjectFilesAsync(
        ISimpleDirectory projectRoot,
        string projectLocator)
    {
        var fumenCandidates = EditorProjectPathResolver.FindFiles(
            projectRoot,
            FileDialogHelper.GetSupportFumenOpenFileExtensionFilterList().Select(x => x.ext));
        var audioCandidates = EditorProjectPathResolver.FindFiles(
            projectRoot,
            FileDialogHelper.GetSupportAudioFileExtensionFilterList().Select(x => x.ext));

        using var dialog = new ProjectFileBindingDialogViewModel(
            projectLocator,
            fumenCandidates,
            audioCandidates);
        var result = await IoC.Get<IWindowManager>().ShowDialogAsync(dialog);
        return result == true ? dialog.TakeSelection() : null;
    }

    private static async Task<bool> TryBindExternalAwbAsync(EditorFileAccessContext context)
    {
        var audioFile = context.AudioFile ??
            throw new InvalidDataException("The project file binding has no audio file.");
        if (!Path.GetExtension(audioFile.FileName).Equals(".acb", StringComparison.OrdinalIgnoreCase))
            return true;

        string? expectedAwbFileName;
        await using (var acbStream = await audioFile.OpenRead())
        using (var acb = AcbFile.FromStream(acbStream, audioFile.FileName, disposeStream: false))
        {
            if (acb.InternalAwb is not null)
                return true;

            expectedAwbFileName = acb.ExternalAwb?.FileName;
        }

        if (string.IsNullOrWhiteSpace(expectedAwbFileName))
            throw new InvalidDataException($"Audio '{audioFile.FileName}' has no usable AWB data.");

        var siblingMatches = audioFile.ParentDictionary?.ChildFiles
            .Where(file => file.FileName.Equals(expectedAwbFileName, StringComparison.OrdinalIgnoreCase))
            .ToArray() ?? [];
        if (siblingMatches.Length > 1)
        {
            throw new InvalidDataException(
                $"Audio '{audioFile.FileName}' has multiple AWB candidates named '{expectedAwbFileName}'.");
        }

        if (siblingMatches.Length == 1)
        {
            context.AudioAwbFile = siblingMatches[0];
            return true;
        }

        var selectedAwbFile = await FileDialogHelper.OpenFileAsync(
            $"Select external AWB file ({expectedAwbFileName})",
            [(".awb", "AWB audio archive")]);
        if (selectedAwbFile is null)
            return false;

        context.AudioAwbFile = selectedAwbFile;
        return true;
    }

    private Guid StoreRecentProject(
        string projectName,
        string locationDescription,
        EditorFileAccessContextSnapshot snapshot)
    {
        var serialized = snapshot.Serialize();
        var fileType = SupportFileTypes[0];
        var existing = RecentFilesManager.RecentRecordInfos.FirstOrDefault(record =>
            record.EditorFileTypeId.Equals(fileType.Id, StringComparison.OrdinalIgnoreCase) &&
            TryReadSnapshot(record, out var stored) &&
            IsSameProjectIdentity(stored!, snapshot));

        return existing is null
            ? RecentFilesManager.PostRecent(
                fileType,
                projectName,
                locationDescription,
                serialized).RecordId
            : RecentFilesManager.UpdateRecent(
                existing.RecordId,
                projectName,
                locationDescription,
                serialized).RecordId;
    }

    // D38: only reuse an existing record when the project identity is provably identical.
    // Bookmarks are opaque; equal opaque values prove sameness, but different values never
    // prove difference, so unequal bookmarks simply create an independent recent record.
    private static bool IsSameProjectIdentity(
        EditorFileAccessContextSnapshot stored,
        EditorFileAccessContextSnapshot candidate) =>
        string.Equals(
            stored.ProjectDirectoryBookmark,
            candidate.ProjectDirectoryBookmark,
            StringComparison.Ordinal) &&
        string.Equals(
            stored.ProjectFileBookmark ?? string.Empty,
            candidate.ProjectFileBookmark ?? string.Empty,
            StringComparison.Ordinal);

    private bool TryReadSnapshot(
        RecentRecordInfo recordInfo,
        out EditorFileAccessContextSnapshot? snapshot)
    {
        try
        {
            return EditorFileAccessContextSnapshot.TryDeserialize(
                RecentFilesManager.ReadData(recordInfo),
                out snapshot);
        }
        catch (Exception exception)
        {
            Log.LogWarn($"Recent project data is invalid for record {recordInfo.RecordId:N}: {exception.Message}");
            snapshot = null;
            return false;
        }
    }

    private void MarkPermanentlyInvalid(RecentRecordInfo recordInfo)
    {
        try
        {
            RecentFilesManager.SetMarkedInvalid(recordInfo, true);
        }
        catch (Exception exception)
        {
            Log.LogWarn($"Unable to persist invalid state for recent record {recordInfo.RecordId:N}: {exception.Message}");
        }
    }

    private static IStorageProvider? TryGetStorageProvider()
    {
        try
        {
            return (Application.Current as App)?.TopLevel?.StorageProvider;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    private static string BuildLocationDescription(string folderName, string projectLocator) =>
        string.IsNullOrWhiteSpace(folderName)
            ? projectLocator
            : $"{folderName}/{projectLocator}";

    private Task ShowInvalidRecentProjectAsync() =>
        DialogManager.ShowMessageDialog(
            "The recent project permission or project file is no longer available.",
            DialogMessageType.Error);
}
