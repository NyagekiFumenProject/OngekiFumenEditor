using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Base.Collections.Base
{
    internal interface IBinaryFindRangeEnumable<T, X> : IEnumerable<T> where X : IComparable<X>
    {
        (int minIndex, int maxIndex) BinaryFindRangeIndex(X min, X max);
        IEnumerable<T> BinaryFindRange(X min, X max);
        bool Contains(T obj);
    }
}
