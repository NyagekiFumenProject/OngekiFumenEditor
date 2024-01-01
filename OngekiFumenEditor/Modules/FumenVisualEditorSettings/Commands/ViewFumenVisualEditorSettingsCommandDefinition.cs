using Gemini.Framework.Commands;
using OngekiFumenEditor.Properties;
using System.ComponentModel.Composition;
using System.Windows.Input;

namespace OngekiFumenEditor.Modules.FumenVisualEditorSettings.Commands
{
	[CommandDefinition]
	public class ViewFumenVisualEditorSettingsCommandDefinition : CommandDefinition
	{
		public const string CommandName = "View.FumenVisualEditorSettings";

		public override string Name
		{
			get { return CommandName; }
		}

		public override string Text
		{
			get { return Resources.FumenVisualEditorSettings; }
		}

		public override string ToolTip
		{
			get { return Text; }
		}

		[Export]
		public static CommandKeyboardShortcut KeyGesture = new CommandKeyboardShortcut<ViewFumenVisualEditorSettingsCommandDefinition>(new(Key.E, ModifierKeys.Alt | ModifierKeys.Shift));
	}
}