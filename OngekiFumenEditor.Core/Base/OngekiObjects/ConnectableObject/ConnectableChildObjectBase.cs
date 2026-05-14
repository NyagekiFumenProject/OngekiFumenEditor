using OngekiFumenEditor.Core.Base.Attributes;
using OngekiFumenEditor.Core.Base.EditorObjects.LaneCurve;
using OngekiFumenEditor.Core.Kernel.CurveInterpolater;
using OngekiFumenEditor.Core.Kernel.CurveInterpolater.OgkrImpl.Factory;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace OngekiFumenEditor.Core.Base.OngekiObjects.ConnectableObject
{
    public abstract class ConnectableChildObjectBase : ConnectableObjectBase
    {
        public override LaneType LaneType => ReferenceStartObject?.LaneType ?? default;

        public bool IsEndObject => NextObject is null;

        private float curvePrecision = 0.025f;

        [LocalizableObjectPropertyBrowserAlias("CurvePrecisionLabel")]
        public float CurvePrecision
        {
            get => curvePrecision;
            set => Set(ref curvePrecision, value <= 0 ? 0.01f : value);
        }

        private ICurveInterpolaterFactory curveInterpolaterFactory = XGridLimitedCurveInterpolaterFactory.Default;

        [LocalizableObjectPropertyBrowserAlias("CurveInterpolatorFactoryLabel")]
        public ICurveInterpolaterFactory CurveInterpolaterFactory
        {
            get => curveInterpolaterFactory;
            set => Set(ref curveInterpolaterFactory, value);
        }

        public bool IsAnyControlSelecting => PathControls.Any(x => x.IsSelected);

        private ConnectableObjectBase prevObject;
        public ConnectableObjectBase PrevObject
        {
            get => prevObject;
            set
            {
                if (prevObject is not null)
                    prevObject.NextObject = default;
                Set(ref prevObject, value);
                if (prevObject is not null)
                    prevObject.NextObject = this;
                NotifyRefreshPaths();
            }
        }

        private ConnectableStartObject referenceStartObject;
        public override ConnectableStartObject ReferenceStartObject => referenceStartObject;

        private int recordId = int.MinValue;
        public override int RecordId { get => ReferenceStartObject?.RecordId ?? recordId; set => Set(ref recordId, value); }

        private readonly List<LaneCurvePathControlObject> pathControls = new List<LaneCurvePathControlObject>();
        public IReadOnlyList<LaneCurvePathControlObject> PathControls => pathControls;

        public bool IsCurvePath => PathControls.Count > 0;
        public bool IsVaildPath
        {
            get
            {
                if (cacheGeneratedPath is null)
                    RegeneratePaths();

                return cachedIsVaild;
            }
        }

        private bool cachedIsVaild;
        private List<(Vector2 pos, bool isVaild)> cacheGeneratedPath;

        public void SetReferenceStartObject(ConnectableStartObject refStart)
        {
            referenceStartObject = refStart;
        }

        public void AddControlObject(LaneCurvePathControlObject controlObj)
        {
            InsertControlObject(PathControls.Count, controlObj);
        }

        public void InsertControlObject(int index, LaneCurvePathControlObject controlObj)
        {
#if DEBUG
            if (controlObj.RefCurveObject is not null)
                throw new Exception("controlObj is using");
#endif

            pathControls.Insert(index, controlObj);
            for (int i = index; i < pathControls.Count; i++)
                pathControls[i].Index = i;
            controlObj.PropertyChanged += ControlObj_PropertyChanged;
            controlObj.RefCurveObject = this;
            NotifyRefreshPaths();
            NotifyOfPropertyChange(() => PathControls);
        }

        private void ControlObj_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(IsSelected):
                    NotifyOfPropertyChange(() => IsAnyControlSelecting);
                    break;
                case nameof(TGrid):
                case nameof(XGrid):
                    NotifyRefreshPaths();
                    NotifyOfPropertyChange(e.PropertyName);
                    break;
                default:
                    break;
            }
        }

        internal void NotifyRefreshPaths()
        {
            cacheGeneratedPath = default;
            cachedIsVaild = default;
        }

        private void RegeneratePaths()
        {
            cacheGeneratedPath = new List<(Vector2 pos, bool isVaild)>();

            var isVaild = true;
            foreach (var p in GenerateConnectionPaths())
            {
                cacheGeneratedPath.Add(p);
                isVaild = isVaild && p.isVaild;
            }

            cachedIsVaild = isVaild;
        }

        public void RemoveControlObject(LaneCurvePathControlObject controlObj)
        {
            if (pathControls.Remove(controlObj))
            {
                controlObj.RefCurveObject = null;
                controlObj.PropertyChanged -= ControlObj_PropertyChanged;
                NotifyRefreshPaths();
                NotifyOfPropertyChange(() => PathControls);
            }
        }

        public IEnumerable<Vector2> GridBasePoints => PathControls
            .Cast<OngekiMovableObjectBase>()
            .Prepend(PrevObject)
            .Append(this)
            .OfType<OngekiMovableObjectBase>()
            .Select(x => new Vector2(x.XGrid.TotalGrid, x.TGrid.TotalGrid));

        private static Vector2 CalculateBezierPoint(IReadOnlyList<Vector2> points, float t)
        {
            var temp = new Vector2[points.Count];
            for (int i = 0; i < points.Count; i++)
                temp[i] = points[i];

            for (int depth = 1; depth < points.Count; depth++)
            {
                for (int i = 0; i < points.Count - depth; i++)
                    temp[i] = Vector2.Lerp(temp[i], temp[i + 1], t);
            }

            return temp[0];
        }

        private static int BinarySearchByPathY(IReadOnlyList<(Vector2 pos, bool isVaild)> list, double value)
        {
            var lo = 0;
            var hi = list.Count - 1;
            while (lo <= hi)
            {
                var i = lo + ((hi - lo) >> 1);
                double current = list[i].pos.Y;
                var order = current.CompareTo(value);

                if (order == 0)
                {
                    for (var r = i + 1; r < list.Count; r++)
                    {
                        if (((double)list[r].pos.Y).CompareTo(current) == 0)
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

        private static double CalculateXFromTwoPointFormFormula(double y, double x1, double y1, double x2, double y2)
        {
            var by = y2 - y1;
            var bx = x2 - x1;

            if (by == 0)
                return x1;

            return (y - y1) / by * bx + x1;
        }

        public IEnumerable<(Vector2 pos, bool isVaild)> GenerateConnectionPaths()
        {
            int calcSign(Vector2 a, Vector2 b)
            {
                if (a.Y == b.Y)
                    return 1;

                return Math.Sign(b.Y - a.Y);
            }

            var points = GridBasePoints.ToList();
            if (points.Count <= 2)
            {
                var fromP = points[0];
                var toP = points[1];
                yield return (fromP, true);
                yield return (toP, toP.Y >= fromP.Y);
                yield break;
            }

            var prevPos = points[0];
            var prevSign = 0;
            var step = CurvePrecision;
            var isVaild = true;

            var t = 0f;
            while (true)
            {
                var curP = CalculateBezierPoint(points, t);
                var sign = calcSign(prevPos, curP);

                if (prevSign != sign && prevSign != 0)
                    isVaild = false;

                prevPos = curP;
                prevSign = sign;

                yield return (curP, isVaild);

                if (t >= 1)
                    break;

                t = Math.Min(1f, t + step);
            }
        }

        public IReadOnlyList<(Vector2 pos, bool isVaild)> GetConnectionPaths()
        {
            if (cacheGeneratedPath is null)
                RegeneratePaths();

            return cacheGeneratedPath;
        }

        public double? CalulateXGridTotalGrid(double totalTGrid)
        {
            if (PathControls.Count > 0)
            {
                Vector2? prevVec2 = null;

                if (IsVaildPath)
                {
                    var pathList = cacheGeneratedPath;
                    var idx = BinarySearchByPathY(pathList, totalTGrid);
                    var actualIdx = idx < 0 ? Math.Max(0, (~idx) - 1) : idx;

                    if (actualIdx == pathList.Count - 1)
                        return pathList[pathList.Count - 1].pos.X;
                    if (actualIdx == 0 && pathList[0].pos.Y > totalTGrid)
                        return default;

                    var cur = pathList[actualIdx];
                    var next = pathList[actualIdx + 1];

                    return CalculateXFromTwoPointFormFormula(totalTGrid, cur.pos.X, cur.pos.Y, next.pos.X, next.pos.Y);
                }

                foreach (var item in GetConnectionPaths())
                {
                    var gridVec2 = item.pos;
                    var isVaild = item.isVaild;
                    if (!isVaild)
                        return default;

                    if (totalTGrid <= gridVec2.Y)
                    {
                        prevVec2 = prevVec2 ?? gridVec2;

                        var fromXGrid = prevVec2.Value.X;
                        var fromTGrid = prevVec2.Value.Y;
                        var toTGrid = gridVec2.Y;
                        var toXGrid = gridVec2.X;

                        return CalculateXFromTwoPointFormFormula(totalTGrid, fromXGrid, fromTGrid, toXGrid, toTGrid);
                    }

                    prevVec2 = gridVec2;
                }

                return default;
            }

            return CalculateXFromTwoPointFormFormula(totalTGrid, PrevObject.XGrid.TotalGrid, PrevObject.TGrid.TotalGrid, XGrid.TotalGrid, TGrid.TotalGrid);
        }

        public XGrid CalulateXGrid(TGrid tGrid)
        {
            if (CalulateXGridTotalGrid(tGrid.TotalGrid) is not double totalGrid)
                return default;

            var xGrid = new XGrid(0, (int)totalGrid);
            xGrid?.NormalizeSelf();
            return xGrid;
        }

        public bool CheckCurveVaild()
        {
            return GetConnectionPaths().All(x => x.isVaild);
        }

        public override IEnumerable<IDisplayableObject> GetDisplayableObjects()
        {
            return PathControls.Cast<IDisplayableObject>().Append(this);
        }

        public override bool CheckVisiable(TGrid minVisibleTGrid, TGrid maxVisibleTGrid)
        {
            return base.CheckVisiable(minVisibleTGrid, maxVisibleTGrid) || (TGrid > maxVisibleTGrid && PrevObject is not null && PrevObject.TGrid < minVisibleTGrid);
        }

        public override string ToString() => $"{base.ToString()} {(PathControls.Count > 0 ? $"CurveCount[{PathControls.Count}]" : string.Empty)} RefStart[{ReferenceStartObject}]";

        public override void Copy(OngekiObjectBase fromObj)
        {
            base.Copy(fromObj);

            if (fromObj is not ConnectableChildObjectBase from)
                return;

            RecordId = -Math.Abs(from.RecordId);
            SetReferenceStartObject(null);
            PrevObject = null;
            CurvePrecision = from.CurvePrecision;
            CurveInterpolaterFactory = from.CurveInterpolaterFactory;
            foreach (var cp in from.PathControls)
            {
                var newCP = new LaneCurvePathControlObject();
                newCP.Copy(cp);
                AddControlObject(newCP);
            }
        }

        public IEnumerable<ConnectableChildObjectBase> InterpolateCurveChildren(ICurveInterpolaterFactory factory = default)
        {
            var to = ReferenceStartObject.FindNextChild(this);
            var itor = (factory ?? CurveInterpolaterFactory).CreateInterpolaterForRange(this, to);

            while (true)
            {
                if (itor.EnumerateNext() is not CurvePoint point)
                    break;

                var newNext = ReferenceStartObject.CreateChildObject();

                newNext.Copy(this);
                foreach (var ctrl in newNext.PathControls.ToArray())
                    newNext.RemoveControlObject(ctrl);

                newNext.TGrid = point.TGrid;
                newNext.XGrid = point.XGrid;

                yield return newNext;
            }
        }
    }
}
