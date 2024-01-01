using Gemini.Framework.Commands;
using OngekiFumenEditor.Properties;
using System.ComponentModel.Composition;
using System.Windows.Input;

namespace OngekiFumenEditor.Modules.FumenTimeSignatureListViewer.Commands
{
	[CommandDefinition]
	public class ViewFumenTimeSignatureListViewerCommandDefinition : CommandDefinition
	{
		public const string CommandName = "View.FumenTimeSignatureListViewer";

		public override string Name
		{
			get { return CommandName; }
		}

		public override string Text
		{
			get { return Resources.FumenTimeSignatureListViewer; }
		}

		public override string ToolTip
		{
			get { return Text; }
		}

		[Export]
		public static CommandKeyboardShortcut KeyGesture = new CommandKeyboardShortcut<ViewFumenTimeSignatureListViewerCommandDefinition>(new(Key.T, ModifierKeys.Alt | ModifierKeys.Shift));
	}
}