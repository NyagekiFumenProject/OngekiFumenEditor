using Gemini.Framework.Commands;
using OngekiFumenEditor.Properties;
using System;

namespace OngekiFumenEditor.Modules.PreviewSvgGenerator.Commands.GenerateSvg
{
	[CommandDefinition]
	public class GenerateSvgCommandDefinition : CommandDefinition
	{
		public override string Name => "Toolbar.GenerateSvg";

		public override string Text => Resources.GenerateSvg;

		public override string ToolTip => Resources.GenerateSvg;
	}
}
