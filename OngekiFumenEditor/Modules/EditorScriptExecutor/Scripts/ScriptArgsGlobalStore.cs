using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Modules.EditorScriptExecutor.Scripts
{
    internal static class ScriptArgsGlobalStore
    {
        private static Dictionary<Assembly, FumenVisualEditorViewModel> editmapStore = new Dictionary<Assembly, FumenVisualEditorViewModel>();

        public static FumenVisualEditorViewModel GetCurrentEditor(Assembly assembly)
        {
            return editmapStore.TryGetValue(assembly, out var editor) ? editor : default;
        }

        public static void SetCurrentEditor(Assembly assembly, FumenVisualEditorViewModel editor)
        {
            editmapStore[assembly] = editor;
        }

        public static void Clear(Assembly assembly)
        {
            editmapStore.Remove(assembly);
        }
    }
}
