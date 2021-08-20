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
    }
}
