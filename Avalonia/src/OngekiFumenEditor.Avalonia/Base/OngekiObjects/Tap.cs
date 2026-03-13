using OngekiFumenEditor.Avalonia.Base.Attributes;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects.Lane.Base;

namespace OngekiFumenEditor.Avalonia.Base.OngekiObjects
{
    public class Tap : OngekiMovableObjectBase, ILaneDockableChangable, ICriticalableObject
    {
        public bool IsWallTap => ReferenceLaneStart?.IsWallLane ?? false;

        public LaneType BelongLaneType => ReferenceLaneStart?.LaneType ?? LaneType.Undefined;

        private bool isCritical = false;
        public bool IsCritical
        {
            get { return isCritical; }
            set
            {
                isCritical = value;
                OnPropertyChanged(() => IDShortName);
                OnPropertyChanged(() => IsCritical);
            }
        }

        private LaneStartBase referenceLaneStart = default;
        public LaneStartBase ReferenceLaneStart
        {
            get { return referenceLaneStart; }
            set
            {
                referenceLaneStart = value;

                OnPropertyChanged(() => ReferenceLaneStart);
                OnPropertyChanged(() => ReferenceLaneStrId);
            }
        }

        [ObjectPropertyBrowserShow]
        [ObjectPropertyBrowserAlias("RefLaneId")]
        public int ReferenceLaneStrId => ReferenceLaneStart?.RecordId ?? -1;

        private int? referenceLaneStrIdManualSet = default;
        [ObjectPropertyBrowserShow]
        [ObjectPropertyBrowserTipText("ObjectLaneGroupId")]
        [ObjectPropertyBrowserAlias("SetRefLaneId")]
        public int? ReferenceLaneStrIdManualSet
        {
            get => referenceLaneStrIdManualSet;
            set
            {
                referenceLaneStrIdManualSet = value;
                OnPropertyChanged(() => ReferenceLaneStrIdManualSet);
                referenceLaneStrIdManualSet = default;
            }
        }

        public override string IDShortName => this switch
        {
            { IsCritical: true } => "CTP",
            { IsCritical: false } => "TAP",
        };

        public override void Copy(OngekiObjectBase fromObj)
        {
            base.Copy(fromObj);

            if (fromObj is not Tap from)
                return;

            IsCritical = from.IsCritical;
            ReferenceLaneStart = from.ReferenceLaneStart;
        }

        public override string ToString()
        {
            return base.ToString() + $" LaneType[{BelongLaneType}]";
        }
    }
}
