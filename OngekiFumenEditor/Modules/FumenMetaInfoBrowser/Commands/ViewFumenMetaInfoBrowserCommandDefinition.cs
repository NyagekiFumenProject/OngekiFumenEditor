using Gemini.Framework.Commands;
using OngekiFumenEditor.Properties;
using System.ComponentModel.Composition;
using System.Windows.Input;

namespace OngekiFumenEditor.Modules.FumenMetaInfoBrowser.Commands
{
	[CommandDefinition]
	public class ViewFumenMetaInfoBrowserCommandDefinition : CommandDefinition
	{
		public const string CommandName = "View.FumenMetaInfoBrowser";

		public override string Name
		{
			get { return CommandName; }
		}

		public override string Text
		{
			get { return Resources.FumenMetaInfoBrowser; }
		}

		public override string ToolTip
		{
			get { return Text; }
		}

		[Export]
		public static CommandKeyboardShortcut KeyGesture = new CommandKeyboardShortcut<ViewFumenMetaInfoBrowserCommandDefinition>(new(Key.M, ModifierKeys.Alt | ModifierKeys.Shift));
	}
}