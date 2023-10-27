using Gemini.Framework.Commands;
using System.ComponentModel.Composition;
using System.Windows.Input;

namespace OngekiFumenEditor.Modules.FumenEditorSelectingObjectViewer.Commands
{
	[CommandDefinition]
	public class ViewFumenEditorSelectingObjectViewerCommandDefinition : CommandDefinition
	{
		public const string CommandName = "View.FumenEditorSelectingObjectViewer";

		public override string Name
		{
			get { return CommandName; }
		}

		public override string Text
		{
			get { return "当前选择物件查看器"; }
		}

		public override string ToolTip
		{
			get { return Text; }
		}

		[Export]
		public static CommandKeyboardShortcut KeyGesture = new CommandKeyboardShortcut<ViewFumenEditorSelectingObjectViewerCommandDefinition>(new(Key.S, ModifierKeys.Alt | ModifierKeys.Shift));
	}
}