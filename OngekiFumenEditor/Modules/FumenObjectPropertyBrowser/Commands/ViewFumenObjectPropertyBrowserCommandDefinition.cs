using Gemini.Framework.Commands;
using OngekiFumenEditor.Properties;
using System.ComponentModel.Composition;
using System.Windows.Input;

namespace OngekiFumenEditor.Modules.FumenObjectPropertyBrowser.Commands
{
	[CommandDefinition]
	public class ViewFumenObjectPropertyBrowserCommandDefinition : CommandDefinition
	{
		public const string CommandName = "View.FumenObjectPropertyBrowser";

		public override string Name
		{
			get { return CommandName; }
		}

		public override string Text
		{
			get { return Resources.FumenObjectPropertyBrowser; }
		}

		public override string ToolTip
		{
			get { return Text; }
		}

		[Export]
		public static CommandKeyboardShortcut KeyGesture = new CommandKeyboardShortcut<ViewFumenObjectPropertyBrowserCommandDefinition>(new(Key.O, ModifierKeys.Alt | ModifierKeys.Shift));
	}
}