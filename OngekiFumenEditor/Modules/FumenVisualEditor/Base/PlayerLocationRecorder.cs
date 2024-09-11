using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.Collections.Base;
using OngekiFumenEditor.Utils;
using OngekiFumenEditor.Utils.ObjectPool;
using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Base
{
    public class PlayerLocationRecorder
    {
        private readonly Record defaultRecord = new Record()
        {
            XTotalUnit = 0,
            Time = TimeSpan.Zero
        };
        private readonly SortableCollection<Record, TimeSpan> list;

        public class Record
        {
            public double XTotalUnit { get; set; }
            public TimeSpan Time { get; set; }
        }

        public PlayerLocationRecorder()
        {
            list = new SortableCollection<Record, TimeSpan>(x => x.Time)
            {
                defaultRecord
            };
        }

        public void Trim(TimeSpan time)
        {
            //remove unused record if playTime is go back
            while (list.Count > 0 && list[list.Count - 1].Time >= time)
            {
                var remove = list.RemoveAt(list.Count - 1);
                ObjectPool<Record>.Return(remove);
            }
        }

        public void Commit(TimeSpan time, double xGrid)
        {
            Trim(time);

            var record = ObjectPool<Record>.Get();
            record.Time = time;
            record.XTotalUnit = xGrid;
            list.Add(record);

            //check and remove duplicate records
            while (list.Count >= 3 && list[^1].XTotalUnit == list[^2].XTotalUnit && list[^2].XTotalUnit == list[^3].XTotalUnit)
            {
                var remove = list.RemoveAt(list.Count - 2);
                ObjectPool<Record>.Return(remove);
            }
        }

        public double GetLocationXUnit(TimeSpan time)
        {
            if (list.Count == 0)
                return default;

            var cur = default(Record);
            var next = default(Record);

            var idx = list.BinaryFindLastIndexByKey(time);
            if (idx == 0)
            {
                cur = defaultRecord;
                next = list.FirstOrDefault();
            }
            else
            {
                cur = list.ElementAtOrDefault(idx - 1);
                next = list.ElementAtOrDefault(idx);
            }

            if (next == null)
                return cur.XTotalUnit;

            //limit
            var t = Math.Min(time.TotalMilliseconds, next.Time.TotalMilliseconds);
            t = Math.Max(t, cur.Time.TotalMilliseconds);

            var calXUnit = MathUtils.CalculateXFromTwoPointFormFormula(t, cur.XTotalUnit, cur.Time.TotalMilliseconds, next.XTotalUnit, next.Time.TotalMilliseconds);
            return calXUnit;
        }

        internal void Clear()
        {
            list.Clear();
            Log.LogDebug($"recorder list has clear.");
        }
    }
}
