using System;

namespace OngekiFumenEditor.Core.Base
{
    public interface ITimelineObject : IComparable<ITimelineObject>
    {
        TGrid TGrid { get; set; }
    }
}
