using OngekiFumenEditor.Base;
using OngekiFumenEditor.Utils.ObjectPool;
using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.CompilerServices;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace OngekiFumenEditor.Utils
{
    public static class TGridExtensionMethod
    {
        public static IEnumerable<(TGrid minTGrid, TGrid maxTGrid)> Merge(this IEnumerable<(TGrid minTGrid, TGrid maxTGrid)> list)
        {
            var sortedList = list.OrderBy(x => x.minTGrid);

            var itor = sortedList.GetEnumerator();
            if (!itor.MoveNext())
                yield break;

            var cur = itor.Current;
            while (itor.MoveNext())
            {
                var next = itor.Current;
                if (next.minTGrid <= cur.maxTGrid)
                {
                    //combinable
                    cur = new(MathUtils.Min(cur.minTGrid, next.minTGrid), MathUtils.Max(cur.maxTGrid, next.maxTGrid));
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
