using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Base
{
    public class XGrid : GridBase
    {
        public const uint DEFAULT_RES_X = 4096;
        public uint ResX { get; set; } = DEFAULT_RES_X;

        public override string Serialize(OngekiFumen fumenData)
        {
            return Unit.ToString();
        }
    }
}
