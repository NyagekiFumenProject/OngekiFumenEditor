using Caliburn.Micro;
using OngekiFumenEditor.Base.Collections.Base;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Modules.FumenSoflanGroupListViewer.Models;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
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

        private SoflanGroupWrapItemGroup soflanGroupWrapItemGroupRoot = new()
        {
            IsDisplayInDesignMode = false,
            IsDisplayInPreviewMode = false,
            IsSelected = false,
            DisplayName = "root"
        };

        private SoflanGroupWrapItemGroup defaultItemGroup = new()
        {
            DisplayName = "default"
        };

        private Dictionary<int, SoflanGroupWrapItem> cachedSoflanGroupWrapItemMap = new();
        public SoflanGroupWrapItemGroup SoflanGroupWrapItemGroupRoot => soflanGroupWrapItemGroupRoot;

        private Dictionary<int, IndividualSoflanAreaList> isfMap = new();

        public IndividualSoflanAreaListMap()
        {
            SoflanGroupWrapItemGroupRoot.Add(defaultItemGroup);
        }

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

            var wrapItem = TryGetOrCreateSoflanGroupWrapItem(isf.SoflanGroup, out var isCreated);
            if (isCreated)
                defaultItemGroup.Add(wrapItem);
        }

        public void Remove(IndividualSoflanArea isf)
        {
            this[isf.SoflanGroup].Remove(isf);
            cacheTree.Remove(isf);
            cacheTotalTree.Remove(isf);

            if (this[isf.SoflanGroup].Count == 0)
            {
                var item = TryGetOrCreateSoflanGroupWrapItem(isf.SoflanGroup, out _);
                item.Parent?.Remove(item);
            }
        }

        public SoflanGroupWrapItem TryGetOrCreateSoflanGroupWrapItem(int soflanGroup, out bool isCreated)
        {
            if (isCreated = !cachedSoflanGroupWrapItemMap.TryGetValue(soflanGroup, out var item))
                cachedSoflanGroupWrapItemMap[soflanGroup] = item = new SoflanGroupWrapItem(soflanGroup);
            return item;
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
