using Gemini.Framework.Commands;
using OngekiFumenEditor.Properties;
using System.ComponentModel.Composition;
using System.Windows.Input;

namespace OngekiFumenEditor.Modules.FumenEditorRenderControlViewer.Commands
{
	[CommandDefinition]
	public class FumenEditorRenderControlViewerCommandDefinition : CommandDefinition
	{
		public const string CommandName = "View.FumenEditorRenderControlViewer";

		public override string Name
		{
			get { return CommandName; }
		}

		public override string Text
		{
			get { return Resources.FumenEditorRenderControlViewer; }
		}

		public override string ToolTip
		{
			get { return Text; }
		}

		[Export]
		public static CommandKeyboardShortcut KeyGesture = new CommandKeyboardShortcut<FumenEditorRenderControlViewerCommandDefinition>(new(Key.R, ModifierKeys.Alt | ModifierKeys.Shift));
	}
}