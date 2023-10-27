using System;

namespace OngekiFumenEditor.Base
{
	public interface ITimelineObject : IComparable<ITimelineObject>
	{
		public TGrid TGrid { get; set; }
	}
}
