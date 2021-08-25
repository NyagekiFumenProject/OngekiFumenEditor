using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Base.OngekiObjects
{
    public class MeterChange : OngekiTimelineObjectBase
    {
        public int BunShi { get; set; }
        public int Bunbo { get; set; }

        public static string CommandName => "MET";
        public override string IDShortName => CommandName;

        public override string Serialize(OngekiFumen fumenData)
        {
            return $"{IDShortName} {TGrid.Serialize(fumenData)} {BunShi} {Bunbo}";
        }
    }
}
