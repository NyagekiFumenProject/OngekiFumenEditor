using OngekiFumenEditor.Base;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Parser
{
    public interface ICommandParser
    {
        public string CommandLineHeader { get; }
        public OngekiObjectBase Parse(CommandArgs args,OngekiFumen fumen);
        public void AfterParse(OngekiObjectBase obj, OngekiFumen fumen);
    }
}
