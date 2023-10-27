using Gemini.Framework.Commands;
using System.ComponentModel.Composition;
using System.Windows.Input;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Commands.FastPlayPause
{
	[CommandDefinition]
	public class FastPlayPauseCommandDefinition : CommandDefinition
	{
		public override string Name => "Editor.FastPlayPause";

		public override string Text => "编辑器快速播放暂停";

		public override string ToolTip => Text;

		[Export]
		public static CommandKeyboardShortcut KeyGesture = new CommandKeyboardShortcut<FastPlayPauseCommandDefinition>(new(Key.Space));
	}
}
