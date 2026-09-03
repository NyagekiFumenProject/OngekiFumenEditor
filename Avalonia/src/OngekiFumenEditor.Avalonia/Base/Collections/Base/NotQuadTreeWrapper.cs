using System.Collections;
using System.ComponentModel;
using System.Numerics;
using OngekiFumenEditor.Avalonia.Base.Collections.Base.NotQuadTree;

namespace OngekiFumenEditor.Avalonia.Base.Collections.Base
{
    public class NotQuadTreeWrapper<TX, TY, TValue> : IReadOnlyCollection<TValue> where TValue : INotifyPropertyChanged where TX : IDivisionOperators<TX, float, TX>, IAdditionOperators<TX, TX, TX>, ISubtractionOperators<TX, TX, TX>, IComparable<TX>
        where TY : IDivisionOperators<TY, float, TY>, IAdditionOperators<TY, TY, TY>, ISubtractionOperators<TY, TY, TY>, IComparable<TY>
    {
        private NotQuadTree<TX, TY, TValue>? tree;
        private readonly HashSet<string> rebuildProperties;

        private readonly Func<TValue, TX> xStartValueMap;
        private readonly Func<TValue, TY> yStartValueMap;
        private readonly Func<TValue, TX> xEndValueMap;
        private readonly Func<TValue, TY> yEndValueMap;
        private readonly TX minimumWidth;
        private readonly TY minimumHeight;

        private readonly HashSet<TValue> registerObjects = new();
        private readonly object locker = new();

        public IEnumerator<TValue> GetEnumerator()
        {
            CheckAndBuild();
            var currentTree = tree;
            return (currentTree?.TotalValues ?? Enumerable.Empty<TValue>()).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public int Count
        {
            get
            {
                CheckAndBuild();
                return tree?.TotalCount ?? 0;
            }
        }

        public NotQuadTreeWrapper(
            Func<TValue, TX> xStartValueMap,
            Func<TValue, TY> yStartValueMap,
            Func<TValue, TX> xEndValueMap,
            Func<TValue, TY> yEndValueMap,
            TX minimumWidth,
            TY minimumHeight,
            params string[] rebuildProperties)
        {
            this.rebuildProperties = rebuildProperties.ToHashSet(StringComparer.Ordinal);
            this.xStartValueMap = xStartValueMap;
            this.yStartValueMap = yStartValueMap;
            this.xEndValueMap = xEndValueMap;
            this.yEndValueMap = yEndValueMap;
            this.minimumWidth = minimumWidth;
            this.minimumHeight = minimumHeight;
        }

        public void Add(TValue obj)
        {
            lock (locker)
            {
                if (!registerObjects.Add(obj))
                    return;

                obj.PropertyChanged += OnItemPropChanged;
                tree = null;
            }
        }

        private void OnItemPropChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is not string propertyName || !rebuildProperties.Contains(propertyName))
                return;

            lock (locker)
            {
                if (sender is TValue value && registerObjects.Contains(value))
                    tree = null;
            }
        }

        public void Remove(TValue obj)
        {
            lock (locker)
            {
                if (!registerObjects.Remove(obj))
                    return;

                obj.PropertyChanged -= OnItemPropChanged;
                tree = null;
            }
        }

        public IEnumerable<TValue> Query(TX x, TY y)
        {
            CheckAndBuild();
            return tree?.Query(x, y) ?? Enumerable.Empty<TValue>();
        }

        private void CheckAndBuild()
        {
            if (tree is not null)
                return;

            lock (locker)
            {
                if (tree is not null || registerObjects.Count == 0)
                    return;

                var boundedObjects = new List<NotQuadTree<TX, TY, TValue>.BoundedObject>(registerObjects.Count);
                var hasBounds = false;
                TX minX = default!;
                TX maxX = default!;
                TY minY = default!;
                TY maxY = default!;

                foreach (var obj in registerObjects)
                {
                    var bounded = new NotQuadTree<TX, TY, TValue>.BoundedObject(
                        obj,
                        xStartValueMap(obj),
                        yStartValueMap(obj),
                        xEndValueMap(obj),
                        yEndValueMap(obj));
                    boundedObjects.Add(bounded);

                    if (!hasBounds)
                    {
                        minX = bounded.StartX;
                        maxX = bounded.EndX;
                        minY = bounded.StartY;
                        maxY = bounded.EndY;
                        hasBounds = true;
                        continue;
                    }

                    if (bounded.StartX.CompareTo(minX) < 0)
                        minX = bounded.StartX;
                    if (bounded.EndX.CompareTo(maxX) > 0)
                        maxX = bounded.EndX;
                    if (bounded.StartY.CompareTo(minY) < 0)
                        minY = bounded.StartY;
                    if (bounded.EndY.CompareTo(maxY) > 0)
                        maxY = bounded.EndY;
                }

                if (!hasBounds)
                    return;

                var width = maxX - minX;
                if (width.CompareTo(minimumWidth) < 0)
                    width = minimumWidth;
                var height = maxY - minY;
                if (height.CompareTo(minimumHeight) < 0)
                    height = minimumHeight;

                var builtTree = new NotQuadTree<TX, TY, TValue>(
                    new NotQuadTree<TX, TY, TValue>.Rectangle(minX, minY, width, height),
                    xStartValueMap,
                    yStartValueMap,
                    xEndValueMap,
                    yEndValueMap,
                    level: 0);
                builtTree.Build(boundedObjects);
                tree = builtTree;
            }
        }

        public string? DebugFindDataQueryPath(TValue data)
        {
            CheckAndBuild();
            return tree?.DebugFindDataQueryPath(data);
        }

        public void DebugDump()
        {
            CheckAndBuild();
            tree?.DebugDump();
        }
    }
}

