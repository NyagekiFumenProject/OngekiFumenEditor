using Gemini.Framework.Commands;
using System.ComponentModel.Composition;
using System.Windows.Input;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Commands.OgkrImpl.AppendEndLaneObject
{
    [CommandDefinition]
    public class AppendEndLaneObjectCommandDefinition : CommandDefinition
    {
        public const string CommandName = "OngekiFumen.AppendEndLaneObject";

        public override string Name
        {
            get { return CommandName; }
        }

        public override string Text
        {
            get { return "轨道补充中止物件"; }
        }

        public override string ToolTip
        {
            get { return Text; }
        }
    }
}