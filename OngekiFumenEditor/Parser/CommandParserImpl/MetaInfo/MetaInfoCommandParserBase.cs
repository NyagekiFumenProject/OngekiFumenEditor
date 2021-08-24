using OngekiFumenEditor.Base;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Parser.CommandParserImpl.MetaInfo
{
    public abstract class MetaInfoCommandParserBase : ICommandParser
    {
        public abstract string CommandLineHeader { get; }

        public abstract void ParseMetaInfo(string line, OngekiFumen fumen);

        public IOngekiObject Parse(string line, OngekiFumen fumen)
        {
            ParseMetaInfo(line, fumen);
            return null;
        }
    }
}
