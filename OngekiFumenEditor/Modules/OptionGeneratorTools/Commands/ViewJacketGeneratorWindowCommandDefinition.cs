using Gemini.Framework.Commands;

namespace OngekiFumenEditor.Modules.OptionGeneratorTools.Commands
{
    [CommandDefinition]
    public class ViewJacketGeneratorWindowCommandDefinition : CommandDefinition
    {
        public const string CommandName = "View.JacketGenerator";

        public override string Name
        {
            get { return CommandName; }
        }

        public override string Text
        {
            get { return "封面文件生成器"; }
        }

        public override string ToolTip
        {
            get { return Text; }
        }
    }
}