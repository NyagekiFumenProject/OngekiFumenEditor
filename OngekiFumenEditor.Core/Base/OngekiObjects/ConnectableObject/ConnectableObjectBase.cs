using OngekiFumenEditor.Core.Base.Attributes;

namespace OngekiFumenEditor.Core.Base.OngekiObjects.ConnectableObject
{
    public abstract class ConnectableObjectBase : OngekiMovableObjectBase
    {
        [LocalizableObjectPropertyBrowserAlias("RecordId")]
        [ObjectPropertyBrowserReadOnly]
        public abstract int RecordId { get; set; }

        public abstract LaneType LaneType { get; }

        public bool IsWallLane => LaneType == LaneType.WallLeft || LaneType.WallRight == LaneType;

        public bool IsDockableLane => LaneType switch
        {
            LaneType.Right or LaneType.Center or LaneType.Left => true,
            LaneType.WallLeft or LaneType.WallRight => true,
            _ => false
        };

        public override string ToString() => $"{base.ToString()} RID[{RecordId}]";

        public override void Copy(OngekiObjectBase fromObj)
        {
            base.Copy(fromObj);

            if (fromObj is not ConnectableObjectBase from)
                return;

            RecordId = from.RecordId;
        }

        public ConnectableChildObjectBase NextObject { get; set; }

        public abstract ConnectableStartObject ReferenceStartObject { get; }
    }
}
