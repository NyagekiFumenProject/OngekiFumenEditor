using Gekimini.Avalonia;
using Gekimini.Avalonia.Framework;
using Gekimini.Avalonia.Framework.RecentFiles;
using Gekimini.Avalonia.Utils.MethodExtensions;
using Injectio.Attributes;
using Microsoft.Extensions.DependencyInjection;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.ViewModels;
using System.IO;

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
    private IServiceProvider ServiceProvider => OngekiFumenEditor.Avalonia.IoC.Get<IServiceProvider>();

    public IEnumerable<EditorFileType> FileTypes => SupportFileTypes;

    public bool CanCreateNew => true;

    public IDocumentViewModel Create()
    {
        return ServiceProvider.Resolve<FumenVisualEditorViewModel>();
    }

    public async Task<bool> TryNew(IDocumentViewModel document)
    {
        if (document is not FumenVisualEditorViewModel editor)
            return false;
        return await editor.New();
    }

    public async Task<bool> TryOpen(IDocumentViewModel document)
    {
        if (document is not FumenVisualEditorViewModel editor)
            return false;
        return await editor.Load();
    }

    public async Task<bool> TryOpen(IDocumentViewModel document, RecentRecordInfo recordInfo)
    {
        if (document is not FumenVisualEditorViewModel editor)
            return false;
        return await editor.Load(recordInfo.LocationDescription);
    }

    public Task<bool> CheckIsValid(RecentRecordInfo recordInfo)
    {
        return Task.FromResult(!string.IsNullOrWhiteSpace(recordInfo.LocationDescription) &&
                               File.Exists(recordInfo.LocationDescription));
    }

    public async Task<bool> TryOpen(IDocumentViewModel document, string projectFilePath)
    {
        if (document is not FumenVisualEditorViewModel editor || string.IsNullOrWhiteSpace(projectFilePath))
            return false;
        return await editor.Load(projectFilePath);
    }

    public async Task<bool> TryOpen(IDocumentViewModel document, Models.EditorProjectDataModel projectModel)
    {
        if (document is not FumenVisualEditorViewModel editor || projectModel is null)
            return false;
        return await editor.Load(projectModel);
    }
}
