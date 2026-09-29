using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using System.Collections.Generic;
using System.Reflection;

namespace OngekiFumenEditor.Modules.EditorScriptExecutor.Scripts
{
    internal static class ScriptArgsGlobalStore
    {
        private static Dictionary<Assembly, FumenVisualEditorViewModel> editmapStore = new Dictionary<Assembly, FumenVisualEditorViewModel>();

        // 脚本以 Assembly.Load(byte[], byte[]) 载入后，ScriptArgs.TargetEditor 里的 Assembly.GetCallingAssembly()
        // 会归因到宿主程序集（getter 未被内联），于是按脚本程序集注册的项查不到。这里保留精确匹配，并回退到
        // 最近一次注册的编辑器（脚本执行是串行的，见 RuntimeAutomationScriptHost / DefaultEditorScriptExecutor）。
        private static FumenVisualEditorViewModel lastEditor;

        public static FumenVisualEditorViewModel GetCurrentEditor(Assembly assembly)
        {
            if (assembly is not null && editmapStore.TryGetValue(assembly, out var editor))
                return editor;

            return lastEditor;
        }

        public static void SetCurrentEditor(Assembly assembly, FumenVisualEditorViewModel editor)
        {
            editmapStore[assembly] = editor;
            lastEditor = editor;
        }

        public static void Clear(Assembly assembly)
        {
            editmapStore.Remove(assembly);
            lastEditor = default;
        }
    }
}
