#nullable enable

using Gekimini.Avalonia;
using Gekimini.Avalonia.Framework;
using Gekimini.Avalonia.Framework.RecentFiles;
using Gekimini.Avalonia.Utils.MethodExtensions;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Models;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Setup;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.ViewModels;

namespace OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor;

/// <summary>
/// Platform-neutral provider implementation. It deliberately has no DI registration;
/// Desktop and Browser composition roots each register one concrete instance under both
/// provider interfaces so the two interfaces observe the same singleton.
/// </summary>
public abstract partial class FumenVisualEditorProviderBase : IFumenVisualEditorProvider
{
    public const string FILE_EXTENSION_NAME = ".nyagekiProj";
    public static EditorFileType FileType { get; } =
        new("FumenVisualEditorProject", "Fumen Visual Editor Project".ToLocalizedStringByRawText())
        {
            Patterns = [$"*{FILE_EXTENSION_NAME}"],
            MimeTypes = ["application/octet-stream"]
        };

    public static EditorFileType[] SupportFileTypes { get; } =
    [
        FileType
    ];

    private IServiceProvider ServiceProvider => IoC.Get<IServiceProvider>();
    private IEditorRecentFilesManager RecentFilesManager => IoC.Get<IEditorRecentFilesManager>();

    public IEnumerable<EditorFileType> FileTypes => SupportFileTypes;

    public abstract bool CanCreateNew { get; }

    protected abstract IEditorProjectSetupFilePicker CreateSetupFilePicker();

    /// <summary>
    /// Restores a recent-project snapshot through the active platform's storage provider.
    /// The shared provider only coordinates project loading; platform composition roots own
    /// Avalonia storage access so Browser and Desktop can supply their respective providers.
    /// </summary>
    protected abstract Task<EditorFileAccessContext> RestoreContextAsync(
        EditorFileAccessContextSnapshot snapshot,
        CancellationToken cancellationToken = default);

    public IDocumentViewModel Create() => ServiceProvider.Resolve<FumenVisualEditorViewModel>();

    public Task<bool> TryNew(IDocumentViewModel document) =>
        document is FumenVisualEditorViewModel editor
            ? CreateNewProjectAsync(editor)
            : Task.FromResult(false);

    public Task<bool> TryOpen(IDocumentViewModel document) =>
        document is FumenVisualEditorViewModel editor
            ? OpenFromFolderAsync(editor)
            : Task.FromResult(false);

    public Task<bool> TryOpen(IDocumentViewModel document, RecentRecordInfo recordInfo) =>
        document is FumenVisualEditorViewModel editor
            ? OpenFromRecentAsync(editor, recordInfo)
            : Task.FromResult(false);

    public Task<bool> TryOpen(IDocumentViewModel document, EditorContext context) =>
        document is FumenVisualEditorViewModel editor
            ? editor.LoadProjectAsync(context, ResolveSourcePath(context))
            : Task.FromResult(false);

    private static string ResolveSourcePath(EditorContext context) =>
        context.ProjectFile?.FileName ?? context.FumenFile?.FileName ?? string.Empty;
}
