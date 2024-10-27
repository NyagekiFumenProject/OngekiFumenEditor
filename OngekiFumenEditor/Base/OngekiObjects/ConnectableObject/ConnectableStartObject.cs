using OngekiFumenEditor.Kernel.CurveInterpolater;
using OngekiFumenEditor.Kernel.CurveInterpolater.DefaultImpl.Factory;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using OngekiFumenEditor.Utils;
using OpenTK.Mathematics;
using SimpleSvg2LineSegementInterpolater;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

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

        private List<ConnectableChildObjectBase> children = new();
        public IEnumerable<ConnectableChildObjectBase> Children => children;

        public override ConnectableStartObject ReferenceStartObject => this;

        private TGrid cachedMinTGrid = default;
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
                                minTGrid = MathUtils.Min(minTGrid, child.TGrid);
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

                    cachedMinTGrid = minTGrid;
                    cachedMinTGrid.NormalizeSelf();
                }
                return cachedMinTGrid;
            }
        }

        private TGrid cachedMaxTGrid = default;
        public TGrid MaxTGrid
        {
            get
            {
                //children.Count == 0 ? MinTGrid : children[children.Count - 1].TGrid
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
                                maxTGrid = MathUtils.Max(maxTGrid, child.TGrid);
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

                    cachedMaxTGrid = maxTGrid;
                    cachedMaxTGrid.NormalizeSelf();
                }
                return cachedMaxTGrid;
            }
        }

        private int recordId = -1;
        public override int RecordId { get => recordId; set => Set(ref recordId, value); }

        public abstract ConnectableChildObjectBase CreateChildObject();

        public ConnectableStartObject()
        {
            PropertyChanged += OnPropertyChanged;
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
                    var nextObj = children.ElementAtOrDefault(idx);
                    var prevObj = children.ElementAtOrDefault(idx - 1) ?? this as ConnectableObjectBase;

                    //build their relations: prev -> cur(child) -> next
                    if (nextObj is not null)
                        nextObj.PrevObject = child;
                    child.PrevObject = prevObj;

                    idx = Math.Min(idx, children.Count);
                    children.Insert(idx, child);
                }
                else
                {
                    child.PrevObject = Children.LastOrDefault() ?? this as ConnectableObjectBase;
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
            var idx = children.IndexOf(child);
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
                        NextObject?.NotifyRefreshPaths();
                    NotifyRefreshMinMaxTGrid();
                    break;
                case nameof(XGrid):
                    if (sender is ConnectableChildObjectBase child2)
                    {
                        child2.NotifyRefreshPaths();
                        child2.NextObject?.NotifyRefreshPaths();
                    }
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
            foreach (var child in Children.SelectMany(x => x.GetDisplayableObjects().Append(x)))
                yield return child;
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
            var x = children.AsEnumerable<ITimelineObject>().Append(this).Select(x => x.TGrid).MaxMinBy();
            return new GridRange()
            {
                Max = x.max,
                Min = x.min,
            };
        }

        public GridRange GetXGridRange()
        {
            var x = children.AsEnumerable<IHorizonPositionObject>().Append(this).Select(x => x.XGrid).MaxMinBy();
            return new GridRange()
            {
                Max = x.max,
                Min = x.min,
            };
        }

        public ConnectableChildObjectBase GetChildObjectFromTGrid(TGrid tGrid)
        {
            if (tGrid < TGrid)
                return default;

            foreach (var cur in Children)
            {
                if (tGrid <= cur.TGrid)
                    return cur;
            }

            return default;
        }

        public XGrid CalulateXGrid(TGrid tGrid)
        {
            if (GetChildObjectFromTGrid(tGrid) is ConnectableChildObjectBase child)
                return child.CalulateXGrid(tGrid);
            return default;
        }

        public bool IsPathVaild() => GenAllPath().All(x => x.isVaild);

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
                    var sign = MathF.Sign(gradient);

                    if (prevSign != sign && list.Count != 0)
                    {
                        yield return list;
                        list = new List<CurvePoint>();
                        list.Add(prevPoint);
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

            foreach (var lineSegment in split().Where(x => x.Count() >= 2))
            {
                if (calcGradient(lineSegment[0], lineSegment[1]) < 0)
                    lineSegment.Reverse();

                var start = genStartFunc();
                build(start, lineSegment[0]);
                foreach (var childPos in lineSegment.Skip(1).SkipLast(1))
                {
                    var next = genNextFunc();
                    build(next, childPos);
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
