using OngekiFumenEditor.Core.Base;
using System.Collections.Generic;
using System.Linq;

namespace OngekiFumenEditor.Core.Utils
{
    public static class TGridExtensionMethod
    {
        public static IEnumerable<(TGrid minTGrid, TGrid maxTGrid)> Merge(this IEnumerable<(TGrid minTGrid, TGrid maxTGrid)> list)
        {
            var sortedList = list.OrderBy(x => x.minTGrid);

            using var itor = sortedList.GetEnumerator();
            if (!itor.MoveNext())
                yield break;

            var cur = itor.Current;
            while (itor.MoveNext())
            {
                var next = itor.Current;
                if (next.minTGrid <= cur.maxTGrid)
                {
                    cur = new(cur.minTGrid > next.minTGrid ? next.minTGrid : cur.minTGrid,
                        cur.maxTGrid > next.maxTGrid ? cur.maxTGrid : next.maxTGrid);
                }
                else
                {
                    yield return cur;
                    cur = next;
                }
            }

            if (cur.minTGrid is not null)
                yield return cur;
        }
    }
}

