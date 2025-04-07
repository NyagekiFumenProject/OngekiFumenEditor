using FontStashSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Documents;
using static System.Windows.Forms.AxHost;
using System.Windows.Media.Media3D;

namespace OngekiFumenEditor.Base.Collections.Base.QuadTree
{
    public class QuadTreeCore<TX, TY, TData> where TX : IDivisionOperators<TX, float, TX>, IAdditionOperators<TX, TX, TX>, ISubtractionOperators<TX, TX, TX>
        where TY : IDivisionOperators<TY, float, TY>, IAdditionOperators<TY, TY, TY>, ISubtractionOperators<TY, TY, TY>
    {
        public class Rectangle
        {
            public TX X { get; }
            public TY Y { get; }
            public TX Width { get; }
            public TY Height { get; }
            public Rectangle(TX x, TY y, TX width, TY height)
            {
                X = x;
                Y = y;
                Width = width;
                Height = height;
            }
        }

        private const int DefaultMaxObjects = 10;
        private const int DefaultMaxLevels = 5;

        private readonly int maxObjects;
        private readonly int maxLevels;
        private readonly int level;

        private readonly Rectangle bounds;
        private readonly Func<TData, TX> xStartValueMap;
        private readonly Func<TData, TY> yStartValueMap;
        private readonly Func<TData, TX> xEndValueMap;
        private readonly Func<TData, TY> yEndValueMap;
        private List<QuadTreeCore<TX, TY, TData>> nodes = new();
        private readonly List<BoundedObject> objects = new();

        public IEnumerable<TData> Values => nodes.SelectMany(n => n.Values).Concat(objects.Select(o => o.Data));
        public int Count => nodes.Select(n => n.Count).Sum() + objects.Count;

        private struct BoundedObject : IBounded<TX, TY, TData>
        {
            public TData Data { get; }

            public TX X { get; }

            public TY Y { get; }

            public TX Width { get; }

            public TY Height { get; }

            public BoundedObject(TData data, TX x, TY y, TX width, TY height)
            {
                Data = data;
                X = x;
                Y = y;
                Width = width;
                Height = height;
            }
        }

        public QuadTreeCore(Rectangle bounds,
            Func<TData, TX> xStartValueMap, Func<TData, TY> yStartValueMap,
            Func<TData, TX> xEndValueMap, Func<TData, TY> yEndValueMap,
            int maxObjects = DefaultMaxObjects, int maxLevels = DefaultMaxLevels, int level = 0)
        {
            this.bounds = bounds;
            this.xStartValueMap = xStartValueMap;
            this.yStartValueMap = yStartValueMap;
            this.xEndValueMap = xEndValueMap;
            this.yEndValueMap = yEndValueMap;
            this.maxObjects = maxObjects;
            this.maxLevels = maxLevels;
            this.level = level;
        }

        /// <summary>
        /// 将四叉树分割为4个子节点
        /// </summary>
        private void Split()
        {
            var subWidth = bounds.Width / 2;
            var subHeight = bounds.Height / 2;
            var x = bounds.X;
            var y = bounds.Y;

            nodes[0] = new QuadTreeCore<TX, TY, TData>(new Rectangle(x + subWidth, y, subWidth, subHeight), xStartValueMap, yStartValueMap, xEndValueMap, yEndValueMap, maxObjects, maxLevels, level + 1);
            nodes[1] = new QuadTreeCore<TX, TY, TData>(new Rectangle(x, y, subWidth, subHeight), xStartValueMap, yStartValueMap, xEndValueMap, yEndValueMap, maxLevels, level + 1);
            nodes[2] = new QuadTreeCore<TX, TY, TData>(new Rectangle(x, y + subHeight, subWidth, subHeight), xStartValueMap, yStartValueMap, xEndValueMap, yEndValueMap, level + 1);
            nodes[3] = new QuadTreeCore<TX, TY, TData>(new Rectangle(x + subWidth, y + subHeight, subWidth, subHeight), xStartValueMap, yStartValueMap, xEndValueMap, yEndValueMap, level + 1);
        }

        private void GetIndexes(IBounded<TX, TY, TData> bounded, List<int> indexes)
        {
            dynamic verticalMidpoint = bounds.X + (bounds.Width / 2);
            dynamic horizontalMidpoint = bounds.Y + (bounds.Height / 2);

            bool topQuadrant = bounded.Y < horizontalMidpoint && bounded.Y + bounded.Height < horizontalMidpoint;
            bool bottomQuadrant = bounded.Y > horizontalMidpoint;

            // 对象可以完全放入左象限
            if (bounded.X < verticalMidpoint && bounded.X + bounded.Width < verticalMidpoint)
            {
                if (topQuadrant)
                {
                    indexes.Add(1);
                }
                else if (bottomQuadrant)
                {
                    indexes.Add(2);
                }
            }
            // 对象可以完全放入右象限
            else if (bounded.X > verticalMidpoint)
            {
                if (topQuadrant)
                {
                    indexes.Add(0);
                }
                else if (bottomQuadrant)
                {
                    indexes.Add(3);
                }
            }

            // 如果对象无法完全放入任何子节点，则留在父节点
            if (indexes.Count == 0)
            {
                indexes.Add(-1);
            }
        }

        private List<int> _cachedIndexList = new List<int>();

        private BoundedObject ConvertToBoundedObject(TData data)
        {
            var startX = xStartValueMap(data);
            var startY = yStartValueMap(data);
            var width = xEndValueMap(data) - startX;
            var height = yEndValueMap(data) - startY;
            return new BoundedObject(data, startX, startY, width, height);
        }

        public void Insert(TData data)
        {
            var bounded = ConvertToBoundedObject(data);
            Insert(bounded);
        }

        private void Insert(BoundedObject bounded)
        {
            _cachedIndexList.Clear();

            if (nodes[0] != null)
            {
                GetIndexes(bounded, _cachedIndexList);

                foreach (var index in _cachedIndexList)
                {
                    if (index != -1)
                    {
                        nodes[index].Insert(bounded);
                        return;
                    }
                }
            }

            objects.Add(bounded);

            if (objects.Count > maxObjects && level < maxLevels)
            {
                if (nodes[0] == null)
                {
                    Split();
                }

                int i = 0;
                while (i < objects.Count)
                {
                    GetIndexes(objects[i], _cachedIndexList);
                    bool removed = false;

                    foreach (var index in _cachedIndexList)
                    {
                        if (index != -1)
                        {
                            nodes[index].Insert(objects[i]);
                            objects.RemoveAt(i);
                            removed = true;
                            break;
                        }
                    }

                    if (!removed)
                    {
                        i++;
                    }
                }
            }
        }

        public IEnumerable<TData> Query(TX x, TY y)
        {
            if (nodes[0] != null)
            {
                var bounded = new BoundedObject(default, x, y, x - x, y - y);
                GetIndexes(bounded, _cachedIndexList);

                foreach (var index in _cachedIndexList)
                {
                    if (index != -1)
                    {
                        foreach (var item in nodes[index].Query(x, y))
                            yield return item;
                    }
                    else
                    {
                        foreach (var item in objects)
                            yield return item.Data;
                    }
                }
            }
            else
            {
                foreach (var item in objects)
                    yield return item.Data;
            }
        }
    }
}
