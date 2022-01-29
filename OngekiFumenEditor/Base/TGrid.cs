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
        public uint ResT
        {
            get
            {
                return gridBaseRadix;
            }
            set
            {
                gridBaseRadix = value;
            }
        }

        public static TGrid ZeroDefault { get; } = new TGrid();

        public TGrid(float unit = default, int grid = default, uint resT = DEFAULT_RES_T) : base(unit, grid) => ResT = resT;

        public override string Serialize()
        {
            return $"{Unit} {Grid}";
        }

        public override string ToString() => $"T[{Unit},{Grid}]";

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

        public static TGrid operator +(TGrid l, GridOffset r)
        {
            var unit = l.Unit + r.Unit;
            var grid = r.Grid + l.Grid;

            while (grid < 0)
            {
                unit = unit - 1;
                grid = (int)(grid + l.ResT);
            }

            unit += grid / l.ResT;
            grid = (int)(grid % l.ResT);

            return new TGrid(unit, grid);
        }
    }
}
