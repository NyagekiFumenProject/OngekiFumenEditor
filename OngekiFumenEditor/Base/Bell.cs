using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Base
{
    public class Bell : IOgenkiObject
    {
        public TGrid TGrid { get; set; }
        public XGrid XGrid { get; set; }

        public string Group => "BELL";
        public string Name => "BEL";
    }
}
