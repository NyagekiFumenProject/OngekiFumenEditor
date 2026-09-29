using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using System.Linq;

namespace OngekiFumenEditor.Kernel.RuntimeAutomation
{
    public sealed class EditorContextInfo
    {
        public string EditorId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string ProjectPath { get; set; }
        public string FumenPath { get; set; }
        public bool IsDirty { get; set; }
        public bool IsActive { get; set; }
        public int LaneCount { get; set; }
        public int TapCount { get; set; }
        public int HoldCount { get; set; }
        public int BellCount { get; set; }
        public int BulletCount { get; set; }
        public int BpmChangeCount { get; set; }
        public int SoflanCount { get; set; }

        public static EditorContextInfo From(FumenVisualEditorViewModel editor)
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
