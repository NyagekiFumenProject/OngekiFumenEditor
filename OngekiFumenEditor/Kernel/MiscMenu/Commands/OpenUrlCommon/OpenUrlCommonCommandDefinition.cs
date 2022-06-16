using Gemini.Framework.Commands;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Kernel.MiscMenu.Commands.OpenUrlCommon
{
    public abstract class OpenUrlCommonCommandDefinition : CommandDefinition
    {
        public abstract string NameOverride { get; }
        public abstract string Url { get; }
        public override string Name => $"OpenUrl.{NameOverride}";
        public override string ToolTip => Text;
    }

    [CommandDefinition]
    public class OpenProjectUrlCommandDefinition : OpenUrlCommonCommandDefinition
    {
        public override string NameOverride => "OpenProjectUrl";
        public override string Text => "项目网址";
        public override string Url => "https://github.com/NyagekiFumenProject/OngekiFumenEditor";
    }
}
