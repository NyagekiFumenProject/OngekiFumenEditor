using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Base.OngekiObjects
{
    public class BPM : IOngekiObject, ITimelineObject
    {
        public TGrid TGrid { get; set; }
        public float Value { get; set; }

        public string Group => "COMPOSITION";
        public string IDShortName => "BPM";
        public string Name => "BPM";

        public string Serialize(OngekiFumen fumenData)
        {
            return $"{IDShortName} {TGrid.Serialize(fumenData)} {Value}";
        }
    }
}
