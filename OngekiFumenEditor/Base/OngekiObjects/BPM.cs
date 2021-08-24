using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Base.OngekiObjects
{
    public class BPM : IOngekiObject, ITimelineObject
    {
        public TGrid TGrid { get; set; } = new TGrid();
        public double Value { get; set; }

        public string Group => "COMPOSITION";
        public string IDShortName => "BPM";
        public string Name => "BPM";

        public int CompareTo(object obj)
        {
            return TGrid.CompareTo((obj as ITimelineObject)?.TGrid);
        }

        public string Serialize(OngekiFumen fumenData)
        {
            return $"{IDShortName} {TGrid.Serialize(fumenData)} {Value}";
        }

        public override string ToString() => Serialize(default);
    }
}
