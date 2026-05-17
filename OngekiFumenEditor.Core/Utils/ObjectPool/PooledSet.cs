using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace OngekiFumenEditor.Core.Utils.ObjectPool
{
    internal sealed class PooledSet<T> : IPooledSet<T>
    {
        private bool disposed;

        private Collections.Pooled.PooledSet<T> innerSet = new();

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
            innerSet.Dispose();
            ObjectPool<PooledSet<T>>.Return(this);
        }

        public int Count
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => innerSet.Count;
        }

        public bool IsReadOnly
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ((ICollection<T>)innerSet).IsReadOnly;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Add(T item) => innerSet.Add(item);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddRange(IEnumerable<T> items) => innerSet.UnionWith(items);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void ICollection<T>.Add(T item) => innerSet.Add(item);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Clear() => innerSet.Clear();
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Contains(T item) => innerSet.Contains(item);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void CopyTo(T[] array, int arrayIndex) => innerSet.CopyTo(array, arrayIndex);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ExceptWith(IEnumerable<T> other) => innerSet.ExceptWith(other);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IEnumerator<T> GetEnumerator() => innerSet.GetEnumerator();
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void IntersectWith(IEnumerable<T> other) => innerSet.IntersectWith(other);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsProperSubsetOf(IEnumerable<T> other) => innerSet.IsProperSubsetOf(other);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsProperSupersetOf(IEnumerable<T> other) => innerSet.IsProperSupersetOf(other);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsSubsetOf(IEnumerable<T> other) => innerSet.IsSubsetOf(other);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsSupersetOf(IEnumerable<T> other) => innerSet.IsSupersetOf(other);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Overlaps(IEnumerable<T> other) => innerSet.Overlaps(other);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Remove(T item) => innerSet.Remove(item);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool SetEquals(IEnumerable<T> other) => innerSet.SetEquals(other);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SymmetricExceptWith(IEnumerable<T> other) => innerSet.SymmetricExceptWith(other);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void UnionWith(IEnumerable<T> other) => innerSet.UnionWith(other);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
