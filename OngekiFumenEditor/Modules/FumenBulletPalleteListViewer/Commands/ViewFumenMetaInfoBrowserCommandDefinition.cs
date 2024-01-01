using Gemini.Framework.Commands;
using OngekiFumenEditor.Properties;
using System.ComponentModel.Composition;
using System.Windows.Input;

namespace OngekiFumenEditor.Modules.FumenBulletPalleteListViewer.Commands
{
	[CommandDefinition]
	public class ViewFumenBulletPalleteListViewerCommandDefinition : CommandDefinition
	{
		public const string CommandName = "View.FumenBulletPalleteListViewer";

		public override string Name
		{
			get { return CommandName; }
		}

		public override string Text
		{
			get { return Resources.FumenBulletPalleteListViewer; }
		}

		public override string ToolTip
		{
			get { return Text; }
		}

		[Export]
		public static CommandKeyboardShortcut KeyGesture = new CommandKeyboardShortcut<ViewFumenBulletPalleteListViewerCommandDefinition>(new(Key.B, ModifierKeys.Alt | ModifierKeys.Shift));
	}
}