using Gemini.Framework;
using OngekiFumenEditor.Modules.FumenVisualEditor.Models;

namespace OngekiFumenEditor.Modules.FumenVisualEditorSettings
{
    public interface IFumenVisualEditorSettings : ITool
    {
        public EditorSetting Setting { get; set; }
    }
}
