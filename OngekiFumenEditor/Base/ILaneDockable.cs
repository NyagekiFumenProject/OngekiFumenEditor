using OngekiFumenEditor.Base.OngekiObjects.Lane.Base;

namespace OngekiFumenEditor.Base
{
	public interface ILaneDockable : IHorizonPositionObject, ITimelineObject
	{
		LaneStartBase ReferenceLaneStart { get; set; }
		public int ReferenceLaneStrId { get; }
	}
}
