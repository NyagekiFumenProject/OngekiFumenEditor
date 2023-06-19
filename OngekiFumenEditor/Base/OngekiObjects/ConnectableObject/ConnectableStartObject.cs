using Caliburn.Micro;
using NAudio.Midi;
using OngekiFumenEditor.Base.EditorObjects;
using OngekiFumenEditor.Base.EditorObjects.LaneCurve;
using OngekiFumenEditor.Kernel.CurveInterpolater;
using OngekiFumenEditor.Kernel.CurveInterpolater.DefaultImpl.Factory;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.OngekiObjects;
using OngekiFumenEditor.Utils;
using OngekiFumenEditor.Utils.ObjectPool;
using OpenTK.Mathematics;
using SimpleSvg2LineSegementInterpolater;
using SimpleSvg2LineSegementInterpolater.Base;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.DirectoryServices.AccountManagement;
using System.Drawing;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace OngekiFumenEditor.Base.OngekiObjects.ConnectableObject
{
    public abstract class ConnectableStartObject : ConnectableObjectBase
    {
        public event Action<object, PropertyChangedEventArgs> ConnectableObjectsPropertyChanged;

        private ICurveInterpolaterFactory curveInterpolaterFactory = DefaultCurveInterpolaterFactory.Default;
        public ICurveInterpolaterFactory CurveInterpolaterFactory
        {
            get => curveInterpolaterFactory;
            set => Set(ref curveInterpolaterFactory, value);
        }

        private List<ConnectableChildObjectBase> children = new();
        public IEnumerable<ConnectableChildObjectBase> Children => children;

        public TGrid MinTGrid => TGrid;
        public TGrid MaxTGrid => children.Count == 0 ? MinTGrid : children[children.Count - 1].TGrid;

        private int recordId = -1;
        public override int RecordId { get => recordId; set => Set(ref recordId, value); }

        public abstract ConnectableNextObject CreateNextObject();
        public abstract ConnectableEndObject CreateEndObject();

        public ConnectableStartObject()
        {
            PropertyChanged += OnPropertyChanged;
        }

        public void AddChildObject(ConnectableChildObjectBase child)
        {
            var insertIdx = child.CacheRecoveryChildIndex;
            if (!children.Contains(child))
            {
                if (insertIdx >= 0)
                {
                    var nextObj = children.ElementAtOrDefault(insertIdx);
                    var prevObj = children.ElementAtOrDefault(insertIdx - 1) ?? this as ConnectableObjectBase;
                    child.PrevObject = prevObj;

                    if (nextObj is not null)
                    {
                        nextObj.PrevObject = child;
                    }
                    insertIdx = Math.Min(insertIdx, children.Count);
                    children.Insert(insertIdx, child);
                }
                else
                {
                    child.PrevObject = Children.LastOrDefault() ?? this as ConnectableObjectBase;
                    children.Add(child);
                }
                child.PropertyChanged += OnPropertyChanged;
                NotifyWhenChildrenChanged();
            }
            child.ReferenceStartObject = this;
            child.RecordId = RecordId;
        }

        private void NotifyWhenChildrenChanged()
        {
            NotifyOfPropertyChange(() => Children);
            NotifyOfPropertyChange(() => MinTGrid);
            NotifyOfPropertyChange(() => MaxTGrid);
        }

        public void InsertChildObject(TGrid dragTGrid, ConnectableChildObjectBase child)
        {
            if (child is ConnectableEndObject)
            {
                AddChildObject(child);
                return;
            }

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

            child.ReferenceStartObject = this;
        }

        public void RemoveChildObject(ConnectableChildObjectBase child)
        {
            var idx = children.IndexOf(child);
            children.Remove(child);

            var prev = child.PrevObject;
            var next = children.FirstOrDefault(x => x.PrevObject == child);
            if (next is not null)
            {
                next.PrevObject = prev;
            }
            child.PrevObject = default;

            child.ReferenceStartObject = default;
            child.PropertyChanged -= OnPropertyChanged;
            child.CacheRecoveryChildIndex = idx;

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
                    NotifyOfPropertyChange(() => MinTGrid);
                    NotifyOfPropertyChange(() => MaxTGrid);
                    break;
                default:
                    break;
            }
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

        public XGrid CalulateXGrid(TGrid tGrid)
        {
            if (tGrid < TGrid)
                return default;

            foreach (var cur in Children)
            {
                if (tGrid <= cur.TGrid)
                {
                    var xGrid = cur.CalulateXGrid(tGrid);
                    if (xGrid == null)
                        return default;
                    return xGrid;
                }
            }

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
            => InterpolateCurve(() => CopyNew() as ConnectableStartObject, () => CreateNextObject(), () => CreateEndObject(), factory).OfType<ConnectableStartObject>();

        public IEnumerable<ConnectableStartObject> InterpolateCurve(Type startType, Type nextType, Type endType, ICurveInterpolaterFactory factory = default)
            => InterpolateCurve(
                () => LambdaActivator.CreateInstance(startType) as ConnectableStartObject,
                () => LambdaActivator.CreateInstance(nextType) as ConnectableNextObject,
                () => LambdaActivator.CreateInstance(endType) as ConnectableEndObject,
                factory
                ).OfType<ConnectableStartObject>();

        public IEnumerable<START> InterpolateCurve<START, NEXT, END>(ICurveInterpolaterFactory factory = default)
            where START : ConnectableStartObject, new()
            where END : ConnectableEndObject, new()
            where NEXT : ConnectableNextObject, new()
            => InterpolateCurve(() => new START(), () => new NEXT(), () => new END(), factory).OfType<START>();

        public virtual IEnumerable<ConnectableStartObject> InterpolateCurve(Func<ConnectableStartObject> genStartFunc, Func<ConnectableNextObject> genNextFunc, Func<ConnectableEndObject> genEndFunc, ICurveInterpolaterFactory factory = default)
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
                var end = genEndFunc();
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
    }
}
