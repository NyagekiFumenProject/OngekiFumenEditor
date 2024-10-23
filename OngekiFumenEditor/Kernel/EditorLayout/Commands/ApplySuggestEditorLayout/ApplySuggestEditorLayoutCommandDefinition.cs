using Gemini.Framework.Commands;
using OngekiFumenEditor.Properties;

namespace OngekiFumenEditor.Kernel.EditorLayout.Commands.About
{
	[CommandDefinition]
	public class ApplySuggestEditorLayoutCommandDefinition : CommandDefinition
	{
		public const string CommandName = "EditorLayout.ApplySuggestEditorLayout";

		public override string Name
		{
			get { return CommandName; }
		}

		public override string Text
		{
			get { return Resources.ApplySuggestedEditorLayout; }
		}

		public override string ToolTip
		{
			get { return Text; }
		}
	}
}