using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Base.OngekiObjects
{
    public class BPMChange : OngekiTimelineObjectBase
    {
        public double Value { get; set; }

        public static string CommandName => "BPM";
        public override string IDShortName => CommandName;

        public override string Serialize(OngekiFumen fumenData)
        {
            return $"{IDShortName} {TGrid.Serialize(fumenData)} {Value}";
        }

        public override string ToString() => Serialize(default);
    }
}
