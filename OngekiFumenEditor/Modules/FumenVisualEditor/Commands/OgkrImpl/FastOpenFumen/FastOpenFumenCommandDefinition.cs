using Gemini.Framework.Commands;
using OngekiFumenEditor.Properties;
using System.ComponentModel.Composition;
using System.Windows.Input;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Commands.OgkrImpl.FastOpenFumen
{
	[CommandDefinition]
	public class FastOpenFumenCommandDefinition : CommandDefinition
	{
		public const string CommandName = "OngekiFumen.FastOpenFumen";

		public override string Name
		{
			get { return CommandName; }
		}

		public override string Text
		{
			get { return Resources.FastOpenFumen; }
		}

		public override string ToolTip
		{
			get { return Text; }
		}

		[Export]
		public static CommandKeyboardShortcut KeyGesture = new CommandKeyboardShortcut<FastOpenFumenCommandDefinition>(new(Key.F, ModifierKeys.Control));
	}
}