using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Modules.EditorScriptExecutor.Kernel
{
    public class ScriptExecutionContext
    {
        public IEditorScriptExecutor ScriptExecutor { get; set; }
        public FumenVisualEditorViewModel Editor { get; set; }
    }
}
