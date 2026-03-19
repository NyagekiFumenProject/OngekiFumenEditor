using Gemini.Framework.Commands;
using System.ComponentModel.Composition;
using System.Windows.Input;

namespace OngekiFumenEditor.Modules.OngekiGamePlayControllerViewer.Commands.QuickPlayPause
{
    [CommandDefinition]
    public class QuickPlayPauseCommandDefinition : CommandDefinition
    {
        public const string CommandName = "OngekiFumenEditor.QuickPlayPause";

        public override string Name
        {
            get { return CommandName; }
        }

        public override string Text
        {
            get { return "快速控制游戏暂停或播放"; }
        }

        public override string ToolTip
        {
            get { return Text; }
        }

        [Export]
        public static CommandKeyboardShortcut KeyGesture = new CommandKeyboardShortcut<QuickPlayPauseCommandDefinition>(new(Key.Space, ModifierKeys.Control));
    }
}