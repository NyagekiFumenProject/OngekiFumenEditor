using OngekiFumenEditor.Base;

namespace OngekiFumenEditor.Kernel.CurveInterpolater
{
	public struct CurvePoint : ITimelineObject, IHorizonPositionObject
	{
		public CurvePoint(TGrid t, XGrid x)
		{
			TGrid = t;
			XGrid = x;
		}

		public TGrid TGrid { get; set; }
		public XGrid XGrid { get; set; }

		public int CompareTo(ITimelineObject obj)
		{
			return TGrid.CompareTo(obj.TGrid);
		}

		public static explicit operator CurvePoint(OngekiMovableObjectBase e)
		{
			return new CurvePoint(e.TGrid, e.XGrid);
		}

		public override string ToString() => $"{XGrid} {TGrid}";
	}
}
