using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Utils
{
    internal static class CollectionHelper
    {
        /// <summary>
        /// 合并已排序的集合，假设各个集合均已被从小到大排序
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <typeparam name="X"></typeparam>
        /// <param name="keySelector"></param>
        /// <param name="sortedCollection"></param>
        /// <returns></returns>
        public static IEnumerable<T> MergeTwoSortedCollections<T, X>(Func<T, X> keySelector, IEnumerable<T> sortedA, IEnumerable<T> sortB) where X : IComparable<X>
        {
            var itorA = sortedA.GetEnumerator();
            var itorB = sortB.GetEnumerator();

            var hasA = itorA.MoveNext();
            var hasB = itorB.MoveNext();

            while (hasA && hasB)
            {
                var valA = itorA.Current;
                var valB = itorB.Current;

                if (keySelector(valA).CompareTo(keySelector(valB)) > 0)
                {
                    yield return valA;
                    hasA = itorA.MoveNext();
                }
                else
                {
                    yield return valB;
                    hasB = itorB.MoveNext();
                }
            }

            while (hasA)
            {
                yield return itorA.Current;
                hasA = itorA.MoveNext();
            }

            while (hasB)
            {
                yield return itorB.Current;
                hasB = itorB.MoveNext();
            }
        }

        public static IEnumerable<T> MergeSortedCollections<T, X>(Func<T, X> keySelector, params IEnumerable<T>[] sortedCollections) where X : IComparable<X>
        {
            return sortedCollections.Aggregate((merged, next) => MergeTwoSortedCollections(keySelector, merged, next));
        }
    }
}
