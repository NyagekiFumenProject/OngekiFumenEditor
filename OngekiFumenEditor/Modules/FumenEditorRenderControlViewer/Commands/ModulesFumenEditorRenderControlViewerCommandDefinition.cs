using Gemini.Framework.Commands;
using System.ComponentModel.Composition;
using System.Windows.Input;

namespace OngekiFumenEditor.Modules.FumenEditorRenderControlViewer.Commands
{
    [CommandDefinition]
    public class FumenEditorRenderControlViewerCommandDefinition : CommandDefinition
    {
        public const string CommandName = "View.FumenEditorRenderControlViewer";

        public override string Name
        {
            get { return CommandName; }
        }

        public override string Text
        {
            get { return "谱面编辑器渲染控制"; }
        }

        public override string ToolTip
        {
            get { return Text; }
        }

        [Export]
        public static CommandKeyboardShortcut KeyGesture = new CommandKeyboardShortcut<FumenEditorRenderControlViewerCommandDefinition>(new(Key.R, ModifierKeys.Alt | ModifierKeys.Shift));
    }
}