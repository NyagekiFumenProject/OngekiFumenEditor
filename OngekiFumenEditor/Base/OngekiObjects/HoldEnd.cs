using OngekiFumenEditor.Base.Attributes;
using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Base.OngekiObjects.Lane.Base;
using OngekiFumenEditor.Kernel.CurveInterpolater;
using System.Runtime.InteropServices;

namespace OngekiFumenEditor.Base.OngekiObjects
{
    public class HoldEnd : OngekiMovableObjectBase, ILaneDockable
    {
        public override string IDShortName => "[HoldEnd]";

        private Hold refHold;
        public Hold RefHold
        {
            get => refHold;
            internal set => Set(ref refHold, value);
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
    }
}
