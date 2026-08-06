#nullable enable

using Avalonia;
using Avalonia.Platform.Storage;
using Gekimini.Avalonia;
using Gekimini.Avalonia.Framework;
using Gekimini.Avalonia.Framework.RecentFiles;
using Gekimini.Avalonia.Utils.MethodExtensions;
using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Base;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Models;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.ViewModels;
using OngekiFumenEditor.Avalonia.Utils.SimpleFileSystem.Impl.AvaloniaStorageProvider;

namespace OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor;

[RegisterSingleton<IEditorProvider>]
[RegisterSingleton<IFumenVisualEditorProvider>]
internal partial class FumenVisualEditorProvider : IFumenVisualEditorProvider
{
    public const string FILE_EXTENSION_NAME = ".nyagekiProj";
    public static EditorFileType[] SupportFileTypes { get; } =
    [
        new("FumenVisualEditorProject", "Fumen Visual Editor Project".ToLocalizedStringByRawText())
        {
            Patterns = [$"*{FILE_EXTENSION_NAME}"],
            MimeTypes = ["application/octet-stream"]
        }
    ];

    private IServiceProvider ServiceProvider => IoC.Get<IServiceProvider>();
    private IEditorRecentFilesManager RecentFilesManager => IoC.Get<IEditorRecentFilesManager>();

    public IEnumerable<EditorFileType> FileTypes => SupportFileTypes;

    public bool CanCreateNew => false;

    public IDocumentViewModel Create() => ServiceProvider.Resolve<FumenVisualEditorViewModel>();

    public Task<bool> TryNew(IDocumentViewModel document) =>
        document is FumenVisualEditorViewModel editor
            ? editor.New()
            : Task.FromResult(false);

    public Task<bool> TryOpen(IDocumentViewModel document) =>
        document is FumenVisualEditorViewModel editor
            ? editor.Load()
            : Task.FromResult(false);

    public Task<bool> TryOpen(IDocumentViewModel document, RecentRecordInfo recordInfo) =>
        document is FumenVisualEditorViewModel editor
            ? editor.Load(recordInfo)
            : Task.FromResult(false);

    public async Task<bool> CheckIsValid(RecentRecordInfo recordInfo)
    {
        if (!TryReadRecentData(recordInfo, out var recentData) ||
            !EditorProjectPathResolver.TryNormalizeRootRelativeLocator(
                recentData!.ProjectFileLocator,
                out var projectLocator,
                out _) ||
            !projectLocator.EndsWith(FILE_EXTENSION_NAME, StringComparison.OrdinalIgnoreCase))
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
            using var folder = await storageProvider.OpenFolderBookmarkAsync(recentData.FolderBookmark);
            if (folder is null)
            {
                MarkPermanentlyInvalid(recordInfo);
                return false;
            }

            if (AvaloniaStorageProviderFileSystemBuilder.IsLocalLink(folder) ||
                !await ContainsExactProjectFileAsync(folder, projectLocator))
            {
                MarkPermanentlyInvalid(recordInfo);
                return false;
            }

            return true;
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

    private static async Task<bool> ContainsExactProjectFileAsync(
        IStorageFolder root,
        string projectLocator)
    {
        var parts = projectLocator.Split('/', StringSplitOptions.RemoveEmptyEntries);
        IStorageFolder current = root;
        var ownedItems = new List<IStorageItem>();
        try
        {
            for (var i = 0; i < parts.Length; i++)
            {
                var item = await FindUniqueChildAsync(current, parts[i]);
                if (item is null)
                    return false;
                if (AvaloniaStorageProviderFileSystemBuilder.IsLocalLink(item))
                {
                    item.Dispose();
                    return false;
                }
                ownedItems.Add(item);

                if (i == parts.Length - 1)
                    return item is IStorageFile;

                if (item is not IStorageFolder folder)
                    return false;
                current = folder;
            }

            return false;
        }
        finally
        {
            for (var i = ownedItems.Count - 1; i >= 0; i--)
                ownedItems[i].Dispose();
        }
    }

    private static async Task<IStorageItem?> FindUniqueChildAsync(
        IStorageFolder folder,
        string expectedName)
    {
        IStorageItem? match = null;
        var hasConflict = false;
        try
        {
            await foreach (var item in folder.GetItemsAsync())
            {
                if (!item.Name.Equals(expectedName, StringComparison.OrdinalIgnoreCase))
                {
                    item.Dispose();
                    continue;
                }

                if (match is null && !hasConflict)
                {
                    match = item;
                    continue;
                }

                match?.Dispose();
                match = null;
                hasConflict = true;
                item.Dispose();
            }

            return hasConflict ? null : match;
        }
        catch
        {
            match?.Dispose();
            throw;
        }
    }
}
