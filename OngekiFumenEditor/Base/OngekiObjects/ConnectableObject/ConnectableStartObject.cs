using OngekiFumenEditor.Kernel.CurveInterpolater;
using OngekiFumenEditor.Kernel.CurveInterpolater.OgkrImpl.Factory;
using OngekiFumenEditor.Utils;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Numerics;

namespace OngekiFumenEditor.Base.OngekiObjects.ConnectableObject
{
    public abstract class ConnectableStartObject : ConnectableObjectBase
    {
        public event Action<object, PropertyChangedEventArgs> ConnectableObjectsPropertyChanged;

        private ICurveInterpolaterFactory curveInterpolaterFactory = XGridLimitedCurveInterpolaterFactory.Default;
        public ICurveInterpolaterFactory CurveInterpolaterFactory
        {
            get => curveInterpolaterFactory;
            set => Set(ref curveInterpolaterFactory, value);
        }

        private readonly List<ConnectableChildObjectBase> children = new List<ConnectableChildObjectBase>();
        public IEnumerable<ConnectableChildObjectBase> Children => children;
        public int ChildCount => children.Count;

        public override ConnectableStartObject ReferenceStartObject => this;

        private TGrid cachedMinTGrid;
        public TGrid MinTGrid
        {
            get
            {
                if (cachedMinTGrid is null)
                {
                    var minTGrid = TGrid;
                    if (!Children.All(x => x.IsVaildPath))
                    {
                        var shareTGrid = new TGrid();
                        foreach (var child in Children)
                        {
                            if (child.IsVaildPath)
                            {
                                if (child.TGrid < minTGrid)
                                    minTGrid = child.TGrid;
                            }
                            else
                            {
                                foreach (var path in child.GetConnectionPaths())
                                {
                                    shareTGrid.Unit = path.pos.Y / TGrid.ResT;
                                    if (shareTGrid < minTGrid)
                                        minTGrid = shareTGrid.CopyNew();
                                }
                            }
                        }
                    }

                    cachedMinTGrid = minTGrid.CopyNew();
                    cachedMinTGrid.NormalizeSelf();
                }
                return cachedMinTGrid;
            }
        }

        private TGrid cachedMaxTGrid;
        public TGrid MaxTGrid
        {
            get
            {
                if (cachedMaxTGrid is null)
                {
                    var maxTGrid = TGrid;
                    if (children.Count == 0)
                    {
                        maxTGrid = MinTGrid;
                    }
                    else if (Children.All(x => x.IsVaildPath))
                    {
                        maxTGrid = children[children.Count - 1].TGrid;
                    }
                    else
                    {
                        var shareTGrid = new TGrid();
                        foreach (var child in Children)
                        {
                            if (child.IsVaildPath)
                            {
                                if (child.TGrid > maxTGrid)
                                    maxTGrid = child.TGrid;
                            }
                            else
                            {
                                foreach (var path in child.GetConnectionPaths())
                                {
                                    shareTGrid.Unit = path.pos.Y / TGrid.ResT;
                                    if (shareTGrid > maxTGrid)
                                        maxTGrid = shareTGrid.CopyNew();
                                }
                            }
                        }
                    }

                    cachedMaxTGrid = maxTGrid.CopyNew();
                    cachedMaxTGrid.NormalizeSelf();
                }
                return cachedMaxTGrid;
            }
        }

        private int recordId = -1;
        public override int RecordId { get => recordId; set => Set(ref recordId, value); }

        public abstract ConnectableChildObjectBase CreateChildObject();

        protected ConnectableStartObject()
        {
            PropertyChanged += OnPropertyChanged;
        }

        internal ConnectableChildObjectBase FindNextChild(ConnectableChildObjectBase target)
        {
            var prev = default(ConnectableChildObjectBase);
            foreach (var child in children)
            {
                if (ReferenceEquals(prev, target))
                    return child;
                prev = child;
            }
            return default;
        }

        private static int BinarySearchChildrenByTGrid(IList<ConnectableChildObjectBase> list, TGrid value)
        {
            var lo = 0;
            var hi = list.Count - 1;
            while (lo <= hi)
            {
                var i = lo + ((hi - lo) >> 1);
                var order = list[i].TGrid.CompareTo(value);

                if (order == 0)
                {
                    for (var r = i + 1; r < list.Count; r++)
                    {
                        if (list[r].TGrid.CompareTo(list[i].TGrid) == 0)
                            i = r;
                        else
                            break;
                    }
                    return i;
                }

                if (order < 0)
                    lo = i + 1;
                else
                    hi = i - 1;
            }

            return ~lo;
        }

