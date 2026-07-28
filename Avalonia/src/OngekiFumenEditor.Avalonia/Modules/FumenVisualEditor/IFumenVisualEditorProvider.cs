using Gekimini.Avalonia.Framework;
using Gekimini.Avalonia.Framework.RecentFiles;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Models;

namespace OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor;

public interface IFumenVisualEditorProvider : IEditorProvider
{
    Task<bool> TryOpen(IDocumentViewModel document, EditorProjectDataModel projectModel);
    Task<bool> TryOpen(IDocumentViewModel document, string projectFilePath);
}
