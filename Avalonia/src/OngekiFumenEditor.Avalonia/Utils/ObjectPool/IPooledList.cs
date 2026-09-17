using System;
using System.Collections.Generic;

namespace OngekiFumenEditor.Avalonia.Utils.ObjectPool
{
    public interface IPooledList<T> : IList<T>, IReadOnlyList<T>, IDisposable
    {
        new T this[int index] { get; set; }
        new int Count { get; }
        void AddRange(IEnumerable<T> items);
        void Sort(IComparer<T> comparer);

        /// <summary>
        /// Ensures the backing buffer can hold <paramref name="capacity"/> items without growing.
        /// Callers that know the final size should call this before a long <see cref="AddRange(IEnumerable{T})"/>
        /// run: the backing <c>Collections.Pooled.PooledList</c> grows by doubling and returns each
        /// intermediate buffer to the array pool while that pool never leases the same array back
        /// mid-operation, so growth is reallocated rather than recycled.
        /// </summary>
        void EnsureCapacity(int capacity);
    }
}
