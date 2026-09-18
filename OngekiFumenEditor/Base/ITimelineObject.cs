using System;

namespace OngekiFumenEditor.Base
{
    public interface ITimelineObject : IComparable<ITimelineObject>
    {
        TGrid TGrid { get; set; }
    }
}
