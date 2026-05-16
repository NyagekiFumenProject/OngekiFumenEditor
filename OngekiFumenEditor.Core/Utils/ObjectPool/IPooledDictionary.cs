using System;
using System.Collections.Generic;

namespace OngekiFumenEditor.Core.Utils.ObjectPool
{
    public interface IPooledDictionary<TKey, TValue> : IDictionary<TKey, TValue>, IReadOnlyDictionary<TKey, TValue>, IDisposable
    {
        new TValue this[TKey key] { get; set; }
        new int Count { get; }
        new bool TryGetValue(TKey key, out TValue value);
    }
}
