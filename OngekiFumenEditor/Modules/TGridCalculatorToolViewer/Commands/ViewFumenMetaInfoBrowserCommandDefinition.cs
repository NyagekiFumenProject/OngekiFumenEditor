using Gemini.Framework.Commands;
using OngekiFumenEditor.Properties;
using System.ComponentModel.Composition;
using System.Windows.Input;

namespace OngekiFumenEditor.Modules.TGridCalculatorToolViewer.Commands
{
	[CommandDefinition]
	public class ViewTGridCalculatorToolViewerCommandDefinition : CommandDefinition
	{
		public const string CommandName = "View.TGridCalculatorToolViewer";

		public override string Name
		{
			get { return CommandName; }
		}

		public override string Text
		{
			get { return Resources.TGridCalculatorToolViewer; }
		}

		public override string ToolTip
		{
			get { return Text; }
		}

		[Export]
		public static CommandKeyboardShortcut KeyGesture = new CommandKeyboardShortcut<ViewTGridCalculatorToolViewerCommandDefinition>(new(Key.C, ModifierKeys.Alt | ModifierKeys.Shift));
	}
}