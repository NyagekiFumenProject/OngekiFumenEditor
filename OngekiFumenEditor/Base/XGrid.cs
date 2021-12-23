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

        public XGrid(float unit = default, int grid = default, uint resX = DEFAULT_RES_X) : base(unit, grid) => ResX = resX;

        public override string Serialize(OngekiFumen fumenData)
        {
            return Unit.ToString();
        }

        public override string ToString() => Serialize(default);

        public static XGrid operator +(XGrid l, GridOffset r)
        {
            var unit = l.Unit + r.Unit;
            var grid = r.Grid + l.Grid;

            unit += grid / l.ResX;
            grid = (int)(grid % l.ResX);

            return new XGrid(unit, grid);
        }
    }
}
