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

        public event PropertyChangedEventHandler OnPropertyChangedEvent;
        public event Action<ISoflan> OnCollectionChangedEvent;

        public IEnumerator<ISoflan> GetEnumerator() => soflans.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public IEnumerable<ISoflan> GetVisibleStartObjects(TGrid min, TGrid max)
        {
            return soflans.QueryInRange(min, max);
        }

        public SoflanList(IEnumerable<ISoflan> initSoflanChanges = default)
        {
            OnPropertyChangedEvent += OnChilidrenSubPropsChangedEvent;
            foreach (var item in initSoflanChanges ?? [new KeyframeSoflan()
            {
                TGrid = new TGrid(0,0),
                Speed = 1f,
            }])
                Add(item);
        }

        private void OnChilidrenSubPropsChangedEvent(object sender, PropertyChangedEventArgs e)
        {
            cachedSoflanListCacheHash = RandomHepler.Random(int.MinValue, int.MaxValue);
        }

        public void Add(ISoflan soflan)
        {
            soflans.Add(soflan);
            soflan.PropertyChanged += OnSoflanPropChanged;
            OnCollectionChangedEvent?.Invoke(soflan);
            cachedSoflanListCacheHash = RandomHepler.Random(int.MinValue, int.MaxValue);
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
                case nameof(Soflan.SoflanGroup):
                    OnPropertyChangedEvent?.Invoke(sender, e);
                    break;
                default:
                    break;
            }
        }

        public void Remove(ISoflan soflan)
        {
            soflans.Remove(soflan);
            soflan.PropertyChanged -= OnSoflanPropChanged;
            OnCollectionChangedEvent?.Invoke(soflan);
            cachedSoflanListCacheHash = RandomHepler.Random(int.MinValue, int.MaxValue);
        }
    }
}
