using Gekimini.Avalonia.Framework;
using OngekiFumenEditor.Avalonia.Base;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.ViewModels;

namespace OngekiFumenEditor.Avalonia.Modules.FumenObjectPropertyBrowser;

public interface IFumenObjectPropertyBrowser : IToolViewModel
{
    IReadOnlySet<ISelectableObject> SelectedObjects { get; }
    FumenVisualEditorViewModel Editor { get; }

    void RefreshSelected(FumenVisualEditorViewModel referenceEditor);
    void RefreshSelected(FumenVisualEditorViewModel referenceEditor, params object[] ongekiObj);
}
