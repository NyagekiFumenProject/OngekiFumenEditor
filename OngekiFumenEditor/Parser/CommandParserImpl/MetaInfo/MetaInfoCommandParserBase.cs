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

        public abstract void ParseMetaInfo(CommandArgs args, OngekiFumen fumen);

        public OngekiObjectBase Parse(CommandArgs args, OngekiFumen fumen)
        {
            ParseMetaInfo(args, fumen);
            return null;
        }
    }
}
