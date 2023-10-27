using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using System.Collections.Generic;
using System.Linq;

namespace OngekiFumenEditor.Kernel.AssistHelper.Impls
{
	public static class AdjustDockablesHorizonPositionHelper
	{
		private static XGrid CalculateConnectableObjectXGrid(ConnectableStartObject startObject, TGrid tGrid)
		{
			if (tGrid < startObject.TGrid)
				return default;

			return startObject.CalulateXGrid(tGrid);
		}

		public static void Execute(OngekiFumen fumen)
		{
			void execute<T>(IEnumerable<T> objs) where T : IHorizonPositionObject, ITimelineObject, ILaneDockable
			{
				foreach (var o in objs)
				{
					if (CalculateConnectableObjectXGrid(o.ReferenceLaneStart, o.TGrid) is XGrid xGrid)
					{
						o.XGrid = xGrid;
					}
					else
					{
						//todo report error.
					}
				}
			}

			execute(fumen.Taps.Where(x => x.ReferenceLaneStart is not null));
			execute(fumen.Holds.Where(x => x.ReferenceLaneStart is not null));
		}
	}
}
