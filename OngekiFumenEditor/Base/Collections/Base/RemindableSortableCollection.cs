using System;
using System.ComponentModel;
using System.Linq;

namespace OngekiFumenEditor.Base.Collections.Base
{
	public class RemindableSortableCollection<T, X> : SortableCollection<T, X> where T : INotifyPropertyChanged where X : IComparable<X>
	{
		private readonly string sortKeyPropertyName;

		public RemindableSortableCollection(Func<T, X> sortKeySelector, string sortKeyPropertyName) : base(sortKeySelector)
		{
			this.sortKeyPropertyName = sortKeyPropertyName;
		}

		private void OnItemPropChanged(object sender, PropertyChangedEventArgs e)
		{
			if (IsBatching)
				return;

			if (e.PropertyName == sortKeyPropertyName)
			{
				var obj = (T)sender;
				Remove(obj);
				Add(obj);
			}
		}

		public override void Add(T obj)
		{
			base.Add(obj);
			obj.PropertyChanged += OnItemPropChanged;
		}

		public override bool Remove(T obj)
		{
			var r =base.Remove(obj);
			obj.PropertyChanged -= OnItemPropChanged;
			return r;
		}

		public void Clear()
		{
			foreach (var obj in this.ToArray())
				Remove(obj);
		}
	}
}
