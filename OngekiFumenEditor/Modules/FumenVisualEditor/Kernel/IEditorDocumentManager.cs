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
        delegate void NotifyCreateFunc(FumenVisualEditorViewModel sender);
        delegate void NotifyDestoryFunc(FumenVisualEditorViewModel sender);

        public event NotifyCreateFunc OnNotifyCreated;
        public event ActivateEditorChangedFunc OnActivateEditorChanged;
        public event NotifyDestoryFunc OnNotifyDestoryed;

        FumenVisualEditorViewModel CurrentActivatedEditor { get; }

        void NotifyDeactivate(FumenVisualEditorViewModel editor);
        void NotifyActivate(FumenVisualEditorViewModel editor);

        void NotifyCreate(FumenVisualEditorViewModel editor);
        void NotifyDestory(FumenVisualEditorViewModel editor);

        IEnumerable<FumenVisualEditorViewModel> GetCurrentEditors();
    }
}
