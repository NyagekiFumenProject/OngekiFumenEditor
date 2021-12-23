using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Base.OngekiObjects
{
    public class BpmList : IEnumerable<BPMChange>
    {
        private BPMChange firstBpm = new BPMChange();
        private List<BPMChange> changedBpmList = new List<BPMChange>();

        public BPMChange FirstBpm => firstBpm;
        public IEnumerable<BPMChange> BpmChanges => changedBpmList;

        public IEnumerator<BPMChange> GetEnumerator()
        {
            yield return firstBpm;
            foreach (var item in changedBpmList)
                yield return item;
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public void Sort() => changedBpmList.Sort();

        public void Add(BPMChange bpm)
        {
            changedBpmList.Add(bpm);
        }

        public void SetFirstBpm(BPMChange firstBpm)
        {
            this.firstBpm = firstBpm;
        }

        public BPMChange GetBpm(TGrid time)
        {
            return this.LastOrDefault(bpm => bpm.TGrid <= time);
        }

        public BPMChange GetPrevBpm(BPMChange time) => GetPrevBpm(time.TGrid);

        public BPMChange GetPrevBpm(TGrid time)
        {
            return this.LastOrDefault(bpm => bpm.TGrid < time);
        }

        public BPMChange GetNextBpm(BPMChange bpm) => GetNextBpm(bpm.TGrid);

        public BPMChange GetNextBpm(TGrid time)
        {
            return this.FirstOrDefault(bpm => time < bpm.TGrid);
        }
    }
}
