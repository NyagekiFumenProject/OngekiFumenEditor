using System;
using System.Collections.Generic;

namespace OngekiFumenEditor.Avalonia.Utils.ObjectPool
{
    public interface IPooledSet<T> : ISet<T>, IReadOnlyCollection<T>, IDisposable
    {
        new int Count { get; }
        void AddRange(IEnumerable<T> items);
    }
}
