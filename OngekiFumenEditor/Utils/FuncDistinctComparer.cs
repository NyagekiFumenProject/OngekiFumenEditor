using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Utils
{
    public class FuncDistinctComparer<T> : IEqualityComparer<T>
    {
        private readonly Func<T, T, bool> compFunc;
        public FuncDistinctComparer(Func<T, T, bool> compFunc) => this.compFunc = compFunc;
        public bool Equals(T x, T y) => compFunc(x, y);
        public int GetHashCode([DisallowNull] T obj) => obj.GetHashCode();
    }
}