        public void AddChildObject(ConnectableChildObjectBase child)
        {
            InsertChildObject(children.Count, child);
        }

        public void InsertChildObject(int idx, ConnectableChildObjectBase child)
        {
            if (!children.Contains(child))
            {
                if (idx >= 0)
                {
                    var nextObj = idx < children.Count ? children[idx] : default;
                    var prevObj = idx > 0 ? children[idx - 1] : this as ConnectableObjectBase;

                    if (nextObj is not null)
                        nextObj.PrevObject = child;
                    child.PrevObject = prevObj;

                    idx = Math.Min(idx, children.Count);
                    children.Insert(idx, child);
                }
                else
                {
                    child.PrevObject = children.LastOrDefault() ?? this as ConnectableObjectBase;
                    children.Add(child);
                }
                child.PropertyChanged += OnPropertyChanged;
                NotifyWhenChildrenChanged();
            }
            child.SetReferenceStartObject(this);
            child.RecordId = RecordId;
        }

        private void NotifyWhenChildrenChanged()
        {
            NotifyOfPropertyChange(() => Children);
            NotifyRefreshMinMaxTGrid();
        }

        public void InsertChildObject(TGrid dragTGrid, ConnectableChildObjectBase child)
        {
            if (!children.Contains(child))
            {
                child.PrevObject = default;
                for (int i = 0; i < children.Count; i++)
                {
                    var next = children[i];

                    if (dragTGrid < next.TGrid)
                    {
                        ConnectableObjectBase prev = i == 0 ? this : children[i - 1];
                        children.Insert(i, child);
                        next.PrevObject = child;
                        child.PrevObject = prev;

                        child.PropertyChanged += OnPropertyChanged;
                        child.RecordId = RecordId;
                        break;
                    }
                }

                if (child.PrevObject is null)
                    AddChildObject(child);
                else
                    NotifyWhenChildrenChanged();
            }

            child.SetReferenceStartObject(this);
        }

        public void RemoveChildObject(ConnectableChildObjectBase child)
        {
            children.Remove(child);

            var prev = child.PrevObject;
            var next = children.FirstOrDefault(x => x.PrevObject == child);
            if (next is not null)
                next.PrevObject = prev;
            else
                child.PrevObject = default;

            child.SetReferenceStartObject(default);
            child.PropertyChanged -= OnPropertyChanged;

            NotifyWhenChildrenChanged();
        }

        private void OnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            ConnectableObjectsPropertyChanged?.Invoke(sender, e);
            switch (e.PropertyName)
            {
                case nameof(TGrid):
                    if (sender is ConnectableChildObjectBase child)
                    {
                        child.NotifyRefreshPaths();
                        child.NextObject?.NotifyRefreshPaths();
                    }
                    else
                    {
                        NextObject?.NotifyRefreshPaths();
                    }
                    NotifyRefreshMinMaxTGrid();
                    break;
                case nameof(XGrid):
                    if (sender is ConnectableChildObjectBase child2)
                    {
                        child2.NotifyRefreshPaths();
                        child2.NextObject?.NotifyRefreshPaths();
                    }
                    break;
                case nameof(ConnectableChildObjectBase.CurvePrecision):
                    //曲线精度变化只会影响该子物件自己生成的路径，重新生成即可。
                    if (sender is ConnectableChildObjectBase curveChangedChild)
                        curveChangedChild.NotifyRefreshPaths();
                    break;
                default:
                    break;
            }
        }

        private void NotifyRefreshMinMaxTGrid()
        {
            cachedMaxTGrid = default;
            cachedMinTGrid = default;
            NotifyOfPropertyChange(() => MinTGrid);
            NotifyOfPropertyChange(() => MaxTGrid);
        }

        public override IEnumerable<IDisplayableObject> GetDisplayableObjects()
        {
            yield return this;
            for (var i = 0; i < children.Count; i++)
            {
                var child = children[i];
                var pcs = child.PathControls;
                for (var j = 0; j < pcs.Count; j++)
                    yield return pcs[j];
                yield return child;
            }
        }

        public override bool CheckVisiable(TGrid minVisibleTGrid, TGrid maxVisibleTGrid)
        {
            if (maxVisibleTGrid < MinTGrid)
                return false;

            if (MaxTGrid < minVisibleTGrid)
                return false;

            return true;
        }

        public GridRange GetTGridRange()
        {
            var min = TGrid;
            var max = TGrid;
            foreach (var child in children)
            {
                if (child.TGrid < min)
                    min = child.TGrid;
                if (child.TGrid > max)
                    max = child.TGrid;
            }

            return new GridRange
            {
                Max = max,
                Min = min,
            };
        }

