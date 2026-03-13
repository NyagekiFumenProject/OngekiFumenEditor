using OngekiFumenEditor.Avalonia.Base.Attributes;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects.Lane.Base;

namespace OngekiFumenEditor.Avalonia.Base.OngekiObjects
{
    public class HoldEnd : OngekiMovableObjectBase, ILaneDockable
    {
        public override string IDShortName => "[HoldEnd]";

        private Hold refHold;
        public Hold RefHold
        {
            get => refHold;
            internal set => SetProperty(ref refHold, value);
        }

        [ObjectPropertyBrowserHide]
        public LaneStartBase ReferenceLaneStart
        {
            get => RefHold?.ReferenceLaneStart;
            set { }
        }

        [ObjectPropertyBrowserHide]
        public int ReferenceLaneStrId => ReferenceLaneStart?.RecordId ?? -1;

        internal int? CacheRecoveryHoldObjectID { get; set; } = null;

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
