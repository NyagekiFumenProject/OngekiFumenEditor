using OngekiFumenEditor.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Base
{
    public abstract class OngekiTimelineObjectBase : OngekiObjectBase, ITimelineObject, IDisplayableObject
    {
        private TGrid tGrid = new TGrid();
        public virtual TGrid TGrid
        {
            get { return tGrid; }
            set
            {
                this.RegisterOrUnregisterPropertyChangeEvent(tGrid, value);
                tGrid = value;
                NotifyOfPropertyChange(() => TGrid);
            }
        }

        public abstract Type ModelViewType { get; }

        public virtual bool CheckVisiable(TGrid minVisibleTGrid, TGrid maxVisibleTGrid)
        {
            return minVisibleTGrid <= TGrid && TGrid <= maxVisibleTGrid;
        }

        public virtual IEnumerable<IDisplayableObject> GetDisplayableObjects()
        {
            yield return this;
        }

        public int CompareTo(object obj)
        {
            return TGrid.CompareTo((obj as ITimelineObject)?.TGrid);
        }

        public override void Copy(OngekiObjectBase fromObj, OngekiFumen fumen)
        {
            if (fromObj is not OngekiTimelineObjectBase timelineObject)
                return;

            TGrid = timelineObject.TGrid;
        }

        public override string ToString() => $"{base.ToString()} {TGrid}";
    }
}
