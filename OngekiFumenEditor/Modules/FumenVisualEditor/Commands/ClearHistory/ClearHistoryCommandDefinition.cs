using Gemini.Framework.Commands;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Commands.ClearHistory
{
    [CommandDefinition]
    public class ClearHistoryCommandDefinition : CommandDefinition
    {
        public override string Name => "Toolbar.ClearHistory";

        public override string Text => "清除历史记录";

        public override string ToolTip => "清除历史记录";

        public override Uri IconSource => new Uri("pack://application:,,,/OngekiFumenEditor;component/Resources/Icons/close.png");

        //[Export]
        //public static CommandKeyboardShortcut KeyGesture = new CommandKeyboardShortcut<ClearHistoryCommandDefinition>(new (Key.B, ModifierKeys.Alt));
    }
}
