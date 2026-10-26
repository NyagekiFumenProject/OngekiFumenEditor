using OngekiFumenEditor.Base.Attributes;
using OngekiFumenEditor.Utils;
using System.ComponentModel;

namespace OngekiFumenEditor.Base
{
    public abstract class OngekiMovableObjectBase : OngekiTimelineObjectBase, IHorizonPositionObject
    {
        private XGrid xGrid = new XGrid();

        [ObjectPropertyBrowserTipText("ObjectXGrid")]
        public virtual XGrid XGrid
        {
            get => xGrid;
            set
            {
                this.RegisterOrUnregisterPropertyChangeEvent(xGrid, value, OnXGridPropertyChanged);
                xGrid = value;
                NotifyOfPropertyChange(() => XGrid);
            }
        }

        private void OnXGridPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            // Unit/Grid 是坐标分量内部通知；对外统一表达为 XGrid 变化。
            NotifyOfPropertyChange(e.PropertyName is nameof(GridBase.Unit) or nameof(GridBase.Grid)
                ? nameof(XGrid)
                : e.PropertyName);
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

