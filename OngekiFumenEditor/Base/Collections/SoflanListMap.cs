using OngekiFumenEditor.Base.OngekiObjects;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Base.Collections
{
    internal class SoflanListMap : IReadOnlyCollection<SoflanList>
    {
        private Dictionary<int, SoflanList> soflanListMap = new();

        public SoflanListMap()
        {

        }

        public SoflanList this[int pattern]
        {
            get
            {
                if (!soflanListMap.TryGetValue(pattern, out var list))
                {
                    soflanListMap[pattern] = list = new SoflanList();
                }
                return list;
            }
        }

        public int Count => soflanListMap.Count;

        public void Add(ISoflan soflan)
        {
            var soflans = this[soflan.Pattern];
            soflans.Add(soflan);
        }

        public IEnumerator<SoflanList> GetEnumerator()
        {
            return soflanListMap.Values.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
