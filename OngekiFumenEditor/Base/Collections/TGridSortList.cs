using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Base.Collections.Base
{
    public class TGridSortList<T> : SortableCollection<T, TGrid> where T : ITimelineObject, INotifyPropertyChanged
    {
        public TGridSortList() : base(x => x.TGrid, nameof(ITimelineObject.TGrid))
        {

        }
    }
}
