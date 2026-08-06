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
            var folderBookmark = await TrySaveFolderBookmarkAsync(selectedFolder);

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
            var projectData = await LoadProjectAsync(rootForLoad, selectedProject.File, selectedLocator);
            if (projectData is null)
                return false;

            if (!string.IsNullOrWhiteSpace(folderBookmark))
            {
                var recentData = new FumenVisualEditorRecentRecordData
                {
                    FolderBookmark = folderBookmark,
                    ProjectFileLocator = selectedLocator
                };
                projectData.RecentRecordId = StoreRecentProject(
                    selectedProject.File.FileName,
                    BuildLocationDescription(folderDisplayName, selectedLocator),
                    recentData);
            }

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
        if (!TryReadRecentData(recordInfo, out var recentData))
        {
            MarkPermanentlyInvalid(recordInfo);
            await ShowInvalidRecentProjectAsync();
            return false;
        }

        var storageProvider = TryGetStorageProvider();
        if (storageProvider is null)
            return false;

        IStorageFolder? storageFolder = null;
        ISimpleDirectory? projectRoot = null;
        try
        {
            using (await EditorProjectIoGate.EnterAsync())
            {
                storageFolder = await storageProvider.OpenFolderBookmarkAsync(recentData!.FolderBookmark);
                if (storageFolder is null)
                {
                    MarkPermanentlyInvalid(recordInfo);
                    return false;
                }

                var transferredFolder = storageFolder;
                storageFolder = null;
                projectRoot = await AvaloniaStorageProviderFileSystemBuilder
                    .LoadFromAvaloniaStorageFolder(transferredFolder);

                if (!EditorProjectPathResolver.TryFindFile(
                        projectRoot,
                        recentData.ProjectFileLocator,
                        out var projectFile,
                        out var actualLocator,
                        out _)
                    || !actualLocator.EndsWith(
                        FumenVisualEditorProvider.FILE_EXTENSION_NAME,
                        StringComparison.OrdinalIgnoreCase))
                {
                    MarkPermanentlyInvalid(recordInfo);
                    return false;
                }

                var rootForLoad = projectRoot;
                projectRoot = null;
                var projectData = await LoadProjectWithoutGateAsync(
                    rootForLoad,
                    projectFile!,
                    actualLocator);
                if (projectData is null)
                    return false;

                var updated = RecentFilesManager.UpdateRecent(
                    recordInfo.RecordId,
                    projectFile!.FileName,
                    recordInfo.LocationDescription,
                    FumenVisualEditorRecentRecordData.Serialize(recentData));
                projectData.RecentRecordId = updated.RecordId;
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
        finally
        {
            projectRoot?.Dispose();
            storageFolder?.Dispose();
        }
    }

    private async Task<EditorProjectDataModel?> LoadProjectAsync(
        ISimpleDirectory projectRoot,
        ISimpleFile projectFile,
        string projectLocator)
    {
        using var ioLease = await EditorProjectIoGate.EnterAsync();
        return await LoadProjectWithoutGateAsync(projectRoot, projectFile, projectLocator);
    }

    private async Task<EditorProjectDataModel?> LoadProjectWithoutGateAsync(
        ISimpleDirectory projectRoot,
        ISimpleFile projectFile,
        string projectLocator)
    {
        EditorProjectDataModel? projectData = null;
        try
        {
            projectData = await EditorProjectDataUtils.TryLoadFromFileAsync(
                projectRoot,
                projectFile,
                projectLocator);
            if (await LoadProjectAsync(projectData, projectLocator))
                return projectData;

            projectData.DisposeRuntimeFiles();
            return null;
        }
        catch
        {
            if (projectData is null)
                projectRoot.Dispose();
            else
                projectData.DisposeRuntimeFiles();
            throw;
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

    private Guid StoreRecentProject(
        string projectName,
        string locationDescription,
        FumenVisualEditorRecentRecordData recentData)
    {
        var serialized = FumenVisualEditorRecentRecordData.Serialize(recentData);
        var fileType = FumenVisualEditorProvider.SupportFileTypes[0];
        var existing = RecentFilesManager.RecentRecordInfos.FirstOrDefault(record =>
            record.EditorFileTypeId.Equals(fileType.Id, StringComparison.OrdinalIgnoreCase) &&
            TryReadRecentData(record, out var stored) &&
            stored!.FolderBookmark.Equals(recentData.FolderBookmark, StringComparison.Ordinal) &&
            stored.ProjectFileLocator.Equals(recentData.ProjectFileLocator, StringComparison.OrdinalIgnoreCase));

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

    private bool TryReadRecentData(
        RecentRecordInfo recordInfo,
        out FumenVisualEditorRecentRecordData? recentData)
    {
        try
        {
            return FumenVisualEditorRecentRecordData.TryDeserialize(
                RecentFilesManager.ReadData(recordInfo),
                out recentData);
        }
        catch (Exception exception)
        {
            Log.LogWarn($"Recent project data is invalid for record {recordInfo.RecordId:N}: {exception.Message}");
            recentData = null;
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

    private static async Task<string?> TrySaveFolderBookmarkAsync(IStorageFolder folder)
    {
        if (!folder.CanBookmark)
            return null;

        try
        {
            var bookmark = await folder.SaveBookmarkAsync();
            return string.IsNullOrWhiteSpace(bookmark) ? null : bookmark;
        }
        catch (Exception exception)
        {
            Log.LogWarn($"The selected project folder could not be bookmarked: {exception.Message}");
            return null;
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
