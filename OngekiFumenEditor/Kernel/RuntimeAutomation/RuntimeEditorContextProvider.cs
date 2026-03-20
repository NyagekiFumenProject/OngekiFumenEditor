using OngekiFumenEditor.Modules.FumenVisualEditor.Kernel;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;

namespace OngekiFumenEditor.Kernel.RuntimeAutomation
{
    [Export(typeof(IRuntimeEditorContextProvider))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    internal sealed class RuntimeEditorContextProvider : IRuntimeEditorContextProvider
    {
        private readonly IEditorDocumentManager editorDocumentManager;

        [ImportingConstructor]
        public RuntimeEditorContextProvider(IEditorDocumentManager editorDocumentManager)
        {
            this.editorDocumentManager = editorDocumentManager;
        }

        public EditorContextInfo GetCurrentEditor()
        {
            return ToEditorContextInfo(editorDocumentManager.CurrentActivatedEditor);
        }

        public EditorContextInfo GetEditor(string editorId)
        {
            return ToEditorContextInfo(editorDocumentManager.GetCurrentEditors()
                .FirstOrDefault(x => RuntimeAutomationEditorId.Generate(x) == editorId));
        }

        public IReadOnlyList<EditorContextInfo> GetOpenedEditors()
        {
            return editorDocumentManager.GetCurrentEditors()
                .Select(ToEditorContextInfo)
                .Where(x => x is not null)
                .ToArray();
        }

        private static EditorContextInfo ToEditorContextInfo(FumenVisualEditorViewModel editor)
        {
            if (editor is null)
                return default;

            return new EditorContextInfo
            {
                EditorId = RuntimeAutomationEditorId.Generate(editor),
                DisplayName = editor.DisplayName ?? string.Empty,
                ProjectPath = string.IsNullOrWhiteSpace(editor.FilePath) ? default : editor.FilePath,
                FumenPath = string.IsNullOrWhiteSpace(editor.EditorProjectData?.FumenFilePath) ? default : editor.EditorProjectData.FumenFilePath,
                IsDirty = editor.IsDirty,
                IsActive = editor.IsActive,
                LaneCount = editor.Fumen?.Lanes?.Count ?? 0,
                TapCount = editor.Fumen?.Taps?.Count ?? 0,
                HoldCount = editor.Fumen?.Holds?.Count ?? 0,
                BellCount = editor.Fumen?.Bells?.Count ?? 0,
                BulletCount = editor.Fumen?.Bullets?.Count ?? 0,
                BpmChangeCount = editor.Fumen?.BpmList?.Count ?? 0,
                SoflanCount = editor.Fumen?.SoflansMap?.Values?.Sum(x => x.Count) ?? 0,
            };
        }
    }
}
