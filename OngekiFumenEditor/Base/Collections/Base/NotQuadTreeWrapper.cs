using OngekiFumenEditor.Base.Collections.Base.NotQuadTree;
using OngekiFumenEditor.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Base.Collections.Base
{
    public class NotQuadTreeWrapper<TX, TY, TValue> : IReadOnlyCollection<TValue> where TValue : INotifyPropertyChanged where TX : IDivisionOperators<TX, float, TX>, IAdditionOperators<TX, TX, TX>, ISubtractionOperators<TX, TX, TX>, IComparable<TX>
        where TY : IDivisionOperators<TY, float, TY>, IAdditionOperators<TY, TY, TY>, ISubtractionOperators<TY, TY, TY>, IComparable<TY>
    {
        private NotQuadTree<TX, TY, TValue> tree;
        private readonly HashSet<string> rebuildProperties;

        private readonly Func<TValue, TX> xStartValueMap;
        private readonly Func<TValue, TY> yStartValueMap;
        private readonly Func<TValue, TX> xEndValueMap;
        private readonly Func<TValue, TY> yEndValueMap;

        private HashSet<TValue> registerObjects = new();

        private object locker = new();

        public IEnumerator<TValue> GetEnumerator() => tree.TotalValues.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        public int Count => tree.TotalCount;

        public NotQuadTreeWrapper(Func<TValue, TX> xStartValueMap, Func<TValue, TY> yStartValueMap,
            Func<TValue, TX> xEndValueMap, Func<TValue, TY> yEndValueMap, params string[] rebuildProperties)
        {
            this.rebuildProperties = rebuildProperties.ToHashSet();
            this.xStartValueMap = xStartValueMap;
            this.yStartValueMap = yStartValueMap;
            this.xEndValueMap = xEndValueMap;
            this.yEndValueMap = yEndValueMap;
        }

        public void Add(TValue obj)
        {
            tree = default;
            registerObjects.Add(obj);

            obj.PropertyChanged += OnItemPropChanged;
        }

        private void OnItemPropChanged(object sender, PropertyChangedEventArgs e)
        {
            if (rebuildProperties.Contains(e.PropertyName))
            {
                tree = default;
            }
        }

        public void Remove(TValue obj)
        {
            tree = default;
            obj.PropertyChanged -= OnItemPropChanged;
        }

        public IEnumerable<TValue> Query(TX x, TY y)
        {
            if (registerObjects.Count == 0)
                return Enumerable.Empty<TValue>();

            if (tree == null)
            {
                lock (locker)
                {
                    if (tree == null)
                    {
                        //rebuild quadtree
                        var minX = registerObjects.Min(xStartValueMap);
                        var maxX = registerObjects.Max(xEndValueMap);
                        var minY = registerObjects.Min(yStartValueMap);
                        var maxY = registerObjects.Max(yEndValueMap);

                        var rect = new NotQuadTree<TX, TY, TValue>.Rectangle(minX, minY, maxX - minX, maxY - minY);

                        var tree = new NotQuadTree<TX, TY, TValue>(rect, xStartValueMap, yStartValueMap, xEndValueMap, yEndValueMap, 0);
                        tree.Build(registerObjects);

                        this.tree = tree;
                    }
                }
            }

            return tree.Query(x, y);
        }
    }
}