        public GridRange GetXGridRange()
        {
            var min = XGrid;
            var max = XGrid;
            foreach (var child in children)
            {
                if (child.XGrid < min)
                    min = child.XGrid;
                if (child.XGrid > max)
                    max = child.XGrid;
            }

            return new GridRange
            {
                Max = max,
                Min = min,
            };
        }

        public ConnectableChildObjectBase GetChildObjectFromTGrid(TGrid tGrid)
        {
            return TryGetChildObjectFromTGrid(tGrid, out var child) ? child : default;
        }

        /// <summary>
        /// <see cref="GetChildObjectFromTGrid"/> 的零分配形态：与 <c>GetChildObjectsFromTGrid(tGrid).FirstOrDefault()</c> 等价
        /// （有效路径走索引区间，无效路径逐项扫描，都不分配）。
        /// </summary>
        public bool TryGetChildObjectFromTGrid(TGrid tGrid, out ConnectableChildObjectBase child)
        {
            child = default;

            if (tGrid is null || tGrid < TGrid || children.Count == 0)
                return false;

            if (IsPathVaild())
            {
                if (!TryGetValidPathChildRange(tGrid, out var start, out _))
                    return false;

                child = children[start];
                return true;
            }

            ConnectableObjectBase prev = this;
            foreach (var item in children)
            {
                if (item.TGrid >= tGrid && prev.TGrid <= tGrid && tGrid <= item.TGrid)
                {
                    child = item;
                    return true;
                }

                prev = item;
            }

            return false;
        }

        /// <summary>
        /// 直接求指定 TGrid 处的 XGrid.TotalUnit（不构造 XGrid），等价于 <c>CalulateXGrid(tGrid)?.TotalUnit</c>。
        /// </summary>
        public bool TryCalulateXGridTotalUnit(TGrid tGrid, out double totalUnit)
        {
            if (TryGetChildObjectFromTGrid(tGrid, out var child))
                return child.TryCalulateXGridTotalUnit(tGrid, out totalUnit);

            totalUnit = default;
            return false;
        }

        public IEnumerable<ConnectableChildObjectBase> GetChildObjectsFromTGrid(TGrid tGrid)
        {
            if (tGrid is null || tGrid < TGrid || children.Count == 0)
                return Enumerable.Empty<ConnectableChildObjectBase>();

            if (IsPathVaild())
            {
                //path is vaild, means children are sorted by TGrid, just find by binary search
                return TryGetValidPathChildRange(tGrid, out var start, out var count)
                    ? children.GetRange(start, count)
                    : Enumerable.Empty<ConnectableChildObjectBase>();
            }

            var result = new List<ConnectableChildObjectBase>();
            ConnectableObjectBase prev = this;
            foreach (var child in children)
            {
                if (child.TGrid >= tGrid && prev.TGrid <= tGrid && tGrid <= child.TGrid)
                    result.Add(child);
                prev = child;
            }

            return result;
        }

        /// <summary>
        /// 在有效路径下定位 tGrid 对应子物体的索引区间（等价于 <see cref="GetChildObjectsFromTGrid"/> 的有效路径分支），
        /// 但不分配 List，供每帧高频查询复用。
        /// </summary>
        public bool TryGetValidPathChildRange(TGrid tGrid, out int start, out int count)
        {
            start = 0;
            count = 0;

            if (tGrid is null || tGrid < TGrid || children.Count == 0)
                return false;

            if (children.Count > 1)
            {
                var idx = BinarySearchChildrenByTGrid(children, tGrid);
                var actualIdx = idx < 0 ? ~idx : idx;
                var fixedIdx = (actualIdx == children.Count - 1 && tGrid > children[actualIdx].TGrid)
                    ? -1
                    : actualIdx;

                if (fixedIdx < 0 || fixedIdx >= children.Count)
                    return false;

                var selectedTGrid = children[fixedIdx].TGrid;

                var minIdx = fixedIdx;
                while (minIdx > 0 && children[minIdx - 1].TGrid == selectedTGrid)
                    minIdx--;

                var maxIdx = fixedIdx;
                while (maxIdx < children.Count - 1 && children[maxIdx + 1].TGrid == selectedTGrid)
                    maxIdx++;

                start = minIdx;
                count = maxIdx - minIdx + 1;
                return true;
            }

            var child = children[0];
            if (tGrid > child.TGrid)
                return false;

            start = 0;
            count = 1;
            return true;
        }

        /// <summary>
        /// 按索引访问子物体；配合 <see cref="TryGetValidPathChildRange"/> 可无分配地读取区间元素。
        /// </summary>
        public ConnectableChildObjectBase GetChildObjectAt(int index) => children[index];

