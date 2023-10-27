using Gemini.Framework.Commands;
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
			get { return "音频播放"; }
		}

		public override string ToolTip
		{
			get { return Text; }
		}

		[Export]
		public static CommandKeyboardShortcut KeyGesture = new CommandKeyboardShortcut<ViewAudioPlayerToolViewerCommandDefinition>(new(Key.A, ModifierKeys.Alt | ModifierKeys.Shift));
	}
}