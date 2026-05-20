using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace OngekiFumenEditor.Core.Utils.ObjectPool
{
    internal sealed class PooledList<T> : IPooledList<T>
    {
        private bool disposed;

        private Collections.Pooled.PooledList<T> innerList = new();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void Rent()
        {
            Clear();
            disposed = false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            Clear();
            innerList.Dispose();
            ObjectPool<PooledList<T>>.Return(this);
        }

        public T this[int index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => innerList[index];
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set => innerList[index] = value;
        }

        public int Count
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => innerList.Count;
        }

        public bool IsReadOnly
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ((ICollection<T>)innerList).IsReadOnly;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add(T item) => innerList.Add(item);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddRange(IEnumerable<T> items) => innerList.AddRange(items);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Clear() => innerList.Clear();
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Contains(T item) => innerList.Contains(item);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void CopyTo(T[] array, int arrayIndex) => ((ICollection<T>)innerList).CopyTo(array, arrayIndex);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Sort(IComparer<T> comparer) => innerList.Sort(comparer);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IEnumerator<T> GetEnumerator() => innerList.GetEnumerator();
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int IndexOf(T item) => innerList.IndexOf(item);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Insert(int index, T item) => innerList.Insert(index, item);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Remove(T item) => innerList.Remove(item);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RemoveAt(int index) => innerList.RemoveAt(index);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
