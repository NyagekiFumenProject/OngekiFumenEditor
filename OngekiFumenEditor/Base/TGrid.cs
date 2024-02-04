using System;

namespace OngekiFumenEditor.Base
{
    public class TGrid : GridBase
    {
        public const uint DEFAULT_RES_T = 1920;
        public uint ResT => DEFAULT_RES_T;

        public static TGrid Zero { get; } = new TGrid();
        public static TGrid MaxValue { get; } = new TGrid((int.MaxValue - DEFAULT_RES_T) / DEFAULT_RES_T, (int)DEFAULT_RES_T);
        public static TGrid MinValue { get; } = new TGrid(float.MinValue, int.MinValue);

        public TGrid(float unit = default, int grid = default) : base(unit, grid)
        {
            GridRadix = ResT;
        }

        public override string Serialize()
        {
            return $"{Unit}\t{Grid}";
        }

        public override string ToString() => $"T[{Unit},{Grid}]";

        public TGrid CopyNew() => new(Unit, Grid);

        public static TGrid FromTotalUnit(float totalUnit)
        {
            var tGrid = new TGrid(totalUnit, 0);
            tGrid.NormalizeSelf();

            return tGrid;
        }

        public static TGrid FromTotalGrid(int totalGrid)
        {
            var tGrid = new TGrid(0, totalGrid);
            tGrid.NormalizeSelf();

            return tGrid;
        }

        public static bool operator <(TGrid l, TGrid r)
        {
            return l.CompareTo(r) < 0;
        }

        public static bool operator >(TGrid l, TGrid r)
        {
            return l.CompareTo(r) > 0;
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

        public static TGrid operator -(TGrid l, GridOffset r)
        {
            var lGrids = l.TotalGrid;
            var rGrids = r.TotalGrid(l.GridRadix);

            var grid = lGrids - rGrids;
            if (grid < 0)
                return null;

            var t = new TGrid(0, grid);
            t.NormalizeSelf();

            return t;
        }
    }
}
