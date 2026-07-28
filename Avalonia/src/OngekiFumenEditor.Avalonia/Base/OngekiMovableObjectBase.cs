using OngekiFumenEditor.Avalonia.Base.Attributes;

namespace OngekiFumenEditor.Avalonia.Base
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
                OnPropertyChanged(() => XGrid);
            }
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
    }
}