        public XGrid CalulateXGrid(TGrid tGrid)
        {
            if (TryGetChildObjectFromTGrid(tGrid, out var child))
                return child.CalulateXGrid(tGrid);
            return default;
        }

        public bool IsPathVaild()
        {
            for (var i = 0; i < children.Count; i++)
            {
                if (!children[i].IsVaildPath)
                    return false;
            }

            return true;
        }

        public IEnumerable<(Vector2 pos, bool isVaild)> GenAllPath(bool filterSamePointSameSeq = true)
        {
            Vector2? prevP = null;
            var isVaild = true;

            foreach (var child in Children)
            {
                foreach (var cg in child.GetConnectionPaths())
                {
                    if (cg.pos == prevP && filterSamePointSameSeq)
                        continue;

                    isVaild = isVaild && cg.isVaild;

                    yield return (cg.pos, isVaild);

                    prevP = cg.pos;
                }
            }
        }

        public IEnumerable<ConnectableStartObject> InterpolateCurve(ICurveInterpolaterFactory factory = default)
            => InterpolateCurve(() => CopyNew() as ConnectableStartObject, () => CreateChildObject(), factory).OfType<ConnectableStartObject>();

        public IEnumerable<ConnectableStartObject> InterpolateCurve(Type startType, Type nextType, Type endType, ICurveInterpolaterFactory factory = default)
            => InterpolateCurve(
                () => LambdaActivator.CreateInstance(startType) as ConnectableStartObject,
                () => LambdaActivator.CreateInstance(nextType) as ConnectableChildObjectBase,
                factory
            ).OfType<ConnectableStartObject>();

        public IEnumerable<START> InterpolateCurve<START, NEXT, END>(ICurveInterpolaterFactory factory = default)
            where START : ConnectableStartObject, new()
            where NEXT : ConnectableChildObjectBase, new()
            => InterpolateCurve(() => new START(), () => new NEXT(), factory).OfType<START>();

        public virtual IEnumerable<ConnectableStartObject> InterpolateCurve(Func<ConnectableStartObject> genStartFunc, Func<ConnectableChildObjectBase> genNextFunc, ICurveInterpolaterFactory factory = default)
        {
            var traveller = (factory ?? CurveInterpolaterFactory).CreateInterpolaterForAll(this);

            float calcGradient(CurvePoint a, CurvePoint b)
            {
                if (a.TGrid == b.TGrid)
                    return float.MaxValue;

                var offset = a.TGrid - b.TGrid;
                return -(offset.Unit * a.TGrid.ResT + offset.Grid);
            }

            IEnumerable<List<CurvePoint>> split()
            {
                var list = new List<CurvePoint>();
                if (traveller.EnumerateNext() is not CurvePoint p)
                    yield break;
                var prevPoint = p;
                traveller.PushBack(p);
                var prevSign = 0;

                while (true)
                {
                    if (traveller.EnumerateNext() is not CurvePoint point)
                        break;
                    var gradient = calcGradient(prevPoint, point);
                    var sign = Math.Sign(gradient);

                    if (prevSign != sign && list.Count != 0)
                    {
                        yield return list;
                        list = new List<CurvePoint> { prevPoint };
                    }

                    prevPoint = point;
                    prevSign = sign;

                    list.Add(point);
                }

                if (list.Count != 0)
                    yield return list;
            }

            void build(OngekiMovableObjectBase o, CurvePoint p)
            {
                o.TGrid = p.TGrid;
                o.XGrid = p.XGrid;
            }

            foreach (var lineSegment in split().Where(x => x.Count >= 2))
            {
                if (calcGradient(lineSegment[0], lineSegment[1]) < 0)
                    lineSegment.Reverse();

                var start = genStartFunc();
                build(start, lineSegment[0]);
                for (int i = 1; i < lineSegment.Count - 1; i++)
                {
                    var next = genNextFunc();
                    build(next, lineSegment[i]);
                    start.AddChildObject(next);
                }
                var end = genNextFunc();
                build(end, lineSegment[lineSegment.Count - 1]);
                start.AddChildObject(end);

                yield return start;
            }
        }

        public override void Copy(OngekiObjectBase fromObj)
        {
            base.Copy(fromObj);

            if (fromObj is not ConnectableStartObject from)
                return;

            RecordId = -Math.Abs(from.RecordId);
        }

        public void CopyEntireConnectableObject(ConnectableStartObject from)
        {
            Copy(from);

            RecordId = -Math.Abs(from.RecordId);

            foreach (var child in from.Children)
            {
                var copyChild = child.CopyNew() as ConnectableChildObjectBase;
                AddChildObject(copyChild);
            }
        }
    }
}

