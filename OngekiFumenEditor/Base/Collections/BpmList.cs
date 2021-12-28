using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Base.OngekiObjects.Collections
{
    public class BpmList : IEnumerable<BPMChange>
    {
        private BPMChange firstBpm = new BPMChange();
        private List<BPMChange> changedBpmList = new List<BPMChange>();
        public BPMChange FirstBpm => firstBpm;

        public event Action OnChangedEvent;

        public void Add(BPMChange bpm)
        {
            changedBpmList.Add(bpm);
            bpm.PropertyChanged += OnBpmPropChanged;
            OnChangedEvent?.Invoke();
        }
        
        private void OnBpmPropChanged(object sender, PropertyChangedEventArgs e)
        {
            OnChangedEvent?.Invoke();
        }

        public void SetFirstBpm(BPMChange firstBpm)
        {
            this.firstBpm = firstBpm;
            OnChangedEvent?.Invoke();
            firstBpm.PropertyChanged += OnBpmPropChanged;
        }

        public void Remove(BPMChange bpm)
        {
            if (bpm == firstBpm)
                throw new Exception($"BpmList can't delete firstBpm : {bpm}");
            changedBpmList.Remove(bpm);
            bpm.PropertyChanged -= OnBpmPropChanged;
            OnChangedEvent?.Invoke();
        }

        public IEnumerator<BPMChange> GetEnumerator()
        {
            yield return firstBpm;
            foreach (var item in changedBpmList.OrderBy(x=>x.TGrid))
                yield return item;
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public void Sort() => changedBpmList.Sort();

        public BPMChange GetBpm(TGrid time) => this.LastOrDefault(bpm => bpm.TGrid <= time);

        public BPMChange GetPrevBpm(BPMChange time) => GetPrevBpm(time.TGrid);

        public BPMChange GetPrevBpm(TGrid time) => this.LastOrDefault(bpm => bpm.TGrid < time);

        public BPMChange GetNextBpm(BPMChange bpm) => GetNextBpm(bpm.TGrid);

        public BPMChange GetNextBpm(TGrid time) => this.FirstOrDefault(bpm => time < bpm.TGrid);
    }
}
