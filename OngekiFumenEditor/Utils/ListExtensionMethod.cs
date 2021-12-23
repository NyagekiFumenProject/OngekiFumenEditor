using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Utils
{
    public static class ListExtensionMethod
    {
        public static void Sort<T>(this List<T> list, Func<T, T, int> compFunc) => list.Sort(new ComparerWrapper<T>(compFunc));
        public static void SortBy<T,X>(this List<T> list, Func<T, X> keySelect) => list.Sort((a,b) => Comparer<X>.Default.Compare(keySelect(a), keySelect(b)));
    }
}
