using Gemini.Framework.Commands;
using OngekiFumenEditor.Properties;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Commands.OgkrImpl.FumenStatistics
{
    [CommandDefinition]
    public class FumenStatisticsCommandDefinition : CommandDefinition
    {
        public const string CommandName = "OngekiFumen.FumenStatistics";

        public override string Name => CommandName;
        public override string Text => Resources.FumenStatistics;
        public override string ToolTip => Resources.FumenStatisticsToolTip;
    }
}
