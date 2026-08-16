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
}
