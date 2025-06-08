using Gemini.Framework.Commands;
using OngekiFumenEditor.Properties;
using System.ComponentModel.Composition;
using System.Windows.Input;

namespace OngekiFumenEditor.Modules.FumenSoflanGroupListViewer.Commands
{
	[CommandDefinition]
	public class FumenSoflanGroupListViewerCommandDefinition : CommandDefinition
	{
		public const string CommandName = "View.FumenSoflanGroupListViewer";

		public override string Name
		{
			get { return CommandName; }
		}

		public override string Text
		{
			get { return "变速分组查看器"; }
		}

		public override string ToolTip
		{
			get { return Text; }
		}

		[Export]
		public static CommandKeyboardShortcut KeyGesture = new CommandKeyboardShortcut<FumenSoflanGroupListViewerCommandDefinition>(new(Key.S, ModifierKeys.Alt | ModifierKeys.Shift));
	}
}