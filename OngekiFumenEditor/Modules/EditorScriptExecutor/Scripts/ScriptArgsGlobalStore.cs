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
            // 只解除 per-assembly 精确映射；lastEditor 必须保留。
            // 脚本通过 LambdaUndoAction 入队的 redo/undo lambda 会在执行器的注册窗口之外运行
            // （组合动作在 Execute 返回后才执行，editor.undo / editor.redo 更可能在之后任意时刻触发），
            // 此时 lambda 里重新读取的 ScriptArgs.TargetEditor 只能靠这个回退解析出编辑器；
            // 若在这里清掉，undo/redo 会拿到 null 而抛 NullReferenceException。
            editmapStore.Remove(assembly);
        }
    }
}
