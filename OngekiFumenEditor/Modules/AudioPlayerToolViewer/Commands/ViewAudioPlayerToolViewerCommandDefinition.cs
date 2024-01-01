using Gemini.Framework.Commands;
using OngekiFumenEditor.Properties;
using System.ComponentModel.Composition;
using System.Windows.Input;

namespace OngekiFumenEditor.Modules.AudioPlayerToolViewer.Commands
{
	[CommandDefinition]
	public class ViewAudioPlayerToolViewerCommandDefinition : CommandDefinition
	{
		public const string CommandName = "View.AudioPlayerToolViewer";

		public override string Name
		{
			get { return CommandName; }
		}

		public override string Text
		{
			get { return Resources.AudioPlayerToolViewer; }
		}

		public override string ToolTip
		{
			get { return Text; }
		}

		[Export]
		public static CommandKeyboardShortcut KeyGesture = new CommandKeyboardShortcut<ViewAudioPlayerToolViewerCommandDefinition>(new(Key.A, ModifierKeys.Alt | ModifierKeys.Shift));
	}
}