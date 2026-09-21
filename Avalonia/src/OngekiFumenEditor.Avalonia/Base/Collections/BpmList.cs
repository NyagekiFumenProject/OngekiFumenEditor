using System.Collections;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using OngekiFumenEditor.Avalonia.Base.Collections.Base;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects;
using OngekiFumenEditor.Avalonia.Utils;

namespace OngekiFumenEditor.Avalonia.Base.Collections
{
    public class BpmList : IBinaryFindRangeEnumable<BPMChange, TGrid>
    {
        public const double DefaultFirstBpm = 240;

        private TGridSortList<BPMChange> changedBpmList = new();

        public int Count => 1 + changedBpmList.Count;

        public event Action OnChangedEvent;

        public double FirstBpm
        {
            get
            {
                return changedBpmList.FirstOrDefault()?.BPM ?? DefaultFirstBpm;
            }
            set
            {
                if (changedBpmList.FirstOrDefault() is not BPMChange bpmChange)
                {
                    bpmChange = new BPMChange()
                    {
                        TGrid = new(0, 0)
                    };
                    Add(bpmChange);
                }

                bpmChange.BPM = value;
            }
        }

        public BpmList(IEnumerable<BPMChange> initBpmChanges = default)
        {
            FirstBpm = DefaultFirstBpm;

            OnChangedEvent += BpmList_OnChangedEvent;
            foreach (var item in initBpmChanges ?? Enumerable.Empty<BPMChange>())
                Add(item);
        }

        #region ContentVersion

        // 内容令牌（DAT-C1）：任何会改变本列表内容的路径（Add / Remove / 任一 BPMChange 的 BPM 或
        // TGrid 变化，含经转发链上来的 TGrid.Unit/Grid 子属性）都会取一个新的全局唯一值，
        // 于是下游缓存的命中判断只是一次整数比较，不再像旧实现那样每次调用都对整表
        // Aggregate 求内容哈希（成本与 BPM 数量线性相关）。
        // 本字段同时是 MeterChangeList / SoflanList 各自缓存的失效依据：三者都取自
        // NonceGenerator.Next()，故彼此只做相等比较 —— 既不判新旧，也不比来源实例。
        private int contentVersion = NonceGenerator.Next();
        private int cachedBpmUniformPositionVersion = NonceGenerator.Next();
        internal int ContentVersion => contentVersion;

        #endregion

        private void BpmList_OnChangedEvent()
        {
            contentVersion = NonceGenerator.Next();
        }

        public void Add(BPMChange bpm)
        {
            changedBpmList.Add(bpm);
            bpm.PropertyChanged += OnBpmPropChanged;
            OnChangedEvent?.Invoke();
        }

        private void OnBpmPropChanged(object sender, PropertyChangedEventArgs e)
        {
            OnChangedEvent?.Invoke();
        }

        public bool Remove(BPMChange bpm)
        {
            if (bpm == changedBpmList.FirstOrDefault())
                throw new Exception($"BpmList can't delete firstBpm : {bpm}, but you can use SetFirstBpm({bpm})");
            var r = changedBpmList.Remove(bpm);
            if (r)
            {
                bpm.PropertyChanged -= OnBpmPropChanged;
                OnChangedEvent?.Invoke();
            }
            return r;
        }

        public IEnumerator<BPMChange> GetEnumerator()
        {
            return changedBpmList.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetBpmIndex(TGrid time)
        {
            var idx = changedBpmList.BinarySearchBy(time);
            var actualIdx = idx < 0 ? ((~idx) - 1) : idx;
            return actualIdx;
        }

        public BPMChange GetBpm(TGrid time)
        {
            var idx = GetBpmIndex(time);
            return changedBpmList[idx];
        }

        public BPMChange GetPrevBpm(BPMChange time) => GetPrevBpm(time.TGrid);

        public BPMChange GetPrevBpm(TGrid time)
        {
            var idx = GetBpmIndex(time);
            return changedBpmList.ElementAtOrDefault(idx - 1);
        }

        public BPMChange GetNextBpm(BPMChange bpm) => GetNextBpm(bpm.TGrid);

        public BPMChange GetNextBpm(TGrid time)
        {
            var idx = GetBpmIndex(time);
            return changedBpmList.ElementAtOrDefault(idx + 1);
        }

        private List<(TimeSpan audioTime, BPMChange bpm)> cachedBpmUniformPosition = new();

        private void UpdateCachedAllBpmUniformPositionList()
        {
            cachedBpmUniformPosition.Clear();

            var itor = changedBpmList.GetEnumerator();

            if (itor.MoveNext())
            {
                var prev = itor.Current;
                var currentTimeMs = 0d;

                cachedBpmUniformPosition.Add((TimeSpan.FromMilliseconds(0), prev));

                while (itor.MoveNext())
                {
                    var cur = itor.Current;
                    if (cur is null)
                        break;
                    var len = MathUtils.CalculateBPMLength(prev, cur.TGrid);
                    prev = cur;
                    currentTimeMs += len;

                    var time = TimeSpan.FromMilliseconds(currentTimeMs);
                    cachedBpmUniformPosition.Add((time, cur));
                }
            }
        }

        public List<(TimeSpan audioTime, BPMChange bpm)> GetCachedAllBpmUniformPositionList()
        {
            var version = ContentVersion;

            if (cachedBpmUniformPositionVersion != version)
            {
                //Log.LogDebug("recalculate all bpm postions.");
                UpdateCachedAllBpmUniformPositionList();
                cachedBpmUniformPositionVersion = version;
#if DEBUG
                debugLastContentHash = CalculateContentHash();
#endif
            }
#if DEBUG
            else if (debugLastContentHash != CalculateContentHash())
            {
                // 版本号方案的前提是「内容一变就递增版本号」。旧实现每次调用都重算内容哈希，
                // 天然容忍漏掉版本号递增的变更路径；这里把那份哈希留在 DEBUG 下对拍，
                // 任何未被 PropertyChanged 通知到的内容变更都会立刻暴露（Release 下不参与编译）。
                throw new InvalidOperationException(
                    $"{nameof(BpmList)} content changed without bumping {nameof(ContentVersion)}: some mutation path does not raise PropertyChanged.");
            }
#endif

            return cachedBpmUniformPosition;
        }

#if DEBUG
        private int debugLastContentHash;

        /// <summary>旧实现每次调用都会重算的那份内容哈希，仅 DEBUG 校验用。</summary>
        private int CalculateContentHash()
        {
            int calcHash(BPMChange e) => HashCode.Combine(e.BPM, e.TGrid.TotalGrid);
            return HashCode.Combine(this.Aggregate(0, (x, e) => HashCode.Combine(x, calcHash(e))));
        }
#endif

        public (int minIndex, int maxIndex) BinaryFindRangeIndex(TGrid min, TGrid max)
            => ((IBinaryFindRangeEnumable<BPMChange, TGrid>)changedBpmList).BinaryFindRangeIndex(min, max);

        public IEnumerable<BPMChange> BinaryFindRange(TGrid min, TGrid max)
            => ((IBinaryFindRangeEnumable<BPMChange, TGrid>)changedBpmList).BinaryFindRange(min, max);

        public bool Contains(BPMChange obj)
            => ((IBinaryFindRangeEnumable<BPMChange, TGrid>)changedBpmList).Contains(obj);
    }
}

