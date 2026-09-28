//copy&modify from repo : https://github.com/mbuchetics/RangeTree , LICENSE.txt : https://github.com/mbuchetics/RangeTree/blob/master/LICENSE.txt
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace OngekiFumenEditor.Base.Collections.Base.RangeTree
{
    /// <summary>
    /// 区间索引。写入只改写者私有的 <c>staging</c> 列表并递增版本号，查询时若发现索引落后，
    /// 就按「单飞重建 + 不可变节点 + 原子发布」重建并发布新根。
    /// 旧实现把节点就地清空后重建（<c>IntervalTreeNode.Release</c>），并发读者会解引用被置空的字段；
    /// 并行投射物绘制正是这样的读者（RND-008）。
    /// </summary>
    public class IntervalTree<TKey, TValue> : IIntervalTree<TKey, TValue>
    {
        private readonly object writeGate = new();

        private readonly object rebuildGate = new();

        private List<RangeValuePair<TKey, TValue>> staging = new();

        private IntervalTreeNode<TKey, TValue> root;

        private int version;
        private int syncedVersion;

        private int count;

        private readonly IComparer<TKey> comparer;

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public bool EnableAutoSwapMinMax { get; set; } = false;

        public TKey Max
        {
            get
            {
                EnsureInSync();
                return Volatile.Read(ref root).Max;
            }
        }

        public TKey Min
        {
            get
            {
                EnsureInSync();
                return Volatile.Read(ref root).Min;
            }
        }

        public IEnumerable<TValue> Values
        {
            get
            {
                //拷贝到调用方私有数组：返回值可能在写者继续 Add/Remove 时被枚举，不能把 staging 交出去。
                RangeValuePair<TKey, TValue>[] snapshot;
                lock (writeGate)
                    snapshot = staging.ToArray();

                return snapshot.Select(i => i.Value);
            }
        }

        public int Count => Volatile.Read(ref count);

        /// <summary>
        /// Initializes an empty tree.
        /// </summary>
        public IntervalTree() : this(Comparer<TKey>.Default) { }

        /// <summary>
        /// Initializes an empty tree.
        /// </summary>
        public IntervalTree(IComparer<TKey> comparer)
        {
            this.comparer = comparer ?? Comparer<TKey>.Default;
            root = IntervalTreeNode<TKey, TValue>.BuildTree(Array.Empty<RangeValuePair<TKey, TValue>>(), this.comparer);
        }

        public IEnumerable<TValue> Query(TKey value)
        {
            EnsureInSync();

            return Volatile.Read(ref root).Query(value);
        }

        public IEnumerable<TValue> Query(TKey from, TKey to)
        {
            EnsureInSync();

            return Volatile.Read(ref root).Query(from, to);
        }

        public void QueryInto(TKey from, TKey to, ICollection<TValue> output)
        {
            EnsureInSync();

            Volatile.Read(ref root).QueryInto(from, to, output);
        }

        /// <summary>把索引更新到最新内容；已最新时是一次版本比较（两个 volatile 读），不做任何工作。</summary>
        public void EnsureInSync()
        {
            if (Volatile.Read(ref syncedVersion) != Volatile.Read(ref version))
                RebuildInternal();
        }

        public void Add(TKey from, TKey to, TValue value)
        {
            if (comparer.Compare(from, to) > 0)
            {
                if (EnableAutoSwapMinMax)
                    (to, from) = (from, to);
                else
                    throw new ArgumentOutOfRangeException($"{nameof(from)} cannot be larger than {nameof(to)}");
            }

            lock (writeGate)
            {
                if (staging.Any(x => x.Value.Equals(value)))
                    throw new ArgumentOutOfRangeException($"{value} is exist");

                staging.Add(new RangeValuePair<TKey, TValue>(from, to, value));
                count = staging.Count;
                version++;
            }
        }

        public bool Remove(TValue value)
        {
            lock (writeGate)
            {
                var index = staging.FindIndex(x => x.Value.Equals(value));
                if (index < 0)
                    return false;

                staging.RemoveAt(index);
                count = staging.Count;
                version++;
                return true;
            }
        }

        public void Clear()
        {
            lock (writeGate)
            {
                staging.Clear();
                count = 0;
                version++;
            }
        }

        public IEnumerator<RangeValuePair<TKey, TValue>> GetEnumerator()
        {
            RangeValuePair<TKey, TValue>[] snapshot;
            lock (writeGate)
                snapshot = staging.ToArray();

            return ((IEnumerable<RangeValuePair<TKey, TValue>>)snapshot).GetEnumerator();
        }

        public void NotifyDirty()
        {
            lock (writeGate)
                version++;
        }

        private void RebuildInternal()
        {
            if (Volatile.Read(ref syncedVersion) == Volatile.Read(ref version))
                return;

            lock (rebuildGate)
            {
                if (Volatile.Read(ref syncedVersion) == Volatile.Read(ref version))
                    return;

                //快照必须在 writeGate 内连同版本号一起取：否则重建期间的新增会被这次发布"合并掉"，
                //版本比较也就永远追不上（索引静默漏项）。
                RangeValuePair<TKey, TValue>[] snapshot;
                int snapshotVersion;
                lock (writeGate)
                {
                    snapshot = staging.ToArray();
                    snapshotVersion = version;
                }

                var newRoot = IntervalTreeNode<TKey, TValue>.BuildTree(snapshot, comparer);

                //发布顺序：先索引后版本；读者先读版本、命中后再读索引，故不会看到"版本已最新但索引还是旧的"。
                //快照期间发生过的写入会让版本不等，接下来的一次查询会再重建一次，不会丢项。
                Volatile.Write(ref root, newRoot);
                Volatile.Write(ref syncedVersion, snapshotVersion);
            }
        }
    }
}
