using OngekiFumenEditor.Core.Base.Attributes;
using OngekiFumenEditor.Core.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Core.Base.OngekiObjects.Lane.Base;
using OngekiFumenEditor.Core.Utils;
using System;

namespace OngekiFumenEditor.Core.Base.OngekiObjects
{
    public class HoldEnd : OngekiMovableObjectBase, ILaneDockable
    {
        public override string IDShortName => "[HoldEnd]";

        private Hold refHold;
        public Hold RefHold
        {
            get => refHold;
            set => Set(ref refHold, value);
        }

        [ObjectPropertyBrowserHide]
        public LaneStartBase ReferenceLaneStart
        {
            get => RefHold?.ReferenceLaneStart;
            set { }
        }

        [ObjectPropertyBrowserHide]
        public int ReferenceLaneStrId => ReferenceLaneStart?.RecordId ?? -1;

        public int? CacheRecoveryHoldObjectID { get; set; } = null;

        public void RedockXGrid()
        {
            if (ReferenceLaneStart is LaneStartBase refLane)
            {
                if (refLane.CalulateXGrid(TGrid) is XGrid xGrid)
                    XGrid = xGrid;
            }
        }
    }
}

