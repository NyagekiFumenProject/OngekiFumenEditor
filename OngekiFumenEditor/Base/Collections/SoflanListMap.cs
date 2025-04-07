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
    public class SoflanListMap : IReadOnlyDictionary<int, SoflanList>
    {
        private Dictionary<int, SoflanList> soflanListMap = new();

        public const int DefaultSoflanGroup = 0;

        public SoflanList DefaultSoflanList { get; } = new SoflanList();

        public SoflanListMap()
        {
            soflanListMap[DefaultSoflanGroup] = DefaultSoflanList;
        }

        public SoflanList this[int pattern]
        {
            get
            {
                if (!soflanListMap.TryGetValue(pattern, out var list))
                    soflanListMap[pattern] = list = new();
                return list;
            }
        }

        public int Count => soflanListMap.Count;

        public IEnumerable<int> Keys => soflanListMap.Keys;

        public IEnumerable<SoflanList> Values => soflanListMap.Values;

        public void Add(ISoflan soflan)
        {
            var soflans = this[soflan.SoflanGroup];
            soflans.Add(soflan);
        }

        public bool ContainsKey(int key)
        {
            return soflanListMap.ContainsKey(key);
        }

        public IEnumerator GetEnumerator()
        {
            return soflanListMap.GetEnumerator();
        }

        public void Remove(ISoflan soflan)
        {
            var soflans = this[soflan.SoflanGroup];
            soflans.Remove(soflan);
        }

        public bool TryGetValue(int key, [MaybeNullWhen(false)] out SoflanList value)
        {
            return soflanListMap.TryGetValue(key, out value);
        }

        IEnumerator<KeyValuePair<int, SoflanList>> IEnumerable<KeyValuePair<int, SoflanList>>.GetEnumerator()
        {
            return soflanListMap.GetEnumerator();
        }
    }
}
