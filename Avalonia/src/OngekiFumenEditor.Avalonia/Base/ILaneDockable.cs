using OngekiFumenEditor.Avalonia.Base.OngekiObjects.Lane.Base;

namespace OngekiFumenEditor.Avalonia.Base
{
	public interface ILaneDockable : IHorizonPositionObject, ITimelineObject
	{
		LaneStartBase ReferenceLaneStart { get; set; }
		public int ReferenceLaneStrId { get; }
	}
}

