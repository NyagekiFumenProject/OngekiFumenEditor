using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using OngekiFumenEditor.Utils;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static OngekiFumenEditor.Modules.FumenVisualEditor.Kernel.IEditorDocumentManager;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Kernel.DefaultImpl
{
    [Export(typeof(IEditorDocumentManager))]
    public class DefaultEditorDocumentManager : IEditorDocumentManager
    {
        private HashSet<FumenVisualEditorViewModel> currentEditor = new();

        public event ActivateEditorChangedFunc OnActivateEditorChanged;

        private FumenVisualEditorViewModel currentActivatedEditor;
        public FumenVisualEditorViewModel CurrentActivatedEditor
        {
            get => currentActivatedEditor;
            private set
            {
                var old = currentActivatedEditor;
                currentActivatedEditor = value;
                OnActivateEditorChanged?.Invoke(value, old);
            }
        }

        public void NotifyDeactivate(FumenVisualEditorViewModel editor)
        {
            Log.LogInfo($"editor deactivated: {editor.GetHashCode()} {editor.DisplayName}");
            var otherActive = currentEditor.Where(x => x != editor).FirstOrDefault(x => x.IsActive);
            CurrentActivatedEditor = otherActive;
        }

        public void NotifyActivate(FumenVisualEditorViewModel editor)
        {
            Log.LogInfo($"editor activated: {editor.GetHashCode()} {editor.DisplayName}");
            CurrentActivatedEditor = editor;
        }

        public void NotifyCreate(FumenVisualEditorViewModel editor)
        {
            Log.LogInfo($"editor created: {editor.GetHashCode()} {editor.DisplayName}");
            currentEditor.Add(editor);
        }

        public void NotifyDestory(FumenVisualEditorViewModel editor)
        {
            Log.LogInfo($"editor destoryed: {editor.GetHashCode()} {editor.DisplayName}");
            currentEditor.Remove(editor);
            if (CurrentActivatedEditor == editor)
                NotifyDeactivate(editor);
        }
    }
}
