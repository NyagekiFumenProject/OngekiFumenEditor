using System.ComponentModel;

namespace OngekiFumenEditor.Base.Collections.Base
{
	public class TGridSortList<T> : RemindableSortableCollection<T, TGrid> where T : ITimelineObject, INotifyPropertyChanged
	{
		public TGridSortList() : base(x => x.TGrid, nameof(ITimelineObject.TGrid))
		{

		}
	}
}
