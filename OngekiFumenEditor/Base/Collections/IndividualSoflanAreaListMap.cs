using OngekiFumenEditor.Base.Collections.Base;
using OngekiFumenEditor.Base.OngekiObjects;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Base.Collections
{
    public class IndividualSoflanAreaListMap : IReadOnlyDictionary<int, IndividualSoflanAreaList>
    {
        private IntervalTreeWrapper<TGrid, IndividualSoflanArea> cacheTree = new(
            x => new() { Min = x.TGrid, Max = x.EndIndicator.TGrid },
            true,
            nameof(IndividualSoflanArea.TGrid),
            nameof(IndividualSoflanArea.EndIndicator.TGrid)
            );

        private NotQuadTreeWrapper<float, float, IndividualSoflanArea> cacheTotalTree = new(
            x => (float)x.XGrid.TotalUnit,
            x => (float)x.TGrid.TotalUnit,
            x => (float)x.EndIndicator.XGrid.TotalUnit,
            x => (float)x.EndIndicator.TGrid.TotalUnit,
            nameof(IndividualSoflanArea.TGrid),
            nameof(IndividualSoflanArea.EndIndicator.TGrid),
            nameof(IndividualSoflanArea.XGrid),
            nameof(IndividualSoflanArea.EndIndicator.XGrid)
            );

        private Dictionary<int, IndividualSoflanAreaList> isfMap = new();

        public IndividualSoflanAreaList this[int pattern]
        {
            get
            {
                if (!isfMap.TryGetValue(pattern, out var list))
                    isfMap[pattern] = list = new();
                return list;
            }
        }

        public int Count => isfMap.Count;

        public IEnumerable<int> Keys => isfMap.Keys;

        public IEnumerable<IndividualSoflanAreaList> Values => isfMap.Values;

        public void Add(IndividualSoflanArea isf)
        {
            this[isf.SoflanGroup].Add(isf);
            cacheTree.Add(isf);
            cacheTotalTree.Add(isf);
        }

        public void Remove(IndividualSoflanArea isf)
        {
            this[isf.SoflanGroup].Remove(isf);
            cacheTree.Remove(isf);
            cacheTotalTree.Remove(isf);
        }

        IEnumerator IEnumerable.GetEnumerator() => isfMap.GetEnumerator();

        public bool ContainsKey(int key)
        {
            return isfMap.ContainsKey(key);
        }

        public bool TryGetValue(int key, [MaybeNullWhen(false)] out IndividualSoflanAreaList value)
        {
            return isfMap.TryGetValue(key, out value);
        }

        IEnumerator<KeyValuePair<int, IndividualSoflanAreaList>> IEnumerable<KeyValuePair<int, IndividualSoflanAreaList>>.GetEnumerator()
        {
            return isfMap.GetEnumerator();
        }

        public int QuerySoflanGroup(XGrid xGrid, TGrid tGrid)
        {
            return cacheTotalTree.Query((float)xGrid.TotalUnit, (float)tGrid.TotalUnit).FirstOrDefault()?.SoflanGroup ?? 0;
        }

        public int QuerySoflanGroup<T>(T obj) where T : IHorizonPositionObject, ITimelineObject
        {
            return QuerySoflanGroup(obj.XGrid, obj.TGrid);
        }

        public string DebugFindDataQueryPath(IndividualSoflanArea isf)
        {
            return cacheTotalTree.DebugFindDataQueryPath(isf);
        }

        public void DebugDump()
        {
            cacheTotalTree.DebugDump();
        }
    }
}
