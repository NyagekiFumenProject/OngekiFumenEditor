using OngekiFumenEditor.Base;
using OngekiFumenEditor.Modules.FumenVisualEditor.Base;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Kernel
{
    public interface IFumenEditorClipboard
    {
        bool ContainPastableObjects { get; }
        IReadOnlyCollection<OngekiObjectBase> CurrentCopiedObjects { get; }

        Task PasteObjects(FumenVisualEditorViewModel targetEditor, PasteOption mirrorOption, Point? placePoint = default);
        Task CopyObjects(FumenVisualEditorViewModel sourceEditor, IEnumerable<ISelectableObject> objects);
    }
}
