using Gemini.Framework.Commands;
using OngekiFumenEditor.Properties;
using System;
using System.ComponentModel.Composition;
using System.Windows.Input;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Commands.ShowCurveControlAlways
{
	[CommandDefinition]
	public class ShowCurveControlAlwaysCommandDefinition : CommandDefinition
	{
		public override string Name => "Toolbar.ShowCurveControlAlways";

		public override string Text => Resources.CommandShowCurveControlAlways;

		public override string ToolTip => Resources.CommandShowCurveControlAlwaysTipText;

		public override Uri IconSource => new Uri("pack://application:,,,/OngekiFumenEditor;component/Resources/Icons/ease.png");

		[Export]
		public static CommandKeyboardShortcut KeyGesture = new CommandKeyboardShortcut<ShowCurveControlAlwaysCommandDefinition>(new(Key.S, ModifierKeys.Alt));
	}
}
