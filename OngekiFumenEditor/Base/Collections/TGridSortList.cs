using OngekiFumenEditor.Base.Collections.Base;
using System.ComponentModel;

namespace OngekiFumenEditor.Base.Collections
{
	public class TGridSortList<T> : RemindableSortableCollection<T, TGrid> where T : ITimelineObject, INotifyPropertyChanged
	{
		public TGridSortList() : base(x => x.TGrid, nameof(ITimelineObject.TGrid))
		{

		}
	}
}
