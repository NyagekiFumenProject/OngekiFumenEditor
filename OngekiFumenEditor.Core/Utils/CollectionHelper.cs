using System;
using System.Collections.Generic;
using System.Linq;

namespace OngekiFumenEditor.Core.Utils
{
    public static class CollectionHelper
    {
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

