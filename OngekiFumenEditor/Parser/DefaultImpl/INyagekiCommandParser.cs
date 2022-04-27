using OngekiFumenEditor.Base;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Parser.DefaultImpl
{
    public interface INyagekiCommandParser
    {
        string CommandName { get; }
        void ParseAndApply(OngekiFumen fumen, string[] seg);
    }
}
