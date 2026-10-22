using Caliburn.Micro;
using OngekiFumenEditor.Base.Collections.Base;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Modules.FumenSoflanGroupListViewer.Models;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Base.Collections
{
    public class IndividualSoflanAreaListMap : PropertyChangedBase, IReadOnlyDictionary<int, IndividualSoflanAreaList>
    {
        private IntervalTreeWrapper<TGrid, IndividualSoflanArea> cacheTree = new(
            x => new() { Min = x.TGrid, Max = x.EndIndicator.TGrid },
            true,
            nameof(IndividualSoflanArea.TGrid),
            nameof(IndividualSoflanArea.EndIndicator.TGrid)
            );

        private QuadTreeWrapper<IndividualSoflanArea> cacheTotalTree = new(
            x => (float)x.XGrid.TotalUnit,
            x => (float)x.TGrid.TotalUnit,
            x => (float)x.EndIndicator.XGrid.TotalUnit,
            x => (float)x.EndIndicator.TGrid.TotalUnit,
            nameof(IndividualSoflanArea.TGrid),
            nameof(IndividualSoflanArea.EndIndicator.TGrid),
            nameof(IndividualSoflanArea.XGrid),
            nameof(IndividualSoflanArea.EndIndicator.XGrid)
            );

        public SoflanGroupTree SoflanGroupTree { get; } = new();

        private Dictionary<int, IndividualSoflanAreaList> isfMap = new();

        public IndividualSoflanAreaListMap()
        {
            SoflanGroupTree.EnsureDefaultGroup();
        }

        public IndividualSoflanAreaList this[int pattern]
        {
            get
            {
                if (!isfMap.TryGetValue(pattern, out var list))
                {
                    isfMap[pattern] = list = new();
                    list.OnChildPropertyChangedEvent += OnChildPropertyChangedEvent;
                }
                return list;
            }
        }

        private void OnChildPropertyChangedEvent(object sender, PropertyChangedEventArgs e)
        {
            if (sender is not IndividualSoflanArea isf)
                return;

            if (e.PropertyName == nameof(IndividualSoflanArea.SoflanGroup))
            {
                var oldSoflanGroup = -1;
                foreach (var pair in isfMap)
                {
                    if (pair.Value.Remove(isf))
                    {
                        oldSoflanGroup = pair.Key;
                        break;
                    }
                }

                this[isf.SoflanGroup].Add(isf);
                SoflanGroupTree.GetOrCreateGroup(isf.SoflanGroup);
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
            SoflanGroupTree.GetOrCreateGroup(isf.SoflanGroup);
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
            var result = cacheTotalTree.Query((float)xGrid.TotalUnit, (float)tGrid.TotalUnit);
            //ISF 归属规则:区域按开始时间升序检查,最先命中的区域生效;T 区间为 [TGrid, EndIndicator.TGrid),结束边界不包含
            return result
                .Where(x => tGrid < x.EndIndicator.TGrid)
                .OrderBy(x => x.TGrid)
                .FirstOrDefault()?.SoflanGroup ?? 0;
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
