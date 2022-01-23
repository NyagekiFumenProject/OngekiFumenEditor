using OngekiFumenEditor.Base;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Parser
{
    public abstract class CommandParserBase : ICommandParser
    {
        public abstract string CommandLineHeader { get; }

        public virtual void AfterParse(OngekiObjectBase obj, OngekiFumen fumen) { }

        public abstract OngekiObjectBase Parse(CommandArgs args, OngekiFumen fumen);
    }
}
