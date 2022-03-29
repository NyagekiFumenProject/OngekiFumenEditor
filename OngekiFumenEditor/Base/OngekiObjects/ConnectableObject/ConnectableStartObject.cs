using Caliburn.Micro;
using NAudio.Midi;
using OngekiFumenEditor.Base.EditorObjects;
using OngekiFumenEditor.Base.EditorObjects.LaneCurve;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.OngekiObjects;
using OngekiFumenEditor.Utils;
using OngekiFumenEditor.Utils.ObjectPool;
using OpenTK.Mathematics;
using SimpleSvg2LineSegementInterpolater;
using SimpleSvg2LineSegementInterpolater.Base;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using static OngekiFumenEditor.Utils.CurveInterpolaterTraveller;

namespace OngekiFumenEditor.Base.OngekiObjects.ConnectableObject
{
    public abstract class ConnectableStartObject : ConnectableObjectBase
    {
        public event Action<object, PropertyChangedEventArgs> ConnectableObjectsPropertyChanged;

        private List<ConnectableChildObjectBase> children = new();
        private List<ConnectorLineBase<ConnectableObjectBase>> connectors = new();
        public IEnumerable<ConnectableChildObjectBase> Children => children;

        public TGrid MinTGrid => TGrid;
        public TGrid MaxTGrid => children.Count == 0 ? MinTGrid : children[children.Count - 1].TGrid;

        private int recordId = -1;
        public override int RecordId { get => recordId; set => Set(ref recordId, value); }

        protected abstract ConnectorLineBase<ConnectableObjectBase> GenerateConnector(ConnectableObjectBase from, ConnectableObjectBase to);

        public abstract Type NextType { get; }
        public abstract Type EndType { get; }

        protected ConnectorLineBase<ConnectableObjectBase> GenerateConnectorInternal<T>(ConnectableObjectBase from, ConnectableObjectBase to) where T : ConnectorLineBase<ConnectableObjectBase>, new()
        {
            return new T()
            {
                From = from,
                To = to,
            };
        }

        public ConnectableStartObject()
        {
            PropertyChanged += OnPropertyChanged;
        }

        public void AddChildObject(ConnectableChildObjectBase child)
        {
            if (!children.Contains(child))
            {
                child.PrevObject = children.LastOrDefault() ?? this as ConnectableObjectBase;
                children.Add(child);
                NotifyOfPropertyChange(() => Children);
                connectors.Add(GenerateConnector(child.PrevObject, child));
                child.PropertyChanged += OnPropertyChanged;
            }
            child.ReferenceStartObject = this;
        }

        private void RemoveConnector(ConnectableObjectBase from, ConnectableObjectBase to)
        {
            connectors.Remove(connectors.FirstOrDefault(x => x.From == from && x.To == to));
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
                        RemoveConnector(prev, next);
                        next.PrevObject = child;
                        child.PrevObject = prev;

                        connectors.Add(GenerateConnector(child.PrevObject, child));
                        connectors.Add(GenerateConnector(next.PrevObject, next));

                        NotifyOfPropertyChange(() => Children);
                        child.PropertyChanged += OnPropertyChanged;
                        break;
                    }
                }

                if (child.PrevObject is null)
                    AddChildObject(child);
            }

            child.ReferenceStartObject = this;
        }

        public void RemoveChildObject(ConnectableChildObjectBase child)
        {
            children.Remove(child);

            connectors.RemoveAll(x => x.From == child || x.To == child);

            var prev = child.PrevObject;
            var next = children.FirstOrDefault(x => x.PrevObject == child);
            if (next is not null)
            {
                next.PrevObject = prev;
                connectors.Add(GenerateConnector(next.PrevObject, next));
            }
            child.PrevObject = default;

            child.ReferenceStartObject = default;
            child.PropertyChanged -= OnPropertyChanged;
            NotifyOfPropertyChange(() => Children);
        }

        private void OnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            ConnectableObjectsPropertyChanged?.Invoke(sender, e);
        }

        public override IEnumerable<IDisplayableObject> GetDisplayableObjects()
        {
            foreach (var child in connectors.SelectMany(x => x.GetDisplayableObjects().Append(x)))
                yield return child;
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

            var prev = this as ConnectableObjectBase;
            foreach (var cur in Children)
            {
                if (tGrid <= cur.TGrid)
                {
                    var xGrid = cur.CalulateXGrid(tGrid);
                    return xGrid;
                }

                prev = cur;
            }

            return default;
        }

        public IEnumerable<ConnectableStartObject> InterpolateCurve()
            => InterpolateCurve(GetType(), NextType, EndType).OfType<ConnectableStartObject>();

        public IEnumerable<ConnectableStartObject> InterpolateCurve(Type startType, Type nextType, Type endType)
            => InterpolateCurve(
                () => LambdaActivator.CreateInstance(startType) as ConnectableStartObject,
                () => LambdaActivator.CreateInstance(nextType) as ConnectableNextObject,
                () => LambdaActivator.CreateInstance(endType) as ConnectableEndObject
                ).OfType<ConnectableStartObject>();

        public IEnumerable<START> InterpolateCurve<START, NEXT, END>()
            where START : ConnectableStartObject, new()
            where END : ConnectableEndObject, new()
            where NEXT : ConnectableNextObject, new()
            => InterpolateCurve(() => new START(), () => new NEXT(), () => new END()).OfType<START>();

        public IEnumerable<ConnectableStartObject> InterpolateCurve(Func<ConnectableStartObject> genStartFunc, Func<ConnectableNextObject> genNextFunc, Func<ConnectableEndObject> genEndFunc)
        {
            var traveller = new CurveInterpolaterTraveller(this);

            float calcGradient(CurvePoint a, CurvePoint b)
            {
                if (a.TGrid == b.TGrid)
                    return float.MaxValue;

                return -(a.TGrid - b.TGrid).TotalGrid(a.TGrid.ResT);
            }

            IEnumerable<List<CurvePoint>> split()
            {
                var list = new List<CurvePoint>();
                if (traveller.Travel() is not CurvePoint p)
                    yield break;
                var prevPoint = p;
                traveller.PushBack(p);
                var prevSign = 0;

                while (true)
                {
                    if (traveller.Travel() is not CurvePoint point)
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
    }
}
