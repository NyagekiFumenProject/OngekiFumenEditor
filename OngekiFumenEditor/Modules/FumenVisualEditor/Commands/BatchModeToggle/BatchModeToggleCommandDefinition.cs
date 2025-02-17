using System;
using System.ComponentModel.Composition;
using System.Windows.Input;
using Gemini.Framework.Commands;
using OngekiFumenEditor.Properties;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Commands.BatchModeToggle
{
	[CommandDefinition]
	public class BatchModeToggleCommandDefinition : CommandDefinition
	{
		public override string Name => "Toolbar.BatchModeToggle";

		public override string Text => Resources.BatchModeToggle;

		public override string ToolTip => Resources.BatchModeToggleTipText;

		public override Uri IconSource => new Uri("pack://application:,,,/OngekiFumenEditor;component/Resources/Icons/icons8-paint-brush-16.png");
	}
}
