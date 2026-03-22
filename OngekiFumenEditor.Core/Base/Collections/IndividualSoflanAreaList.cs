using OngekiFumenEditor.Base.Collections.Base;
using OngekiFumenEditor.Base.EditorObjects;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Base.Collections
{
    public class IndividualSoflanAreaList : IReadOnlyCollection<IndividualSoflanArea>
    {
        private IntervalTreeWrapper<TGrid, IndividualSoflanArea> list = new(
            x => new() { Min = x.TGrid, Max = x.EndIndicator.TGrid },
            true,
            nameof(IndividualSoflanArea.TGrid),
            nameof(IndividualSoflanArea.EndIndicator.TGrid)
            );

        public int Count => list.Count;

        public event PropertyChangedEventHandler OnPropertyChangedEvent;
        public event Action<IndividualSoflanArea> OnCollectionChangedEvent;

        public IEnumerator<IndividualSoflanArea> GetEnumerator() => list.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public IndividualSoflanAreaList(IEnumerable<IndividualSoflanArea> initSoflanChanges = default)
        {
            OnPropertyChangedEvent += OnChilidrenSubPropsChangedEvent;
            foreach (var item in initSoflanChanges ?? Enumerable.Empty<IndividualSoflanArea>())
                Add(item);
        }

        private void OnChilidrenSubPropsChangedEvent(object sender, PropertyChangedEventArgs e)
        {

        }

        public void Add(IndividualSoflanArea soflan)
        {
            list.Add(soflan);
            soflan.PropertyChanged += OnSoflanPropChanged;
            OnCollectionChangedEvent?.Invoke(soflan);
        }

        private void OnSoflanPropChanged(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(IndividualSoflanArea.TGrid):
                case nameof(TGrid.Grid):
                case nameof(TGrid.Unit):
                    OnPropertyChangedEvent?.Invoke(sender, e);
                    break;
                default:
                    break;
            }
        }

        public void Remove(IndividualSoflanArea soflan)
        {
            list.Remove(soflan);
            soflan.PropertyChanged -= OnSoflanPropChanged;
            OnCollectionChangedEvent?.Invoke(soflan);
        }

        public IEnumerable<IndividualSoflanArea> GetVisibleStartObjects(TGrid min, TGrid max)
        {
            return list.QueryInRange(min, max);
        }
    }
}
