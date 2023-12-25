using OngekiFumenEditor.Base.Collections.Base;
using OngekiFumenEditor.Base.EditorObjects;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace OngekiFumenEditor.Base.Collections
{
	public partial class SoflanList : IReadOnlyCollection<ISoflan>
	{
		private IntervalTreeWrapper<TGrid, ISoflan> soflans = new(
			x => new() { Min = x.TGrid, Max = x.EndTGrid },
			true,
			nameof(Soflan.TGrid),
			nameof(Soflan.EndTGrid)
			);

		public int Count => soflans.Count;

		public event Action OnChangedEvent;

		public IEnumerator<ISoflan> GetEnumerator() => soflans.GetEnumerator();
		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

		public IEnumerable<ISoflan> GetVisibleStartObjects(TGrid min, TGrid max)
		{
			return soflans.QueryInRange(min, max);
		}

		public SoflanList(IEnumerable<ISoflan> initSoflanChanges = default)
		{
			OnChangedEvent += OnChilidrenSubPropsChangedEvent;
			foreach (var item in initSoflanChanges ?? Enumerable.Empty<IKeyframeSoflan>())
				Add(item);
		}

		private void OnChilidrenSubPropsChangedEvent()
		{
			cachedSoflanListCacheHash = RandomHepler.Random(int.MinValue, int.MaxValue);
		}

		public void Add(ISoflan soflan)
		{
			soflans.Add(soflan);
			soflan.PropertyChanged += OnSoflanPropChanged;
			OnChangedEvent?.Invoke();
		}

		private void OnSoflanPropChanged(object sender, PropertyChangedEventArgs e)
		{
			switch (e.PropertyName)
			{
				case nameof(Soflan.Speed):
				case nameof(Soflan.TGrid):
				case nameof(TGrid.Grid):
				case nameof(TGrid.Unit):
				case nameof(Soflan.ApplySpeedInDesignMode):
				case nameof(InterpolatableSoflan.Easing):
				case nameof(InterpolatableSoflan.InterpolateCountPerResT):
				case nameof(Soflan.EndTGrid):
				case nameof(Soflan.GridLength):
					OnChangedEvent?.Invoke();
					break;
				default:
					break;
			}
		}

		public void Remove(ISoflan soflan)
		{
			soflans.Remove(soflan);
			soflan.PropertyChanged -= OnSoflanPropChanged;
			OnChangedEvent?.Invoke();
		}
	}
}
