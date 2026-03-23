using OngekiFumenEditor.Core.Base.OngekiObjects.Lane.Base;

namespace OngekiFumenEditor.Core.Base
{
	public interface ILaneDockable : IHorizonPositionObject, ITimelineObject
	{
		LaneStartBase ReferenceLaneStart { get; set; }
		public int ReferenceLaneStrId { get; }
	}
}
