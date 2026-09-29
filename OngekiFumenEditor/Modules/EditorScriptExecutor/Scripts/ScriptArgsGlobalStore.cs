using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using System.Collections.Generic;
using System.Reflection;

namespace OngekiFumenEditor.Modules.EditorScriptExecutor.Scripts
{
    internal static class ScriptArgsGlobalStore
    {
        private static Dictionary<Assembly, FumenVisualEditorViewModel> editmapStore = new Dictionary<Assembly, FumenVisualEditorViewModel>();

        // 精确匹配保留 per-assembly 语义；查不到时回退到最近一次注册的编辑器——脚本 lambda 可能在执行器的
        // 注册窗口之外运行（组合动作由 RuntimeAutomationScriptHost 在 Execute 返回后才真正执行）。
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
