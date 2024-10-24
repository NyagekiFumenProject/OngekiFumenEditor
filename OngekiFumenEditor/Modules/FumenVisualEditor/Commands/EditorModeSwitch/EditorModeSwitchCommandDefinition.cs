using Gemini.Framework.Commands;
using OngekiFumenEditor.Properties;
using System;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Commands.EditorModeSwitch
{
	[CommandDefinition]
	public class EditorModeSwitchCommandDefinition : CommandDefinition
	{
		public override string Name => "Toolbar.EditorModeSwitch";

		public override string Text => Resources.EditorModeSwitch;

		public override string ToolTip => Resources.EditorModeSwitch;

		public override Uri IconSource => new Uri("pack://application:,,,/OngekiFumenEditor;component/Resources/Icons/preview.png");
	}
}
