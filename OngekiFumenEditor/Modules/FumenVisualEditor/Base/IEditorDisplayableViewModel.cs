using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Base
{
    public interface IEditorDisplayableViewModel
    {
        void OnObjectCreated(object createFrom, FumenVisualEditorViewModel editorViewModel);
    }
}
