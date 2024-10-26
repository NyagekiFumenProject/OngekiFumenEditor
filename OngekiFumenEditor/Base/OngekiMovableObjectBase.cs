using OngekiFumenEditor.Base.Attributes;
using OngekiFumenEditor.Properties;
using OngekiFumenEditor.Utils;

namespace OngekiFumenEditor.Base
{
    public abstract class OngekiMovableObjectBase : OngekiTimelineObjectBase, IHorizonPositionObject
    {
        private XGrid xGrid = new XGrid();
        [ObjectPropertyBrowserTipText("ObjectXGrid")]
        public virtual XGrid XGrid
        {
            get { return xGrid; }
            set
            {
                this.RegisterOrUnregisterPropertyChangeEvent(xGrid, value);
                xGrid = value;
                NotifyOfPropertyChange(() => XGrid);
            }
        }

        public bool Clashes(OngekiMovableObjectBase other)
        {
            return base.Clashes(other) && other.XGrid == XGrid;
        }

        public override string ToString() => $"{base.ToString()} {XGrid}";

        public override void Copy(OngekiObjectBase fromObj)
        {
            base.Copy(fromObj);

            if (fromObj is not OngekiMovableObjectBase from)
                return;

            XGrid = from.XGrid;
        }

        public override void Dispose()
        {
            base.Dispose();
            XGrid = default;
        }

        public override bool Clashes(OngekiTimelineObjectBase other)
        {
            return base.Clashes(other) && other is OngekiMovableObjectBase mov && mov.XGrid == XGrid;
        }
    }
}
