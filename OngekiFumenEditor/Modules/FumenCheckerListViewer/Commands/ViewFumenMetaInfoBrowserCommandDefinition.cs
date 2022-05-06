using Gemini.Framework.Commands;
using System.ComponentModel.Composition;
using System.Windows.Input;

namespace OngekiFumenEditor.Modules.FumenCheckerListViewer.Commands
{
    [CommandDefinition]
    public class ViewFumenCheckerListViewerCommandDefinition : CommandDefinition
    {
        public const string CommandName = "View.FumenCheckerListViewer";

        public override string Name
        {
            get { return CommandName; }
        }

        public override string Text
        {
            get { return "谱面检查器"; }
        }

        public override string ToolTip
        {
            get { return Text; }
        }

        [Export]
        public static CommandKeyboardShortcut KeyGesture = new CommandKeyboardShortcut<ViewFumenCheckerListViewerCommandDefinition>(new(Key.C, ModifierKeys.Alt | ModifierKeys.Shift));
    }
}