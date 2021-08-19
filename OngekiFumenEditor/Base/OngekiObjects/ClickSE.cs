using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Base.OngekiObjects
{
    public class ClickSE : IOngekiObject, ITimelineObject
    {
        public TGrid TGrid { get; set; }

        public string Group => "COMPOSITION";

        public string IDShortName => "CLK";

        public string Name => "ClickSE";

        public string Serialize(OngekiFumen fumenData)
        {
            return $"{IDShortName} {TGrid.Serialize(fumenData)}";
        }
    }
}
