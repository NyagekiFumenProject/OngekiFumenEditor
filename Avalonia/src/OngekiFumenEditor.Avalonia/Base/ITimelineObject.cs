using System;

namespace OngekiFumenEditor.Avalonia.Base
{
	public interface ITimelineObject : IComparable<ITimelineObject>
	{
		public TGrid TGrid { get; set; }
    }
}

