using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Base.OngekiObjects
{
    public class MeterChange : IOngekiObject, ITimelineObject
    {
        public TGrid TGrid { get; set; }
        public int BunShi { get; set; }
        public int Bunbo { get; set; }

        public string Group => "COMPOSITION";
        public string IDShortName => "MET";
        public string Name => "MeterChange";

        public string Serialize(OngekiFumen fumenData)
        {
            return $"{IDShortName} {TGrid.Serialize(fumenData)} {BunShi} {Bunbo}";
        }
    }
}
