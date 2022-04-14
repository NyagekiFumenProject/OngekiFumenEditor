using OngekiFumenEditor.Base.EditorObjects.LaneCurve;
using OngekiFumenEditor.Utils;
using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Base.OngekiObjects.ConnectableObject
{
    public abstract class ConnectableChildObjectBase : ConnectableObjectBase
    {
        private float curvePrecision = 0.1f;
        public float CurvePrecision
        {
            get => curvePrecision;
            set => Set(ref curvePrecision, value <= 0 ? 0.01f : value);
        }

        public bool IsAnyControlSelecting => PathControls.Any(x => x.IsSelected);

        public ConnectableStartObject ReferenceStartObject { get; set; }
        public ConnectableObjectBase PrevObject { get; set; }
        private int recordId = int.MinValue;
        internal int CacheRecoveryChildIndex { get; set; } = -1;
        public override int RecordId { get => ReferenceStartObject?.RecordId ?? recordId; set => Set(ref recordId, value); }
        private List<LaneCurvePathControlObject> pathControls = new();
        public IReadOnlyList<LaneCurvePathControlObject> PathControls => pathControls;
        public bool IsCurvePath => PathControls.Count > 0;

        public void AddControlObject(LaneCurvePathControlObject controlObj)
        {
#if DEBUG
            if (controlObj.RefCurveObject is not null)
                throw new Exception("controlObj is using");
#endif

            pathControls.Add(controlObj);
            controlObj.Index = PathControls.Count;
            controlObj.PropertyChanged += ControlObj_PropertyChanged;
            controlObj.RefCurveObject = this;
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
                    NotifyOfPropertyChange(e.PropertyName);
                    break;
                default:
                    break;
            }
        }

        public void RemoveControlObject(LaneCurvePathControlObject controlObj)
        {
            if (pathControls.Remove(controlObj))
            {
                controlObj.RefCurveObject = null;
                controlObj.PropertyChanged -= ControlObj_PropertyChanged;
                NotifyOfPropertyChange(() => PathControls);
            }
        }

        public IEnumerable<Vector2> GridBasePoints => PathControls
            .AsEnumerable<OngekiMovableObjectBase>()
            .Prepend(PrevObject)
            .Append(this)
            .Select(x => new Vector2(x.XGrid.TotalGrid, x.TGrid.TotalGrid));

        public IEnumerable<(Vector2 pos, bool isVaild)> GenPath()
        {
            int calcSign(Vector2 a, Vector2 b)
            {
                if (a.Y == b.Y)
                    return 1;

                return Math.Sign(b.Y - a.Y);
            }

            using var d = GridBasePoints.ToListWithObjectPool(out var points);
            if (points.Count <= 2)
            {
                yield return (points[0], true);
                yield return (points[1], true);
                yield break;
            }

            var prevPos = points[0];
            var prevSign = 0;
            var step = CurvePrecision;
            var isVaild = true;

            var t = 0f;
            while (true)
            {
                var curP = BezierCurve.CalculatePoint(points, t);
                var sign = calcSign(prevPos, curP);

                if (prevSign != sign && prevSign != 0)
                    isVaild = isVaild && false;

                prevPos = curP;
                prevSign = sign;

                yield return (curP, isVaild);

                if (t >= 1)
                    break;

                t = MathF.Min(1, t + step);
            }
        }

        public XGrid CalulateXGrid(TGrid tGrid)
        {
            if (PathControls.Count > 0)
            {
                Vector2? prevVec2 = null;
                var totalGrid = tGrid.TotalGrid;

                foreach ((var gridVec2, var isVaild) in GenPath())
                {
                    if (!isVaild)
                        return default;

                    if (totalGrid <= gridVec2.Y)
                    {
                        prevVec2 = prevVec2 ?? gridVec2;

                        var fromXGrid = new TGrid(prevVec2.Value.Y / TGrid.ResT);
                        fromXGrid.NormalizeSelf();
                        var fromTGrid = new XGrid(prevVec2.Value.X / XGrid.ResX);
                        fromTGrid.NormalizeSelf();
                        var toTGrid = new TGrid(gridVec2.Y / TGrid.ResT);
                        toTGrid.NormalizeSelf();
                        var toXGrid = new XGrid(gridVec2.X / XGrid.ResX);
                        toXGrid.NormalizeSelf();

                        var xGrid = MathUtils.CalculateXGridFromBetweenObjects(fromXGrid, fromTGrid, toTGrid, toXGrid, tGrid);

                        //Log.LogDebug($"fromXGrid:{fromXGrid} fromTGrid:{fromTGrid} fromTGrid:{fromTGrid} fromTGrid:{fromTGrid} tGrid:{tGrid} -> {xGrid}");
                        return xGrid;
                    }

                    prevVec2 = gridVec2;
                }

                return default;
            }
            else
            {
                //就在当前[prev,cur]范围内，那么就插值计算咯
                var xGrid = MathUtils.CalculateXGridFromBetweenObjects(PrevObject.TGrid, PrevObject.XGrid, TGrid, XGrid, tGrid);
                return xGrid;
            }
        }

        public bool CheckCurveVaild()
        {
            return GenPath().All(x => x.isVaild);
        }

        public override IEnumerable<IDisplayableObject> GetDisplayableObjects()
        {
            return PathControls.AsEnumerable<IDisplayableObject>().Append(this);
        }

        public override bool CheckVisiable(TGrid minVisibleTGrid, TGrid maxVisibleTGrid)
        {
            return base.CheckVisiable(minVisibleTGrid, maxVisibleTGrid) || (TGrid > maxVisibleTGrid && PrevObject is not null && PrevObject.TGrid < minVisibleTGrid);
        }

        public override string ToString() => $"{base.ToString()} {RecordId} Ref:{ReferenceStartObject} {(PathControls.Count > 0 ? $"CurveCount:{PathControls.Count}" : string.Empty)}";
    }
}
