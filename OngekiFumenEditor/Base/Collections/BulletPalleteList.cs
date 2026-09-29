using OngekiFumenEditor.Base.OngekiObjects;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;

namespace OngekiFumenEditor.Base.Collections
{
    public class BulletPalleteList : IReadOnlyList<BulletPallete>, INotifyCollectionChanged
    {
        private static readonly Dictionary<char, int> ALPHABET = Enumerable.Empty<char>()
            .Concat(Enumerable.Range(0, 10).Select(x => x + '0').Select(x => (char)x))
            .Concat(Enumerable.Range(0, 26).Select(x => x + 'A').Select(x => (char)x))
            .Select((x, i) => (x, i)).ToDictionary(x => x.x, x => x.i);

        private static readonly Dictionary<int, char> ALPHABET_REV = ALPHABET.ToDictionary(x => x.Value, x => x.Key);

        public static int ConvertIdToInt(string id)
        {
            return id
                .ToUpperInvariant()
                .Reverse()
                .Select((x, i) => (int)Math.Pow(ALPHABET.Count, i) * (ALPHABET.TryGetValue(x, out var d) ? d : 0))
                .Sum();
        }

        public static string ConvertIntToId(int val)
        {
            var str = "";

            while (val != 0)
            {
                str = ALPHABET_REV[val % ALPHABET_REV.Count] + str;
                val = val / ALPHABET_REV.Count;
            }

            return str.ToUpperInvariant();
        }

        private readonly Dictionary<int, BulletPallete> palleteMap = new();

        /// <summary>
        /// 与 <see cref="GetEnumerator"/> 暴露顺序一致的 backing 列表：按 <see cref="ConvertIdToInt"/> 升序。
        /// 索引器与枚举都走它，使 <see cref="Count"/><c>this[int]</c>/枚举三者互相自洽（真正的
        /// <see cref="IReadOnlyList{T}"/> 语义），并免掉原先每次枚举都 <c>OrderBy</c> 一遍的排序与分配。
        /// </summary>
        private readonly List<BulletPallete> orderedPalletes = new();

        private string cacheCurrentMaxId = null;

        public int Count => orderedPalletes.Count;
        public BulletPallete this[int index] => orderedPalletes[index];
        public BulletPallete this[string strId] => palleteMap.TryGetValue(ConvertIdToInt(strId), out var r) ? r : default;

        public event NotifyCollectionChangedEventHandler CollectionChanged;

        public IEnumerator<BulletPallete> GetEnumerator() => orderedPalletes.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        /// <summary>
        /// 按数值 id 在 backing 列表里定位插入点。顺序与旧实现
        /// <c>palleteMap.Values.OrderBy(x =&gt; ConvertIdToInt(x.StrID))</c> 完全一致
        /// （注意是按 <see cref="ConvertIdToInt"/> 的数值序，不是字符串序）。
        /// </summary>
        private int FindOrderedIndex(int id)
        {
            var lo = 0;
            var hi = orderedPalletes.Count;
            while (lo < hi)
            {
                var mid = lo + ((hi - lo) >> 1);
                if (ConvertIdToInt(orderedPalletes[mid].StrID) < id)
                    lo = mid + 1;
                else
                    hi = mid;
            }
            return lo;
        }

        private void EnsureMaxIdCache()
        {
            if (cacheCurrentMaxId is not null)
                return;

            cacheCurrentMaxId = palleteMap.Count == 0
                ? "9Z"
                : ConvertIntToId(palleteMap.Keys.OrderBy(x => x).LastOrDefault());
        }

        /// <summary>
        /// 预分配下一个可用的 StrID，并推进内部缓存。用于需要在模板真正入表前就拿到 id 的调用方
        /// （例如 MCP 工具要在事务提交前把 id 返回给客户端）；被丢弃的分配会永久留空、不再复用，
        /// 以换取绝不与后续分配撞车。
        /// </summary>
        public string AllocateStrID()
        {
            EnsureMaxIdCache();
            var next = ConvertIntToId(ConvertIdToInt(cacheCurrentMaxId) + 1);
            cacheCurrentMaxId = next;
            return next;
        }

        public void AddPallete(BulletPallete pallete)
        {
            EnsureMaxIdCache();

            if (string.IsNullOrWhiteSpace(pallete.StrID))
            {
                pallete.StrID = ConvertIntToId(ConvertIdToInt(cacheCurrentMaxId) + 1);
            }

            var addable = true;
            if (palleteMap.TryGetValue(ConvertIdToInt(pallete.StrID), out var old))
            {
                if (old == pallete)
                    addable = false; //�ظ���ӣ��Ǿͺ�����
                else
                {
                    RemovePallete(old); //���ھɵģ��Ǿ���ɾ�˾ɵ�������µ�
                }
            }

            if (addable)
            {
                var id = ConvertIdToInt(pallete.StrID);
                palleteMap[id] = pallete;
                orderedPalletes.Insert(FindOrderedIndex(id), pallete);

                pallete.PropertyChanged += OnPalletePropChanged;
                // 按数值序比较缓存：字符串序会在 "AZ" 与 "B0"、或不同长度的 id 之间误判，
                // 让缓存偏低并导致后续分配重用已存在的 id（AddPallete 会静默替换同 id 的模板）。
                if (id > ConvertIdToInt(cacheCurrentMaxId))
                    cacheCurrentMaxId = pallete.StrID;

                CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, pallete));
            }
        }

        public void RemovePallete(BulletPallete pallete)
        {
            if (palleteMap.Remove(ConvertIdToInt(pallete.StrID), out var removed))
            {
                // 退订与移除都用真正入表的那一个实例（AddPallete 订阅的就是它），
                // 保证订阅与退订严格互逆；调用方传入同 id 的另一个实例时也不会漏退订。
                orderedPalletes.Remove(removed);
                removed.PropertyChanged -= OnPalletePropChanged;
                CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
            }
        }

        private void OnPalletePropChanged(object sender, PropertyChangedEventArgs e)
        {

        }
    }
}
