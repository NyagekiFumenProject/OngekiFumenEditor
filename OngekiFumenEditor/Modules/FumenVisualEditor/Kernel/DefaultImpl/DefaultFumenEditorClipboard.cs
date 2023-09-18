using OngekiFumenEditor.Base;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Kernel.DefaultImpl
{
    [Export(typeof(IFumenEditorClipboard))]
    internal class DefaultFumenEditorClipboard : IFumenEditorClipboard
    {
        public bool ContainPastableObjects => throw new NotImplementedException();

        public Task<bool> CopyObjects(FumenVisualEditorViewModel sourceEditor, IEnumerable<ISelectableObject> objects)
        {
            throw new NotImplementedException();
        }

        public Task<bool> PasteObjects(FumenVisualEditorViewModel targetEditor, FumenVisualEditorViewModel.PasteMirrorOption mirrorOption, Point? placePoint = null)
        {
            throw new NotImplementedException();
        }
    }
}
