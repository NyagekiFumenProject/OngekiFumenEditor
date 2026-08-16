#nullable enable

using Avalonia;
using Avalonia.Platform.Storage;
using Gekimini.Avalonia.Framework.Dialogs;
using Gekimini.Avalonia.Framework.RecentFiles;
using Gekimini.Avalonia.Platforms.Services.Window;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Base;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Models;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.ViewModels.Dialogs;
using OngekiFumenEditor.Avalonia.Utils;
using OngekiFumenEditor.Avalonia.Utils.SimpleFileSystem;
using OngekiFumenEditor.Avalonia.Utils.SimpleFileSystem.Impl.AvaloniaStorageProvider;

namespace OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.ViewModels;

public partial class FumenVisualEditorViewModel
{
    private IEditorRecentFilesManager RecentFilesManager => IoC.Get<IEditorRecentFilesManager>();
    private IDialogManager DialogManager => IoC.Get<IDialogManager>();

    public virtual Task<bool> New()
    {
        Log.LogWarn("FumenVisualEditor does not currently support creating a project without an existing project folder.");
        return Task.FromResult(false);
    }

    public virtual async Task<bool> Load()
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
                FumenVisualEditorProvider.FILE_EXTENSION_NAME);
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
            var rootForLoad = projectRoot;
            projectRoot = null;
            var projectContext = await LoadProjectAsync(rootForLoad, selectedProject.File, selectedLocator);
            if (projectContext is null)
                return false;

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

    public virtual async Task<bool> Load(RecentRecordInfo recordInfo)
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
                EditorFileAccessContext context;
                try
                {
                    context = await snapshot!.ToContextAsync(storageProvider);
                }
                catch (Exception exception) when (exception is IOException or InvalidDataException)
                {
                    Log.LogWarn($"Recent project {recordInfo.RecordId:N} can no longer be restored: {exception.Message}");
                    MarkPermanentlyInvalid(recordInfo);
                    await ShowInvalidRecentProjectAsync();
                    return false;
                }

                var projectContext = await LoadProjectFromContextWithoutGateAsync(context);
                if (projectContext is null)
                    return false;

                var updated = RecentFilesManager.UpdateRecent(
                    recordInfo.RecordId,
                    projectContext.ProjectFile?.FileName ?? recordInfo.LocationDescription,
                    recordInfo.LocationDescription,
                    snapshot.Serialize());
                projectContext.RecentRecordId = updated.RecordId;
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

    private async Task<EditorContext?> LoadProjectAsync(
        ISimpleDirectory projectRoot,
        ISimpleFile projectFile,
        string projectLocator)
    {
        using var ioLease = await EditorProjectIoGate.EnterAsync();
        return await LoadProjectWithoutGateAsync(projectRoot, projectFile, projectLocator);
    }

    private async Task<EditorContext?> LoadProjectWithoutGateAsync(
        ISimpleDirectory projectRoot,
        ISimpleFile projectFile,
        string projectLocator)
    {
        EditorContext? projectContext = null;
        try
        {
            projectContext = await EditorProjectDataUtils.TryLoadFromFileAsync(
                projectRoot,
                projectFile,
                projectLocator);
            if (await LoadProjectAsync(projectContext, projectLocator))
                return projectContext;

            projectContext.Dispose();
            return null;
        }
        catch
        {
            projectContext?.Dispose();
            throw;
        }
    }

    private async Task<EditorContext?> LoadProjectFromContextWithoutGateAsync(
        EditorFileAccessContext context)
    {
        // TryLoadFromContextAsync consumes the context: it disposes the context on its own
        // failure and transfers ownership into the returned EditorContext on success. From
        // that point the EditorContext dispose releases the restored handles.
        var projectContext = await EditorProjectDataUtils.TryLoadFromContextAsync(context);
        try
        {
            if (await LoadProjectAsync(projectContext, projectContext.ProjectFile?.FileName ?? string.Empty))
                return projectContext;

            projectContext.Dispose();
            return null;
        }
        catch
        {
            projectContext.Dispose();
            throw;
        }
    }

    private async Task TryStoreRecentFromContextAsync(
        EditorContext projectContext,
        string projectName,
        string locationDescription)
    {
        var context = projectContext.FileAccessContext;
        if (context is null)
            return;

        EditorFileAccessContextSnapshot snapshot;
        try
        {
            snapshot = await context.ToSnapshotAsync();
        }
        catch (Exception exception)
        {
            // Bookmarks are unavailable on this platform or for this folder; per the recent-project
            // policy this does not fail the open, it only skips creating a recent record.
            Log.LogWarn($"The opened project could not be bookmarked for the recent list: {exception.Message}");
            return;
        }

        projectContext.RecentRecordId = StoreRecentProject(projectName, locationDescription, snapshot);
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

    private Guid StoreRecentProject(
        string projectName,
        string locationDescription,
        EditorFileAccessContextSnapshot snapshot)
    {
        var serialized = snapshot.Serialize();
        var fileType = FumenVisualEditorProvider.SupportFileTypes[0];
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
