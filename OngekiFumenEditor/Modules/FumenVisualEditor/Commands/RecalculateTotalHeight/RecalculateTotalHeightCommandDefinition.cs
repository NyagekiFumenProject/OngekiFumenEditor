using Gemini.Framework.Commands;
using OngekiFumenEditor.Properties;
using System;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Commands.RecalculateTotalHeight
{
	[CommandDefinition]
	public class RecalculateTotalHeightCommandDefinition : CommandDefinition
	{
		public override string Name => "Toolbar.RecalculateTotalHeight";

		public override string Text => Resources.RecalculateTotalHeight;

		public override string ToolTip => Resources.RecalculateTotalHeight;

		//[Export]
		//public static CommandKeyboardShortcut KeyGesture = new CommandKeyboardShortcut<ClearHistoryCommandDefinition>(new (Key.B, ModifierKeys.Alt));
	}
}
