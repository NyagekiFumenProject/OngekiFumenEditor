using Gemini.Framework.Commands;

namespace OngekiFumenEditor.Modules.FumenVisualEditorSettings.Commands
{
    [CommandDefinition]
    public class ViewFumenVisualEditorSettingsCommandDefinition : CommandDefinition
    {
        public const string CommandName = "View.FumenVisualEditorSettings";

        public override string Name
        {
            get { return CommandName; }
        }

        public override string Text
        {
            get { return "编辑器设置"; }
        }

        public override string ToolTip
        {
            get { return Text; }
        }
    }
}