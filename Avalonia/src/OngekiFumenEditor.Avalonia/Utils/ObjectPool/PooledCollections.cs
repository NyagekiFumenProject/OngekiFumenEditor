using System;
using System.Collections;
using System.Collections.Generic;

namespace OngekiFumenEditor.Avalonia.Utils.ObjectPool
{
    public interface IPooledList<T> : IList<T>, IReadOnlyList<T>, IDisposable
    {
        new T this[int index] { get; set; }
        new int Count { get; }
        void AddRange(IEnumerable<T> items);
        void Sort(IComparer<T> comparer);
    }

    public interface IPooledDictionary<TKey, TValue> : IDictionary<TKey, TValue>, IReadOnlyDictionary<TKey, TValue>, IDisposable
    {
        new TValue this[TKey key] { get; set; }
        new int Count { get; }
        new bool TryGetValue(TKey key, out TValue value);
    }

    internal sealed class PooledList<T> : IPooledList<T>
    {
        private bool disposed;

        private readonly List<T> innerList = new();

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        internal void Rent()
        {
            Clear();
            disposed = false;
        }

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            Clear();
            ObjectPool<PooledList<T>>.Return(this);
        }

        public T this[int index]
        {
            [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            get => innerList[index];
            [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            set => innerList[index] = value;
        }

        public int Count => innerList.Count;

        public bool IsReadOnly => false;

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public void Add(T item) => innerList.Add(item);

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public void AddRange(IEnumerable<T> items) => innerList.AddRange(items);

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public void Clear() => innerList.Clear();

        public bool Contains(T item) => innerList.Contains(item);

        public void CopyTo(T[] array, int arrayIndex) => innerList.CopyTo(array, arrayIndex);

        public void Sort(IComparer<T> comparer) => innerList.Sort(comparer);

        public IEnumerator<T> GetEnumerator() => innerList.GetEnumerator();

        public int IndexOf(T item) => innerList.IndexOf(item);

        public void Insert(int index, T item) => innerList.Insert(index, item);

        public bool Remove(T item) => innerList.Remove(item);

        public void RemoveAt(int index) => innerList.RemoveAt(index);

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    internal sealed class PooledDictionary<TKey, TValue> : IPooledDictionary<TKey, TValue>
    {
        private bool disposed;

        private readonly Dictionary<TKey, TValue> innerDictionary = new();

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        internal void Rent()
        {
            disposed = false;
            Clear();
        }

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            Clear();
            ObjectPool<PooledDictionary<TKey, TValue>>.Return(this);
        }

        public TValue this[TKey key]
        {
            [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            get => innerDictionary[key];
            [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            set => innerDictionary[key] = value;
        }

        public ICollection<TKey> Keys => innerDictionary.Keys;

        public ICollection<TValue> Values => innerDictionary.Values;

        IEnumerable<TKey> IReadOnlyDictionary<TKey, TValue>.Keys => ((IReadOnlyDictionary<TKey, TValue>)innerDictionary).Keys;

        IEnumerable<TValue> IReadOnlyDictionary<TKey, TValue>.Values => ((IReadOnlyDictionary<TKey, TValue>)innerDictionary).Values;

        public int Count => innerDictionary.Count;

        public bool IsReadOnly => false;

        public void Add(TKey key, TValue value) => innerDictionary.Add(key, value);

        public bool ContainsKey(TKey key) => innerDictionary.ContainsKey(key);

        public bool Remove(TKey key) => innerDictionary.Remove(key);

        public bool TryGetValue(TKey key, out TValue value) => innerDictionary.TryGetValue(key, out value);

        public void Add(KeyValuePair<TKey, TValue> item) => ((IDictionary<TKey, TValue>)innerDictionary).Add(item);

        public void Clear() => innerDictionary.Clear();

        public bool Contains(KeyValuePair<TKey, TValue> item) => ((ICollection<KeyValuePair<TKey, TValue>>)innerDictionary).Contains(item);

        public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex) => ((ICollection<KeyValuePair<TKey, TValue>>)innerDictionary).CopyTo(array, arrayIndex);

        public bool Remove(KeyValuePair<TKey, TValue> item) => ((IDictionary<TKey, TValue>)innerDictionary).Remove(item);

        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() => innerDictionary.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
