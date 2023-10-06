using IntervalTree;
using OngekiFumenEditor.Base.Collections.Base;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Utils;
using OngekiFumenEditor.Utils.ObjectPool;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace OngekiFumenEditor.Base.Collections
{
    public partial class SoflanList : TGridSortList<Soflan>
    {
        public event Action OnChangedEvent;

        public SoflanList(IEnumerable<Soflan> initBpmChanges = default)
        {
            OnChangedEvent += OnChilidrenSubPropsChangedEvent;
            foreach (var item in initBpmChanges ?? Enumerable.Empty<Soflan>())
                Add(item);
        }

        private void OnChilidrenSubPropsChangedEvent()
        {
            cachedSoflanListCacheHash = int.MinValue;
        }

        public override void Add(Soflan soflan)
        {
            base.Add(soflan);
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
                case nameof(Soflan.EndTGrid):
                case nameof(Soflan.GridLength):
                    OnChangedEvent?.Invoke();
                    break;
                default:
                    break;
            }
        }

        public override void Remove(Soflan soflan)
        {
            base.Remove(soflan);
            soflan.PropertyChanged -= OnSoflanPropChanged;
            OnChangedEvent?.Invoke();
        }
    }
}
