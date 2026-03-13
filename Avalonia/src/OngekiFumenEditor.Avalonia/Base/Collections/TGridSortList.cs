using System.ComponentModel;
using OngekiFumenEditor.Avalonia.Base.Collections.Base;

namespace OngekiFumenEditor.Avalonia.Base.Collections
{
	public class TGridSortList<T> : RemindableSortableCollection<T, TGrid> where T : ITimelineObject, INotifyPropertyChanged
	{
		public TGridSortList() : base(x => x.TGrid, nameof(ITimelineObject.TGrid))
		{

		}
	}
}
