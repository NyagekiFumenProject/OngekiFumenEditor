using OngekiFumenEditor.Base.Attributes;
using OngekiFumenEditor.Utils;
using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace OngekiFumenEditor.Base
{
    public abstract class OngekiTimelineObjectBase : OngekiObjectBase, ITimelineObject, IDisplayableObject, ISelectableObject, IDisposable
    {
        private TGrid tGrid = new TGrid();

        [ObjectPropertyBrowserTipText("ObjectTGrid")]
        public virtual TGrid TGrid
        {
            get => tGrid;
            set
            {
                this.RegisterOrUnregisterPropertyChangeEvent(tGrid, value, OnTGridPropertyChanged);
                tGrid = value;
                NotifyOfPropertyChange(() => TGrid);
            }
        }

        private void OnTGridPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            // Unit/Grid 是坐标分量内部通知；对外统一表达为 TGrid 变化。
            NotifyOfPropertyChange(e.PropertyName is nameof(GridBase.Unit) or nameof(GridBase.Grid)
                ? nameof(TGrid)
                : e.PropertyName);
        }

        private bool isSelecting = false;

        [ObjectPropertyBrowserHide]
        public virtual bool IsSelected
        {
            get => isSelecting;
            set => Set(ref isSelecting, value);
        }

        public virtual bool CheckVisiable(TGrid minVisibleTGrid, TGrid maxVisibleTGrid)
        {
            return minVisibleTGrid <= TGrid && TGrid <= maxVisibleTGrid;
        }

        public virtual IEnumerable<IDisplayableObject> GetDisplayableObjects()
        {
            yield return this;
        }

        public int CompareTo(ITimelineObject obj)
        {
            return TGrid.CompareTo(obj?.TGrid);
        }

        public override void Copy(OngekiObjectBase fromObj)
        {
            if (fromObj is not OngekiTimelineObjectBase timelineObject)
                return;

            TGrid = timelineObject.TGrid;
        }

        public override string ToString() => $"{base.ToString()} {TGrid}";

        public virtual void Dispose()
        {
            TGrid = default;
        }
    }
}

