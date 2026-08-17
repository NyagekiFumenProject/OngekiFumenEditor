#nullable enable

using Gekimini.Avalonia.Framework.Dialogs;
using Gekimini.Avalonia.Framework.RecentFiles;
using Gekimini.Avalonia.Platforms.Services.Window;
using OngekiFumenEditor.Avalonia.Kernel.Audio;
using OngekiFumenEditor.Avalonia.Parser;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Base;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Models;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Setup;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.ViewModels;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.ViewModels.Dialogs;
using OngekiFumenEditor.Avalonia.Utils;
using OngekiFumenEditor.Avalonia.Utils.SimpleFileSystem;

namespace OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor;

public abstract partial class FumenVisualEditorProviderBase
{
    private IDialogManager DialogManager => IoC.Get<IDialogManager>();

    private async Task<bool> OpenFromFolderAsync(FumenVisualEditorViewModel editor)
    {
        var picker = CreateSetupFilePicker();
        using var directorySelection = await picker.PickProjectDirectoryAsync();
        if (directorySelection is null)
            return false;

        ISimpleDirectory? projectRoot = null;
        try
        {
            var folderDisplayName = directorySelection.DisplayName;
            projectRoot = directorySelection.TakeDirectory();

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
                selectedLocator,
                picker);
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

        try
        {
            using (await EditorProjectIoGate.EnterAsync())
            {
                EditorFileAccessContext fileAccessContext;
                try
                {
                    fileAccessContext = await RestoreContextAsync(snapshot!);
                    try
                    {
                        if (!await TryBindExternalAwbAsync(fileAccessContext, allowExternalPicker: false))
                        {
                            await ShowRecentExternalAwbUnavailableAsync();
                            fileAccessContext.Dispose();
                            return false;
                        }
                    }
                    catch
                    {
                        fileAccessContext.Dispose();
                        throw;
                    }
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

                TryUpdateRecentProject(recordInfo, snapshot!, projectContext);
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

        try
        {
            using var ioLease = await EditorProjectIoGate.EnterAsync();
            using var context = await RestoreContextAsync(snapshot!);
            return await TryBindExternalAwbAsync(context, allowExternalPicker: false);
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
        string projectLocator,
        IEditorProjectSetupFilePicker picker)
    {
        var fumenExtensions = IoC.Get<IFumenParserManager>()
            .GetDeserializerDescriptions()
            .SelectMany(x => x.fileFormat.Select(y => (ext: y, desc: x.desc)));
        var audioExtensions = IoC.Get<IAudioManager>()
            .SupportAudioFileExtensionList
            .Where(x => picker.SupportsAcb ||
                !x.fileExt.Equals(".acb", StringComparison.OrdinalIgnoreCase));
        var fumenCandidates = EditorProjectPathResolver.FindFiles(
            projectRoot,
            fumenExtensions.Select(x => x.ext));
        var audioCandidates = EditorProjectPathResolver.FindFiles(
            projectRoot,
            audioExtensions.Select(x => x.fileExt));

        using var dialog = new ProjectFileBindingDialogViewModel(
            projectLocator,
            fumenCandidates,
            audioCandidates,
            () => picker.PickExistingFumenAsync(),
            () => picker.PickAudioAsync());
        var result = await IoC.Get<IWindowManager>().ShowDialogAsync(dialog);
        return result == true ? dialog.TakeSelection() : null;
    }

    private async Task<bool> TryBindExternalAwbAsync(
        EditorFileAccessContext context,
        bool allowExternalPicker = true)
    {
        var audioFile = context.AudioFile ??
            throw new InvalidDataException("The project file binding has no audio file.");
        var picker = CreateSetupFilePicker();
        var inspection = await Setup.AcbPackageInspector.InspectAsync(
            audioFile,
            picker.SupportsAcb);
        if (!inspection.IsValid)
            throw new InvalidDataException(inspection.ErrorMessage);
        if (inspection.Kind != Setup.SetupAudioPackageKind.AcbWithExternalAwb)
            return true;

        // A snapshot may already carry an external AWB bookmark. Keep that capability
        // instead of replacing it with a sibling alias and losing its ownership reference.
        if (context.AudioAwbFile is not null)
            return true;

        var expectedAwbFileName = inspection.RequiredExternalAwbLeafName!;

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

        if (!allowExternalPicker)
            return false;

        var selectedAwbFile = await picker.PickExternalAwbAsync(expectedAwbFileName);
        if (selectedAwbFile is null)
            return false;

        context.AudioAwbFile = selectedAwbFile;
        return true;
    }

    private Task ShowRecentExternalAwbUnavailableAsync() =>
        DialogManager.ShowMessageDialog(
            "This recent project needs an external AWB file that is no longer available. Open the project folder to bind it again.",
            DialogMessageType.Error);

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

    private static string BuildLocationDescription(string folderName, string projectLocator) =>
        string.IsNullOrWhiteSpace(folderName)
            ? projectLocator
            : $"{folderName}/{projectLocator}";

    private Task ShowInvalidRecentProjectAsync() =>
        DialogManager.ShowMessageDialog(
            "The recent project permission or project file is no longer available.",
            DialogMessageType.Error);
}
