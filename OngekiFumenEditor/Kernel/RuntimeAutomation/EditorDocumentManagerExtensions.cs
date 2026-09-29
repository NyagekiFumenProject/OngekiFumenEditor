using OngekiFumenEditor.Modules.FumenVisualEditor.Kernel;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using System;
using System.Linq;

namespace OngekiFumenEditor.Kernel.RuntimeAutomation
{
    internal static class EditorDocumentManagerExtensions
    {
        /// <summary>
        /// 打开中的编辑器快照。<see cref="IEditorDocumentManager.GetCurrentEditors"/> 返回的是内部集合本体，
        /// 跨线程枚举前必须先物化，否则集合被 UI 线程改动时会抛 InvalidOperationException。
        /// </summary>
        public static FumenVisualEditorViewModel[] GetEditorSnapshot(this IEditorDocumentManager documentManager)
        {
            return documentManager?.GetCurrentEditors()?.ToArray() ?? Array.Empty<FumenVisualEditorViewModel>();
        }

        /// <summary>
        /// 按 <see cref="RuntimeAutomationEditorId"/> 解析编辑器（文档管理器本身没有 by-id 查询）。
        /// </summary>
        public static bool TryGetEditorById(this IEditorDocumentManager documentManager, string editorId, out FumenVisualEditorViewModel editor)
        {
            editor = string.IsNullOrWhiteSpace(editorId)
                ? null
                : documentManager.GetEditorSnapshot().FirstOrDefault(x => RuntimeAutomationEditorId.Generate(x) == editorId);

            return editor is not null;
        }
    }
}
