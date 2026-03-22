using OngekiFumenEditor.Utils;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Numerics;

namespace OngekiFumenEditor.Base.Collections.Base.NotQuadTree
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

            public override string ToString()
            {
                return $"({X}, {Y}) ({X + Width}, {Y + Height})";
            }
        }

        private const int DefaultMaxObjects = 10;

        private readonly int maxObjects;
        private readonly int level;

        private readonly Rectangle bounds;
        private readonly Func<TData, TX> xStartValueMap;
        private readonly Func<TData, TY> yStartValueMap;
        private readonly Func<TData, TX> xEndValueMap;
        private readonly Func<TData, TY> yEndValueMap;

        private NotQuadTree<TX, TY, TData>[] childTrees = new NotQuadTree<TX, TY, TData>[4];
        private readonly List<BoundedObject> objects = new();

        private struct BoundedObject : IBounded<TX, TY, TData>
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

            public BoundedObject(TData data, TX x, TY y, TX width, TY height)
            {
                Data = data;
                X = x;
                Y = y;
                Width = width;
                Height = height;
            }

            public override string ToString()
            {
                return $"({StartX}, {StartY}) ({EndX}, {EndY})";
            }
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
            this.maxObjects = maxObjects;
            this.level = level;
        }

        public void Build(IEnumerable<TData> dataList)
        {
            var bounds = dataList.Select(data =>
            {
                var endX = xEndValueMap(data);
                var endY = yEndValueMap(data);
                var startY = yStartValueMap(data);
                var startX = xStartValueMap(data);

                return new BoundedObject(data, startX, startY, endX - startX, endY - startY);
            }).ToList();

            Build(bounds);
        }

        private int CalculateQuadrant(TX x, TY y)
        {
            if (x.CompareTo(bounds.CenterX) <= 0 && y.CompareTo(bounds.CenterY) >= 0)
            {
                return 0;
            }
            else if (x.CompareTo(bounds.CenterX) >= 0 && y.CompareTo(bounds.CenterY) >= 0)
            {
                return 1;
            }
            else if (x.CompareTo(bounds.CenterX) <= 0 && y.CompareTo(bounds.CenterY) <= 0)
            {
                return 2;
            }

            return 3;
        }

        private void Build(List<BoundedObject> dataList)
        {
            if (dataList.Count < maxObjects)
            {
                objects.AddRange(dataList);
            }
            else
            {
                var childDataList = new List<BoundedObject>[4];

                foreach (var data in dataList)
                {
                    int? beforeQuadrant = default;

                    foreach (var (x, y) in new[]{
                    (data.X,data.Y),
                    (data.X+ data.Width, data.Y),
                    (data.X, data.Y+data.Height),
                    (data.X+ data.Width, data.Y+data.Height),
                })
                    {
                        var curQuadrant = CalculateQuadrant(x, y);

                        if (beforeQuadrant is int bq)
                        {
                            //涓嶅湪鍚屼竴涓薄闄?
                            if (curQuadrant != bq)
                            {
                                beforeQuadrant = null;
                                break;//璺ㄨ薄闄愪簡锛屼笉闇€瑕佸垎閰嶅埌瀛愭爲浜?
                            }
                        }
                        else
                        {
                            beforeQuadrant = curQuadrant;
                        }
                    }

                    if (beforeQuadrant is int quadrant)
                    {
                        var childrenList = childDataList[quadrant] ?? (childDataList[quadrant] = new List<BoundedObject>());

                        childrenList.Add(data);
                    }
                    else
                    {
                        objects.Add(data);
                    }
                }

                //鍒嗛厤鍒板瓙鏍?
                for (int quadrant = 0; quadrant < 4; quadrant++)
                {
                    var children = childDataList[quadrant];
                    if (children is null)
                        continue;

                    var rect = quadrant switch
                    {
                        //绗簩璞￠檺
                        0 => new Rectangle(bounds.X, bounds.CenterY, bounds.HalfWidth, bounds.HalfHeight),
                        //绗竴璞￠檺
                        1 => new Rectangle(bounds.CenterX, bounds.CenterY, bounds.HalfWidth, bounds.HalfHeight),
                        //绗笁璞￠檺
                        2 => new Rectangle(bounds.X, bounds.Y, bounds.HalfWidth, bounds.HalfHeight),
                        //绗洓璞￠檺
                        _ => new Rectangle(bounds.CenterX, bounds.Y, bounds.HalfWidth, bounds.HalfHeight),
                    };

                    var tree = childTrees[quadrant] = new NotQuadTree<TX, TY, TData>(rect, xStartValueMap, yStartValueMap, xEndValueMap, yEndValueMap, maxObjects, level + 1);

                    tree.Build(children);
                }
            }

            LocalCount = objects.Count;
            TotalCount = LocalCount + childTrees.Sum(n => n?.TotalCount ?? 0);

            //todo 浼樺寲涓€涓嬶紝鏋佺鎯呭喌涓嬩細鍑虹幇涓€鏉″緢闀垮緢闀块摼鏉＄殑鏍戞灊
            //瑁佸壀
            for (int quadrant = 0; quadrant < 4; quadrant++)
            {
                if (childTrees[quadrant] is NotQuadTree<TX, TY, TData> childTree)
                {
                    var nonNullChildren = childTree.childTrees.Where(x => x is not null).ToArray(); if (nonNullChildren.Length == 1)
                    {
                        //childTrees[quadrant] = onlyTree;
                    }
                }
            }
        }

        private static bool CheckInBound(BoundedObject bounded, TX x, TY y)
        {
            if (x.CompareTo(bounded.X) < 0 || x.CompareTo(bounded.X + bounded.Width) > 0)
                return false;
            if (y.CompareTo(bounded.Y) < 0 || y.CompareTo(bounded.Y + bounded.Height) > 0)
                return false;
            return true;
        }

        public IEnumerable<TData> Query(TX x, TY y)
        {
            //濡傛灉涓嶅湪褰撳墠鏍戣寖鍥村唴锛岀洿鎺ヨ繑鍥?
            if (x.CompareTo(bounds.X) < 0 || x.CompareTo(bounds.X + bounds.Width) > 0)
                yield break;
            if (y.CompareTo(bounds.Y) < 0 || y.CompareTo(bounds.Y + bounds.Height) > 0)
                yield break;

            foreach (var bound in objects)
            {
                if (CheckInBound(bound, x, y))
                    yield return bound.Data;
            }

            var quadrant = CalculateQuadrant(x, y);

            if (childTrees[quadrant] is NotQuadTree<TX, TY, TData> childTree)
            {
                foreach (var data in childTree.Query(x, y))
                    yield return data;
            }
        }

        public int LocalCount { get; private set; } = 0;
        public int TotalCount { get; private set; } = 0;

        public IEnumerable<TData> TotalValues => objects.Select(n => n.Data).Concat(childTrees.SelectMany(n => n?.TotalValues ?? Enumerable.Empty<TData>()));

        public override string ToString()
        {
            return $"Bound:{bounds} Locals:{LocalCount} Children:{childTrees[0]?.TotalCount ?? 0}/{childTrees[1]?.TotalCount ?? 0}/{childTrees[2]?.TotalCount ?? 0}/{childTrees[3]?.TotalCount ?? 0}";
        }

        internal void DebugDump(int tabLength = 0)
        {
            var tabContent = new string(' ', tabLength * 2);
            void output(string content)
            {
                Console.WriteLine($"{tabContent}{content}");
            }
            output($"Dumping NotQuadTree at level {level} with bounds {bounds}");
            output($"Local Count: {LocalCount}, Total Count: {TotalCount}");
            output("Local Objects:");
            foreach (var obj in objects)
            {
                output($"* {obj} {obj.Data}");
            }
            for (int i = 0; i < childTrees.Length; i++)
            {
                if (childTrees[i] is NotQuadTree<TX, TY, TData> childTree)
                {
                    output($"Child Tree {i}:");
                    childTree.DebugDump(tabLength + 1);
                }
            }
        }

        public string DebugFindDataQueryPath(TData value)
        {
            var queryList = new Stack<string>();
            if (DebugFindDataQueryPathInternal(value, queryList))
                return string.Concat(queryList.Reverse());
            return null;
        }

        private bool DebugFindDataQueryPathInternal(TData value, Stack<string> pathStack)
        {
            if (objects.Select(x => x.Data).Contains(value))
            {
                pathStack.Push("X");
                return true;
            }

            for (int i = 0; i < childTrees.Length; i++)
            {
                pathStack.Push(i switch
                {
                    0 => "NW",
                    1 => "NE",
                    2 => "SW",
                    3 => "SE",
                });

                if (childTrees[i] != null)
                {
                    if (childTrees[i].DebugFindDataQueryPathInternal(value, pathStack))
                        return true;
                }

                pathStack.Pop();
            }

            return false;
        }
    }
}
