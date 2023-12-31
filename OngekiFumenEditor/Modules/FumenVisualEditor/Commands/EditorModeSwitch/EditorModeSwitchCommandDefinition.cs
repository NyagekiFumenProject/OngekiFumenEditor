using Gemini.Framework.Commands;
using OngekiFumenEditor.Properties;
using System;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Commands.EditorModeSwitch
{
	[CommandDefinition]
	public class EditorModeSwitchCommandDefinition : CommandDefinition
	{
		public override string Name => "Toolbar.EditorModeSwitch";

		public override string Text => Resource.EditorModeSwitch;

		public override string ToolTip => Resource.EditorModeSwitch;

		public override Uri IconSource => new Uri("pack://application:,,,/OngekiFumenEditor;component/Resources/Icons/preview.png");

		//[Export]
		//public static CommandKeyboardShortcut KeyGesture = new CommandKeyboardShortcut<BrushModeSwitchCommandDefinition>(new (Key.Q, ModifierKeys.Alt));
	}
}
