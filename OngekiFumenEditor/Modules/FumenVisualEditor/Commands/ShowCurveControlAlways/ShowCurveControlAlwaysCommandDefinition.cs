using Gemini.Framework.Commands;
using System;
using System.ComponentModel.Composition;
using System.Windows.Input;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Commands.ShowCurveControlAlways
{
	[CommandDefinition]
	public class ShowCurveControlAlwaysCommandDefinition : CommandDefinition
	{
		public override string Name => "Toolbar.ShowCurveControlAlways";

		public override string Text => "一直显示曲线控制物件";

		public override string ToolTip => "如果开启，将一直显示所有曲线控制物件";

		public override Uri IconSource => new Uri("pack://application:,,,/OngekiFumenEditor;component/Resources/Icons/ease.png");

		[Export]
		public static CommandKeyboardShortcut KeyGesture = new CommandKeyboardShortcut<ShowCurveControlAlwaysCommandDefinition>(new(Key.S, ModifierKeys.Alt));
	}
}
