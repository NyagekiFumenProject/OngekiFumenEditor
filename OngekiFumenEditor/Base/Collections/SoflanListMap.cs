using OngekiFumenEditor.Base.OngekiObjects;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;

namespace OngekiFumenEditor.Base.Collections
{
    public class SoflanListMap : IReadOnlyDictionary<int, SoflanList>
    {
        private Dictionary<ISoflan, int> registeredSoflanId = new();
        private Dictionary<int, SoflanList> soflanListMap = new();

        public event Action<ISoflan, PropertyChangedEventArgs> OnSoflanPropertyChangedEvent;
        public event Action<CollectionChangeEventArgs> OnCollectionChangedEvent;

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
                {
                    soflanListMap[pattern] = list = new();
                    OnCollectionChangedEvent?.Invoke(new CollectionChangeEventArgs(CollectionChangeAction.Add, list));
                    list.OnSoflanPropertyChangedEvent += OnSoflanPropertyChangedEvent;
                }
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
            registeredSoflanId[soflan] = soflan.SoflanGroup;
            soflan.PropertyChanged += Soflan_PropertyChanged;
        }

        private void Soflan_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (sender is not ISoflan soflan)
                return;

            if (e.PropertyName == nameof(ISoflan.SoflanGroup))
            {
                Remove(soflan);
                Add(soflan);
            }
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
            var beforeSoflanGroup = registeredSoflanId.TryGetValue(soflan, out var sg) ? sg : 0;
            var soflans = this[beforeSoflanGroup];
            registeredSoflanId.Remove(soflan);
            soflans.Remove(soflan);
            soflan.PropertyChanged -= Soflan_PropertyChanged;
        }

        public bool TryGetValue(int key, out SoflanList value)
        {
            return soflanListMap.TryGetValue(key, out value);
        }

        IEnumerator<KeyValuePair<int, SoflanList>> IEnumerable<KeyValuePair<int, SoflanList>>.GetEnumerator()
        {
            return soflanListMap.GetEnumerator();
        }
    }
}
