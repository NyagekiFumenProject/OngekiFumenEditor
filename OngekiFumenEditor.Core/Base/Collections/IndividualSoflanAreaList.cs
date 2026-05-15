using OngekiFumenEditor.Core.Base.Collections.Base;
using OngekiFumenEditor.Core.Base.EditorObjects;
using OngekiFumenEditor.Core.Base.OngekiObjects;
using OngekiFumenEditor.Core.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Core.Base.Collections
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

        public event PropertyChangedEventHandler OnChildPropertyChangedEvent;
        public event Action<IndividualSoflanArea> OnCollectionChangedEvent;

        public IEnumerator<IndividualSoflanArea> GetEnumerator() => list.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public IndividualSoflanAreaList(IEnumerable<IndividualSoflanArea> initSoflanChanges = default)
        {
            foreach (var item in initSoflanChanges ?? Enumerable.Empty<IndividualSoflanArea>())
                Add(item);
        }

        public void Add(IndividualSoflanArea isf)
        {
            list.Add(isf);
            isf.PropertyChanged += OnIndividualSoflanAreaPropChanged;
            OnCollectionChangedEvent?.Invoke(isf);
        }

        public bool Remove(IndividualSoflanArea isf)
        {
            if (list.Remove(isf))
            {
                isf.PropertyChanged -= OnIndividualSoflanAreaPropChanged;
                OnCollectionChangedEvent?.Invoke(isf);
                return true;
            }

            return false;
        }

        private void OnIndividualSoflanAreaPropChanged(object sender, PropertyChangedEventArgs e)
        {
            OnChildPropertyChangedEvent?.Invoke(sender, e);
        }

        public IEnumerable<IndividualSoflanArea> GetVisibleStartObjects(TGrid min, TGrid max)
        {
            return list.QueryInRange(min, max);
        }
    }
}

