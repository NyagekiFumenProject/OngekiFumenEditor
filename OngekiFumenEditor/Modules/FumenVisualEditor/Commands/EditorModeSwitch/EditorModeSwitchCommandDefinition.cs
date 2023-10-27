using Gemini.Framework.Commands;
using System;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Commands.EditorModeSwitch
{
	[CommandDefinition]
	public class EditorModeSwitchCommandDefinition : CommandDefinition
	{
		public override string Name => "Toolbar.EditorModeSwitch";

		public override string Text => "编辑器预览模式";

		public override string ToolTip => "编辑器预览模式";

		public override Uri IconSource => new Uri("pack://application:,,,/OngekiFumenEditor;component/Resources/Icons/preview.png");

		//[Export]
		//public static CommandKeyboardShortcut KeyGesture = new CommandKeyboardShortcut<BrushModeSwitchCommandDefinition>(new (Key.Q, ModifierKeys.Alt));
	}
}
