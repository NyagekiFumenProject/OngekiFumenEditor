using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Kernel
{
    public interface IEditorDocumentManager
    {
        delegate void ActivateEditorChangedFunc(FumenVisualEditorViewModel @new, FumenVisualEditorViewModel old);

        public event ActivateEditorChangedFunc OnActivateEditorChanged;
        FumenVisualEditorViewModel CurrentActivatedEditor { get; }

        void NotifyDeactivate(FumenVisualEditorViewModel editor);
        void NotifyActivate(FumenVisualEditorViewModel editor);

        void NotifyCreate(FumenVisualEditorViewModel editor);
        void NotifyDestory(FumenVisualEditorViewModel editor);
    }
}
