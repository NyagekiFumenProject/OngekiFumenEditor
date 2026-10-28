using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects.Lane.Base;
using OngekiFumenEditor.Modules.FumenVisualEditor.Base;
using OngekiFumenEditor.Properties;
using OngekiFumenEditor.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using static OngekiFumenEditor.Modules.FumenVisualEditor.Graphics.Drawing.Editors.DrawXGridHelper;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.Interactives.Impls
{
    public class DefaultObjectInteractiveAction : ObjectInteractiveActionBase
    {
        /// <summary>
        /// 本帧该物件的水平位置是否由所吸附的轨道接管（吸附物件贴合轨道）。
        /// 由 DockableObjectInteractiveAction / HoldEndObjectInteractiveAction 在改写坐标时置 true；
        /// true 时 ForceXGridIntegerUnit 不做整数单位取整。
        /// </summary>
        protected virtual bool IsXGridSnappedToLaneThisMove => false;

        public override void OnDragStart(OngekiObjectBase obj, Point point, FumenVisualEditorViewModel editor)
        {
            if (obj is OngekiTimelineObjectBase timeline)
                editor.InteractiveManager.BeginSelectionDrag(editor, point, new[] { timeline });
        }

        public override void OnDragMove(OngekiObjectBase obj, Point point, FumenVisualEditorViewModel editor)
        {
            editor.InteractiveManager.UpdateSelectionDrag(editor, point);
        }

        public override void OnDragEnd(OngekiObjectBase obj, Point point, FumenVisualEditorViewModel editor)
        {
            editor.InteractiveManager.UpdateSelectionDrag(editor, point);
            editor.InteractiveManager.CommitSelectionDrag(editor);
        }

        public override void OnMoveCanvas(OngekiObjectBase obj, Point point, FumenVisualEditorViewModel editor)
        {
            if (obj is ITimelineObject timeObj && ResolveTGrid(timeObj, point.Y, editor) is { } tGrid)
                timeObj.TGrid = tGrid;

            if (obj is IHorizonPositionObject posObj && ResolveXGrid(posObj, point.X, editor) is { } xGrid)
                posObj.XGrid = xGrid;
        }

        public bool TryMoveCanvas(OngekiMovableObjectBase obj, Point point, FumenVisualEditorViewModel editor)
        {
            var tGrid = ResolveTGrid(obj, point.Y, editor);
            var xGrid = ResolveXGrid(obj, point.X, editor);
            if (tGrid is null || xGrid is null)
                return false;

            obj.TGrid = tGrid;
            obj.XGrid = xGrid;
            return true;
        }

        private TGrid ResolveTGrid(ITimelineObject obj, double y, FumenVisualEditorViewModel editor)
            => CheckAndAdjustY(obj, y, editor) is double adjustedY
                ? editor.ConvertYToTGrid_DesignMode(adjustedY)
                : null;

        private XGrid ResolveXGrid(IHorizonPositionObject obj, double x, FumenVisualEditorViewModel editor)
        {
            if (CheckAndAdjustX(obj, x, editor) is not double adjustedX)
                return null;

            var xGrid = XGridCalculator.ConvertXToXGrid(adjustedX, editor);
            return editor.Setting.ForceXGridIntegerUnit && !IsXGridSnappedToLaneThisMove
                ? XGridCalculator.RoundToIntegerUnit(xGrid)
                : xGrid;
        }

        public override bool TryAdjustGrid(OngekiObjectBase obj, ref TGrid time, ref XGrid x, FumenVisualEditorViewModel editor, bool preserveTime = false)
        {
            if (time != null && !preserveTime && !editor.Setting.DisableTGridMagneticDock)
            {
                if (AdjustCandidateY((ITimelineObject)obj, editor.ConvertTGridToY_DesignMode(time), editor) is double y)
                    time = editor.ConvertYToTGrid_DesignMode(y);
                if (time == null || time.TotalGrid < 0)
                    return false;
            }
            if (x != null && !editor.Setting.DisableXGridMagneticDock)
            {
                if (AdjustCandidateX((IHorizonPositionObject)obj, XGridCalculator.ConvertXGridToX(x, editor), editor) is double px)
                {
                    var total = XGridCalculator.ConvertXToXGridTotalUnit(px, editor) * XGrid.DEFAULT_RES_X;
                    if (!double.IsFinite(total) || total < int.MinValue || total > int.MaxValue)
                        return false;
                    x = XGridCalculator.ConvertXToXGrid(px, editor);
                }
                if (editor.Setting.ForceXGridIntegerUnit)
                    x = XGridCalculator.RoundToIntegerUnit(x);
            }
            return true;
        }

        protected virtual double? AdjustCandidateY(ITimelineObject obj, double y, FumenVisualEditorViewModel editor)
            => CheckAndAdjustY(obj, y, editor);

        protected virtual double? AdjustCandidateX(IHorizonPositionObject obj, double x, FumenVisualEditorViewModel editor)
            => CheckAndAdjustX(obj, x, editor);

        public virtual double? CheckAndAdjustY(ITimelineObject obj, double y, FumenVisualEditorViewModel editor)
        {
            var enableMagneticAdjust = !editor.Setting.DisableTGridMagneticDock;
            if (!enableMagneticAdjust)
                return y;

            var forceMagneticAdjust = editor.Setting.ForceMagneticDock;
            var fin = forceMagneticAdjust ? editor.TryPickClosestBeatTime((float)y) : editor.TryPickMagneticBeatTime((float)y, 4);
            var ry = fin.y;
            if (fin.tGrid == null)
                ry = y;
            //Log.LogDebug($"before y={y:F2} ,select:({fin.tGrid}) ,fin:{ry:F2}");
            return ry;
        }

        public virtual double? CheckAndAdjustX(IHorizonPositionObject obj, double x, FumenVisualEditorViewModel editor)
        {
            var enableMagneticAdjust = !editor.Setting.DisableXGridMagneticDock;
            var forceMagneticAdjust = editor.Setting.ForceMagneticDock || editor.Setting.ForceXGridMagneticDock;
            var dockableTriggerDistance = forceMagneticAdjust ? int.MaxValue : (int)LaneGeometry.XGridMagneticDistancePixels;

            IEnumerable<double> calc2()
            {
                var xOffset = (float)editor.Setting.XOffset;

                var unitSize = (float)XGridCalculator.CalculateXUnitSize(editor);
                var baseX = editor.RectInDesignMode.Width / 2 + xOffset;

                var rX = x - baseX;
                var sign = Math.Sign(rX);

                var xpX = (int)(Math.Abs(rX) / unitSize);

                yield return baseX + xpX * unitSize * sign;
                yield return baseX + (xpX + 1) * unitSize * sign;
            }

            var nearestUnitLine2 = (enableMagneticAdjust ? calc2().Select(z => (Math.Abs(z - x), z, true))
                .Where(z => z.Item1 < dockableTriggerDistance).OrderBy(x => x.Item1)
            : Enumerable.Empty<(double, double, bool)>()).FirstOrDefault();
            var fin2 = nearestUnitLine2.Item3 ? nearestUnitLine2.Item2 : forceMagneticAdjust ? default(double?) : x;

            return fin2;
        }
    }
}
