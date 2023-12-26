using Gemini.Framework.Commands;

namespace OngekiFumenEditor.Kernel.AssistHelper.Commands.GenerateAutoplayFaderData
{
    [CommandDefinition]
    public class GenerateAutoplayFaderDataCommandDefinition : CommandDefinition
    {
        public const string CommandName = "Assist.GenerateAutoplayFaderData";

        public override string Name
        {
            get { return CommandName; }
        }

        public override string Text
        {
            get { return "生成GenerateAutoplayFader数据文件"; }
        }

        public override string ToolTip
        {
            get { return Text; }
        }
    }
}