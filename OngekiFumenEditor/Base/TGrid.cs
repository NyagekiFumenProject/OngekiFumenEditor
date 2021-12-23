using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Base
{
    public class TGrid : GridBase
    {
        public const uint DEFAULT_RES_T = 1920;
        public uint ResT { get; set; } = DEFAULT_RES_T;

        public override string Serialize(OngekiFumen fumenData)
        {
            return $"{Unit} {Grid}";
        }

        public override string ToString() => Serialize(default);

        public static bool operator <(TGrid l, TGrid r)
        {
            return l.Grid + l.Unit * l.ResT < r.Grid + r.Unit * r.ResT;
        }

        public static bool operator >(TGrid l, TGrid r)
        {
            return l.Grid + l.Unit * l.ResT > r.Grid + r.Unit * r.ResT;
        }

        public static bool operator <=(TGrid l, TGrid r)
        {
            return !(l > r);
        }

        public static bool operator >=(TGrid l, TGrid r)
        {
            return !(l < r);
        }
    }
}
