using Gekimini.Avalonia.Framework;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Models;

namespace OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor;

public interface IFumenVisualEditorProvider : IEditorProvider
{
    // The caller retains ownership when the editor rejects the context or loading throws.
    // A successful result transfers ownership to the document view model.
    Task<bool> TryOpen(IDocumentViewModel document, EditorContext context);
}
