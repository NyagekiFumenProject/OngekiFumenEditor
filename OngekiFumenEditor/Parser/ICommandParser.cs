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
        public IOngekiObject Parse(string line,OngekiFumen fumen);
        public void AfterParse(IOngekiObject obj, OngekiFumen fumen) { }
    }
}
