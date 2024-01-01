using Gemini.Framework.Commands;
using OngekiFumenEditor.Properties;
using System.ComponentModel.Composition;
using System.Windows.Input;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Commands.FastPlayPause
{
	[CommandDefinition]
	public class FastPlayPauseCommandDefinition : CommandDefinition
	{
		public override string Name => "Editor.FastPlayPause";

		public override string Text => Resources.FastPlayPause;

		public override string ToolTip => Text;

		[Export]
		public static CommandKeyboardShortcut KeyGesture = new CommandKeyboardShortcut<FastPlayPauseCommandDefinition>(new(Key.Space));
	}
}
