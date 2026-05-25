using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace OngekiFumenEditor.Base.Collections.Base
{
    public class QuadTreeWrapper<TValue> : IReadOnlyCollection<TValue> where TValue : INotifyPropertyChanged
    {
        private readonly HashSet<string> rebuildProperties;
        private readonly Func<TValue, float> xStartValueMap;
        private readonly Func<TValue, float> yStartValueMap;
        private readonly Func<TValue, float> xEndValueMap;
        private readonly Func<TValue, float> yEndValueMap;
        private readonly HashSet<TValue> registerObjects = new();
        private readonly object locker = new();

        private QuadTree<TValue> tree;

        public QuadTreeWrapper(
            Func<TValue, float> xStartValueMap,
            Func<TValue, float> yStartValueMap,
            Func<TValue, float> xEndValueMap,
            Func<TValue, float> yEndValueMap,
            params string[] rebuildProperties)
        {
            this.xStartValueMap = xStartValueMap;
            this.yStartValueMap = yStartValueMap;
            this.xEndValueMap = xEndValueMap;
            this.yEndValueMap = yEndValueMap;
            this.rebuildProperties = rebuildProperties.ToHashSet();
        }

        public int Count
        {
            get
            {
                CheckAndBuild();
                return tree?.TotalCount ?? 0;
            }
        }

        public IEnumerator<TValue> GetEnumerator()
        {
            CheckAndBuild();
            return (tree?.TotalValues ?? Enumerable.Empty<TValue>()).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public void Add(TValue obj)
        {
            tree = null;
            registerObjects.Add(obj);
            obj.PropertyChanged += OnItemPropChanged;
        }

        public void Remove(TValue obj)
        {
            if (!registerObjects.Remove(obj))
                return;

            tree = null;
            obj.PropertyChanged -= OnItemPropChanged;
        }

        public IEnumerable<TValue> Query(float x, float y)
        {
            if (registerObjects.Count == 0)
                return Enumerable.Empty<TValue>();

            CheckAndBuild();
            return tree.Query(x, y);
        }

        public string DebugFindDataQueryPath(TValue data)
        {
            CheckAndBuild();
            return tree?.DebugFindDataQueryPath(data);
        }

        public void DebugDump()
        {
            CheckAndBuild();
            tree?.DebugDump();
        }

        private void OnItemPropChanged(object sender, PropertyChangedEventArgs e)
        {
            if (rebuildProperties.Contains(e.PropertyName))
                tree = null;
        }

        private void CheckAndBuild()
        {
            if (tree is not null || registerObjects.Count == 0)
                return;

            lock (locker)
            {
                if (tree is not null || registerObjects.Count == 0)
                    return;

                var minX = registerObjects.Min(xStartValueMap);
                var maxX = registerObjects.Max(xEndValueMap);
                var minY = registerObjects.Min(yStartValueMap);
                var maxY = registerObjects.Max(yEndValueMap);

                var width = Math.Max(maxX - minX, 1f);
                var height = Math.Max(maxY - minY, 1f);
                var bounds = new QuadTree<TValue>.Rectangle(minX, minY, width, height);

                var boundedObjects = registerObjects.Select(obj => new QuadTree<TValue>.BoundedObject(
                    obj,
                    xStartValueMap(obj),
                    yStartValueMap(obj),
                    xEndValueMap(obj),
                    yEndValueMap(obj))).ToList();

                var newTree = new QuadTree<TValue>(bounds);
                newTree.Build(boundedObjects);
                tree = newTree;
            }
        }
    }

    internal sealed class QuadTree<TValue>
    {
        internal readonly struct Rectangle
        {
            public float X { get; }
            public float Y { get; }
            public float Width { get; }
            public float Height { get; }

            public float Right => X + Width;
            public float Bottom => Y + Height;
            public float CenterX => X + Width / 2f;
            public float CenterY => Y + Height / 2f;
            public float HalfWidth => Width / 2f;
            public float HalfHeight => Height / 2f;

            public Rectangle(float x, float y, float width, float height)
            {
                X = x;
                Y = y;
                Width = width;
                Height = height;
            }

            public override string ToString() => $"({X}, {Y}) ({Right}, {Bottom})";
        }

        internal readonly struct BoundedObject
        {
            public TValue Data { get; }
            public float StartX { get; }
            public float StartY { get; }
            public float EndX { get; }
            public float EndY { get; }

            public BoundedObject(TValue data, float startX, float startY, float endX, float endY)
            {
                Data = data;
                StartX = Math.Min(startX, endX);
                StartY = Math.Min(startY, endY);
                EndX = Math.Max(startX, endX);
                EndY = Math.Max(startY, endY);
            }

            public override string ToString() => $"({StartX}, {StartY}) ({EndX}, {EndY})";
        }

        private const int DefaultMaxObjects = 8;
        private const int MaxDepth = 8;

        private readonly Rectangle bounds;
        private readonly int level;
        private readonly List<BoundedObject> objects = new();
        private readonly QuadTree<TValue>[] children = new QuadTree<TValue>[4];

        public int LocalCount { get; private set; }
        public int TotalCount { get; private set; }
        public IEnumerable<TValue> TotalValues => objects.Select(x => x.Data).Concat(children.Where(x => x is not null).SelectMany(x => x.TotalValues));

        public QuadTree(Rectangle bounds, int level = 0)
        {
            this.bounds = bounds;
            this.level = level;
        }

        public void Build(List<BoundedObject> source)
        {
            if (source.Count <= DefaultMaxObjects || level >= MaxDepth)
            {
                objects.AddRange(source);
                RefreshCount();
                return;
            }

            var childLists = new List<BoundedObject>[4];

            foreach (var data in source)
            {
                var quadrant = TryGetQuadrant(data);
                if (quadrant is null)
                {
                    objects.Add(data);
                    continue;
                }

                childLists[quadrant.Value] ??= new List<BoundedObject>();
                childLists[quadrant.Value].Add(data);
            }

            for (var i = 0; i < childLists.Length; i++)
            {
                var childList = childLists[i];
                if (childList is null || childList.Count == 0)
                    continue;

                var childTree = new QuadTree<TValue>(CreateChildBounds(i), level + 1);
                childTree.Build(childList);
                children[i] = childTree;
            }

            RefreshCount();
        }

        public IEnumerable<TValue> Query(float x, float y)
        {
            if (!Contains(bounds, x, y))
                yield break;

            foreach (var obj in objects)
            {
                if (Contains(obj, x, y))
                    yield return obj.Data;
            }

            var child = children[CalculateQuadrant(x, y)];
            if (child is null)
                yield break;

            foreach (var item in child.Query(x, y))
                yield return item;
        }

        public void DebugDump(int tabLength = 0)
        {
            var tabContent = new string(' ', tabLength * 2);
            void Output(string content) => Console.WriteLine($"{tabContent}{content}");

            Output($"Dumping QuadTree at level {level} with bounds {bounds}");
            Output($"Local Count: {LocalCount}, Total Count: {TotalCount}");
            Output("Local Objects:");
            foreach (var obj in objects)
                Output($"* {obj} {obj.Data}");

            for (var i = 0; i < children.Length; i++)
            {
                if (children[i] is null)
                    continue;

                Output($"Child Tree {i}:");
                children[i].DebugDump(tabLength + 1);
            }
        }

        public string DebugFindDataQueryPath(TValue value)
        {
            var pathStack = new Stack<string>();
            if (DebugFindDataQueryPathInternal(value, pathStack))
                return string.Concat(pathStack.Reverse());
            return null;
        }

        private bool DebugFindDataQueryPathInternal(TValue value, Stack<string> pathStack)
        {
            if (objects.Any(x => Equals(x.Data, value)))
            {
                pathStack.Push("X");
                return true;
            }

            for (var i = 0; i < children.Length; i++)
            {
                pathStack.Push(i switch
                {
                    0 => "NW",
                    1 => "NE",
                    2 => "SW",
                    _ => "SE",
                });

                if (children[i]?.DebugFindDataQueryPathInternal(value, pathStack) == true)
                    return true;

                pathStack.Pop();
            }

            return false;
        }

        private void RefreshCount()
        {
            LocalCount = objects.Count;
            TotalCount = LocalCount + children.Where(x => x is not null).Sum(x => x.TotalCount);
        }

        private int? TryGetQuadrant(BoundedObject data)
        {
            var left = data.EndX <= bounds.CenterX;
            var right = data.StartX >= bounds.CenterX;
            var bottom = data.EndY <= bounds.CenterY;
            var top = data.StartY >= bounds.CenterY;

            if (left)
            {
                if (top)
                    return 0;
                if (bottom)
                    return 2;
            }
            else if (right)
            {
                if (top)
                    return 1;
                if (bottom)
                    return 3;
            }

            return null;
        }

        private int CalculateQuadrant(float x, float y)
        {
            if (x <= bounds.CenterX && y >= bounds.CenterY)
                return 0;
            if (x >= bounds.CenterX && y >= bounds.CenterY)
                return 1;
            if (x <= bounds.CenterX && y <= bounds.CenterY)
                return 2;
            return 3;
        }

        private Rectangle CreateChildBounds(int quadrant)
        {
            return quadrant switch
            {
                0 => new Rectangle(bounds.X, bounds.CenterY, bounds.HalfWidth, bounds.HalfHeight),
                1 => new Rectangle(bounds.CenterX, bounds.CenterY, bounds.HalfWidth, bounds.HalfHeight),
                2 => new Rectangle(bounds.X, bounds.Y, bounds.HalfWidth, bounds.HalfHeight),
                _ => new Rectangle(bounds.CenterX, bounds.Y, bounds.HalfWidth, bounds.HalfHeight),
            };
        }

        private static bool Contains(Rectangle rectangle, float x, float y)
        {
            return x >= rectangle.X && x <= rectangle.Right && y >= rectangle.Y && y <= rectangle.Bottom;
        }

        private static bool Contains(BoundedObject data, float x, float y)
        {
            return x >= data.StartX && x <= data.EndX && y >= data.StartY && y <= data.EndY;
        }
    }
}
