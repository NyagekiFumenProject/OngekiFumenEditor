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
            XGrid = XGrid.Zero,
            Time = TimeSpan.Zero
        };
        private readonly SortableCollection<Record, TimeSpan> list;

        public class Record
        {
            public XGrid XGrid { get; set; }
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

        public void Commit(TimeSpan time, XGrid xGrid)
        {
            Trim(time);

            var record = ObjectPool<Record>.Get();
            record.Time = time;
            record.XGrid = xGrid;
            list.Add(record);

            //check and remove duplicate records
            while (list.Count >= 3 && list[^1].XGrid == list[^2].XGrid && list[^2].XGrid == list[^3].XGrid)
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
                return cur.XGrid.TotalUnit;

            //limit
            var t = Math.Min(time.TotalMilliseconds, next.Time.TotalMilliseconds);
            t = Math.Max(t, cur.Time.TotalMilliseconds);

            var calXUnit = MathUtils.CalculateXFromTwoPointFormFormula(t, cur.XGrid.TotalUnit, cur.Time.TotalMilliseconds, next.XGrid.TotalUnit, next.Time.TotalMilliseconds);
            return calXUnit;
        }

        public XGrid GetLocationXGrid(TimeSpan time)
        {
            var xGrid = new XGrid((float)GetLocationXUnit(time), 0);
            xGrid.NormalizeSelf();

            return xGrid;
        }
    }
}
