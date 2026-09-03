using System.Numerics;
using OngekiFumenEditor.Avalonia.Utils;

namespace OngekiFumenEditor.Avalonia.Base.Collections.Base.NotQuadTree
{
    public class NotQuadTree<TX, TY, TData> where TX : IDivisionOperators<TX, float, TX>
        , IAdditionOperators<TX, TX, TX>
        , ISubtractionOperators<TX, TX, TX>
        , IComparable<TX>
        where TY : IDivisionOperators<TY, float, TY>
        , IAdditionOperators<TY, TY, TY>
        , ISubtractionOperators<TY, TY, TY>
        , IComparable<TY>
    {
        public class Rectangle
        {
            public TX X { get; }
            public TY Y { get; }
            public TX Width { get; }
            public TY Height { get; }

            public TX CenterX => X + Width / 2;
            public TY CenterY => Y + Height / 2;
            public TX HalfWidth => Width / 2;
            public TY HalfHeight => Height / 2;

            public Rectangle(TX x, TY y, TX width, TY height)
            {
                X = x;
                Y = y;
                Width = width;
                Height = height;
            }

            public override string ToString() => $"({X}, {Y}) ({X + Width}, {Y + Height})";
        }

        private const int DefaultMaxObjects = 8;
        private const int MaxDepth = 8;

        private readonly int maxObjects;
        private readonly int level;
        private readonly Rectangle bounds;
        private readonly Func<TData, TX> xStartValueMap;
        private readonly Func<TData, TY> yStartValueMap;
        private readonly Func<TData, TX> xEndValueMap;
        private readonly Func<TData, TY> yEndValueMap;
        private readonly NotQuadTree<TX, TY, TData>?[] childTrees = new NotQuadTree<TX, TY, TData>?[4];
        private readonly List<BoundedObject> objects = new();

        internal readonly struct BoundedObject : IBounded<TX, TY, TData>
        {
            public TData Data { get; }
            public TX X { get; }
            public TY Y { get; }
            public TX Width { get; }
            public TY Height { get; }

            public TX StartX => X;
            public TX EndX => X + Width;
            public TY StartY => Y;
            public TY EndY => Y + Height;

            public BoundedObject(TData data, TX startX, TY startY, TX endX, TY endY)
            {
                if (startX.CompareTo(endX) <= 0)
                {
                    X = startX;
                    Width = endX - startX;
                }
                else
                {
                    X = endX;
                    Width = startX - endX;
                }

                if (startY.CompareTo(endY) <= 0)
                {
                    Y = startY;
                    Height = endY - startY;
                }
                else
                {
                    Y = endY;
                    Height = startY - endY;
                }

                Data = data;
            }

            public override string ToString() => $"({StartX}, {StartY}) ({EndX}, {EndY})";
        }

        public NotQuadTree(Rectangle bounds,
            Func<TData, TX> xStartValueMap, Func<TData, TY> yStartValueMap,
            Func<TData, TX> xEndValueMap, Func<TData, TY> yEndValueMap,
            int maxObjects = DefaultMaxObjects, int level = 0)
        {
            this.bounds = bounds;
            this.xStartValueMap = xStartValueMap;
            this.yStartValueMap = yStartValueMap;
            this.xEndValueMap = xEndValueMap;
            this.yEndValueMap = yEndValueMap;
            this.maxObjects = maxObjects > 0 ? maxObjects : DefaultMaxObjects;
            this.level = level;
        }

        public void Build(IEnumerable<TData> dataList)
        {
            var boundedObjects = new List<BoundedObject>();
            foreach (var data in dataList)
            {
                boundedObjects.Add(new BoundedObject(
                    data,
                    xStartValueMap(data),
                    yStartValueMap(data),
                    xEndValueMap(data),
                    yEndValueMap(data)));
            }

            Build(boundedObjects);
        }

        private int CalculateQuadrant(TX x, TY y)
        {
            if (x.CompareTo(bounds.CenterX) <= 0 && y.CompareTo(bounds.CenterY) >= 0)
                return 0;

            if (x.CompareTo(bounds.CenterX) >= 0 && y.CompareTo(bounds.CenterY) >= 0)
                return 1;

            if (x.CompareTo(bounds.CenterX) <= 0 && y.CompareTo(bounds.CenterY) <= 0)
                return 2;

            return 3;
        }

        private int? TryGetContainingQuadrant(BoundedObject data)
        {
            var quadrant = CalculateQuadrant(data.StartX, data.StartY);
            if (quadrant != CalculateQuadrant(data.EndX, data.StartY)
                || quadrant != CalculateQuadrant(data.StartX, data.EndY)
                || quadrant != CalculateQuadrant(data.EndX, data.EndY))
            {
                return null;
            }

            return quadrant;
        }

        internal void Build(IReadOnlyList<BoundedObject> dataList)
        {
            if (dataList.Count <= maxObjects || level >= MaxDepth)
            {
                for (var i = 0; i < dataList.Count; i++)
                    objects.Add(dataList[i]);

                UpdateCounts();
                return;
            }

            var childDataLists = new List<BoundedObject>?[4];
            for (var i = 0; i < dataList.Count; i++)
            {
                var data = dataList[i];
                if (TryGetContainingQuadrant(data) is int quadrant)
                {
                    (childDataLists[quadrant] ??= new List<BoundedObject>()).Add(data);
                }
                else
                {
                    objects.Add(data);
                }
            }

            for (var quadrant = 0; quadrant < childDataLists.Length; quadrant++)
            {
                var childDataList = childDataLists[quadrant];
                if (childDataList is null)
                    continue;

                var childBounds = quadrant switch
                {
                    //第二象限
                    0 => new Rectangle(bounds.X, bounds.CenterY, bounds.HalfWidth, bounds.HalfHeight),
                    //第一象限
                    1 => new Rectangle(bounds.CenterX, bounds.CenterY, bounds.HalfWidth, bounds.HalfHeight),
                    //第三象限
                    2 => new Rectangle(bounds.X, bounds.Y, bounds.HalfWidth, bounds.HalfHeight),
                    //第四象限
                    _ => new Rectangle(bounds.CenterX, bounds.Y, bounds.HalfWidth, bounds.HalfHeight),
                };

                var childTree = childTrees[quadrant] = new NotQuadTree<TX, TY, TData>(
                    childBounds,
                    xStartValueMap,
                    yStartValueMap,
                    xEndValueMap,
                    yEndValueMap,
                    maxObjects,
                    level + 1);
                childTree.Build(childDataList);
            }

            UpdateCounts();
        }

        private void UpdateCounts()
        {
            LocalCount = objects.Count;
            var totalCount = LocalCount;
            for (var i = 0; i < childTrees.Length; i++)
                totalCount += childTrees[i]?.TotalCount ?? 0;
            TotalCount = totalCount;
        }

        private static bool CheckInBound(BoundedObject bounded, TX x, TY y)
        {
            if (x.CompareTo(bounded.StartX) < 0 || x.CompareTo(bounded.EndX) > 0)
                return false;
            if (y.CompareTo(bounded.StartY) < 0 || y.CompareTo(bounded.EndY) > 0)
                return false;
            return true;
        }

        public IEnumerable<TData> Query(TX x, TY y)
        {
            //如果不在当前树范围内，直接返回
            if (x.CompareTo(bounds.X) < 0 || x.CompareTo(bounds.X + bounds.Width) > 0)
                yield break;
            if (y.CompareTo(bounds.Y) < 0 || y.CompareTo(bounds.Y + bounds.Height) > 0)
                yield break;

            for (var i = 0; i < objects.Count; i++)
            {
                var bound = objects[i];
                if (CheckInBound(bound, x, y))
                    yield return bound.Data;
            }

            var quadrant = CalculateQuadrant(x, y);
            var childTree = childTrees[quadrant];
            if (childTree is null)
                yield break;

            foreach (var data in childTree.Query(x, y))
                yield return data;
        }

        public int LocalCount { get; private set; }
        public int TotalCount { get; private set; }

        public IEnumerable<TData> TotalValues
        {
            get
            {
                for (var i = 0; i < objects.Count; i++)
                    yield return objects[i].Data;

                for (var i = 0; i < childTrees.Length; i++)
                {
                    var childTree = childTrees[i];
                    if (childTree is null)
                        continue;

                    foreach (var data in childTree.TotalValues)
                        yield return data;
                }
            }
        }

        public override string ToString() =>
            $"Bound:{bounds} Locals:{LocalCount} Children:{childTrees[0]?.TotalCount ?? 0}/{childTrees[1]?.TotalCount ?? 0}/{childTrees[2]?.TotalCount ?? 0}/{childTrees[3]?.TotalCount ?? 0}";

        internal void DebugDump(int tabLength = 0)
        {
            var tabContent = new string(' ', tabLength * 2);
            void output(string content) => Console.WriteLine($"{tabContent}{content}");

            output($"Dumping NotQuadTree at level {level} with bounds {bounds}");
            output($"Local Count: {LocalCount}, Total Count: {TotalCount}");
            output("Local Objects:");
            for (var i = 0; i < objects.Count; i++)
            {
                var obj = objects[i];
                output($"* {obj} {obj.Data}");
            }

            for (var i = 0; i < childTrees.Length; i++)
            {
                var childTree = childTrees[i];
                if (childTree is null)
                    continue;

                output($"Child Tree {i}:");
                childTree.DebugDump(tabLength + 1);
            }
        }

        public string? DebugFindDataQueryPath(TData value)
        {
            var queryList = new Stack<string>();
            if (DebugFindDataQueryPathInternal(value, queryList))
                return string.Concat(queryList.Reverse());
            return null;
        }

        private bool DebugFindDataQueryPathInternal(TData value, Stack<string> pathStack)
        {
            for (var i = 0; i < objects.Count; i++)
            {
                if (EqualityComparer<TData>.Default.Equals(objects[i].Data, value))
                {
                    pathStack.Push("X");
                    return true;
                }
            }

            for (var i = 0; i < childTrees.Length; i++)
            {
                pathStack.Push(i switch
                {
                    0 => "↖",
                    1 => "↗",
                    2 => "↙",
                    _ => "↘",
                });

                var childTree = childTrees[i];
                if (childTree is not null && childTree.DebugFindDataQueryPathInternal(value, pathStack))
                    return true;

                pathStack.Pop();
            }

            return false;
        }
    }
}

