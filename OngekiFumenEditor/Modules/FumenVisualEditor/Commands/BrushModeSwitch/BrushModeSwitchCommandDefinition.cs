using Gemini.Framework.Commands;
using OngekiFumenEditor.Properties;
using System;
using System.ComponentModel.Composition;
using System.Windows.Input;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Commands.BrushModeSwitch
{
	[CommandDefinition]
	public class BrushModeSwitchCommandDefinition : CommandDefinition
	{
		public override string Name => "Toolbar.BrushModeSwitch";

		public override string Text => Resources.BrushModeSwitch;

		public override string ToolTip => Resources.BrushModeSwitchTipText;

		public override Uri IconSource => new Uri("pack://application:,,,/OngekiFumenEditor;component/Resources/Icons/icons8-paint-brush-16.png");

		[Export]
		public static CommandKeyboardShortcut KeyGesture = new CommandKeyboardShortcut<BrushModeSwitchCommandDefinition>(new(Key.B, ModifierKeys.Alt));
	}
}
