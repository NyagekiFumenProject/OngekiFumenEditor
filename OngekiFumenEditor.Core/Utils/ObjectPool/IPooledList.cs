using System;
using System.Collections.Generic;

namespace OngekiFumenEditor.Core.Utils.ObjectPool
{
    public interface IPooledList<T> : IList<T>, IReadOnlyList<T>, IDisposable
    {
        new T this[int index] { get; set; }
        new int Count { get; }
        void AddRange(IEnumerable<T> items);
    }
}
