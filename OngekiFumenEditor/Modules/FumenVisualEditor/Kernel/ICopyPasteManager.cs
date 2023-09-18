using OngekiFumenEditor.Base;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using static OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.FumenVisualEditorViewModel;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Kernel
{
    public interface IFumenEditorClipboard
    {
        bool ContainPastableObjects { get; }

        Task<bool> PasteObjects(FumenVisualEditorViewModel targetEditor, PasteMirrorOption mirrorOption, Point? placePoint = default);
        Task<bool> CopyObjects(FumenVisualEditorViewModel sourceEditor,IEnumerable<ISelectableObject> objects);
    }
}
