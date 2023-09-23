using Caliburn.Micro;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Modules.EditorScriptExecutor.Scripts
{
    public static class ScriptArgs
    {
        public static FumenVisualEditorViewModel TargetEditor => ScriptArgsGlobalStore.GetCurrentEditor(Assembly.GetCallingAssembly());
    }
}
