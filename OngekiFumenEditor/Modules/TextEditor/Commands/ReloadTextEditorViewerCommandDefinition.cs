using Gemini.Framework.Commands;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace OngekiFumenEditor.Modules.TextEditor.Commands
{
    [CommandDefinition]
    public class ReloadTextEditorViewerCommandDefinition : CommandDefinition
    {
        public override string Name => nameof(ReloadTextEditorViewerCommandDefinition);

        public override string Text => "重新加载 (_R)";

        public override string ToolTip => "放弃现有内容,重新加载文件内容";

        [Export]
        public static CommandKeyboardShortcut KeyGesture = new CommandKeyboardShortcut<ReloadTextEditorViewerCommandDefinition>(new KeyGesture(Key.R, ModifierKeys.Control));
    }
}
