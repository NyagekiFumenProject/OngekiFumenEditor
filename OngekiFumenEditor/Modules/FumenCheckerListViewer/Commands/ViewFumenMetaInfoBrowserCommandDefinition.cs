using Gemini.Framework.Commands;
using OngekiFumenEditor.Properties;
using System.ComponentModel.Composition;
using System.Windows.Input;

namespace OngekiFumenEditor.Modules.FumenCheckerListViewer.Commands
{
	[CommandDefinition]
	public class ViewFumenCheckerListViewerCommandDefinition : CommandDefinition
	{
		public const string CommandName = "View.FumenCheckerListViewer";

		public override string Name
		{
			get { return CommandName; }
		}

		public override string Text
		{
			get { return Resources.FumenCheckerListViewer; }
		}

		public override string ToolTip
		{
			get { return Text; }
		}

		[Export]
		public static CommandKeyboardShortcut KeyGesture = new CommandKeyboardShortcut<ViewFumenCheckerListViewerCommandDefinition>(new(Key.C, ModifierKeys.Alt | ModifierKeys.Shift));
	}
}