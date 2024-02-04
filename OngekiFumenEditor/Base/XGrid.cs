using System;

namespace OngekiFumenEditor.Base
{
	public class XGrid : GridBase
	{
		public const uint DEFAULT_RES_X = 4096;
		public uint ResX => DEFAULT_RES_X;

		public static XGrid Zero { get; private set; } = new XGrid();
		public static XGrid MaxValue { get; } = new XGrid((int.MaxValue - DEFAULT_RES_X) / DEFAULT_RES_X);
		public static XGrid MinValue { get; } = new XGrid(float.MinValue, int.MinValue);

		public XGrid(float unit = default, int grid = default) : base(unit, grid)
		{
			GridRadix = ResX;
		}

		public override string Serialize()
		{
			return Unit.ToString();
		}

		public override string ToString() => $"X[{Unit},{Grid}]";

		public static XGrid operator +(XGrid l, GridOffset r)
		{
			var unit = l.Unit + r.Unit;
			var grid = r.Grid + l.Grid;

			unit += grid / l.ResX;
			grid = (int)(grid % l.ResX);

			return new XGrid(unit, grid);
		}

		public XGrid CopyNew() => new XGrid(Unit, Grid);


        public static XGrid FromTotalUnit(float totalUnit)
        {
            var xGrid = new XGrid(totalUnit, 0);
            xGrid.NormalizeSelf();

            return xGrid;
        }

        public static XGrid FromTotalGrid(int totalGrid)
        {
            var xGrid = new XGrid(0, totalGrid);
            xGrid.NormalizeSelf();

            return xGrid;
        }
    }
}
