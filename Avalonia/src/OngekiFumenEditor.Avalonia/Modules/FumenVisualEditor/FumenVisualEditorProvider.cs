#nullable enable

using Gekimini.Avalonia;
using Gekimini.Avalonia.Framework;
using Gekimini.Avalonia.Framework.RecentFiles;
using Gekimini.Avalonia.Utils.MethodExtensions;
using Injectio.Attributes;
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
        !string.IsNullOrWhiteSpace(context.ProjectFileLocator)
            ? context.ProjectFileLocator
            : context.FumenFile?.FileName ?? context.ProjectData.FumenFilePath ?? string.Empty;
}
