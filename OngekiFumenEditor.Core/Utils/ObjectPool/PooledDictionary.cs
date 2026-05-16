using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace OngekiFumenEditor.Core.Utils.ObjectPool
{
    internal sealed class PooledDictionary<TKey, TValue> : IPooledDictionary<TKey, TValue>
    {
        private bool disposed;

        private readonly Dictionary<TKey, TValue> innerDictionary = new();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void Rent()
        {
            disposed = false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            ObjectPool<PooledDictionary<TKey, TValue>>.Return(this);
        }

        public TValue this[TKey key]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => innerDictionary[key];
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set => innerDictionary[key] = value;
        }

        public ICollection<TKey> Keys
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => innerDictionary.Keys;
        }

        public ICollection<TValue> Values
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => innerDictionary.Values;
        }

        IEnumerable<TKey> IReadOnlyDictionary<TKey, TValue>.Keys
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => innerDictionary.Keys;
        }

        IEnumerable<TValue> IReadOnlyDictionary<TKey, TValue>.Values
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => innerDictionary.Values;
        }

        public int Count
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => innerDictionary.Count;
        }

        public bool IsReadOnly
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ((ICollection<KeyValuePair<TKey, TValue>>)innerDictionary).IsReadOnly;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add(TKey key, TValue value) => innerDictionary.Add(key, value);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add(KeyValuePair<TKey, TValue> item) => ((ICollection<KeyValuePair<TKey, TValue>>)innerDictionary).Add(item);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Clear() => innerDictionary.Clear();
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Contains(KeyValuePair<TKey, TValue> item) => ((ICollection<KeyValuePair<TKey, TValue>>)innerDictionary).Contains(item);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ContainsKey(TKey key) => innerDictionary.ContainsKey(key);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex) => ((ICollection<KeyValuePair<TKey, TValue>>)innerDictionary).CopyTo(array, arrayIndex);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() => innerDictionary.GetEnumerator();
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Remove(TKey key) => innerDictionary.Remove(key);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Remove(KeyValuePair<TKey, TValue> item) => ((ICollection<KeyValuePair<TKey, TValue>>)innerDictionary).Remove(item);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetValue(TKey key, out TValue value) => innerDictionary.TryGetValue(key, out value);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
