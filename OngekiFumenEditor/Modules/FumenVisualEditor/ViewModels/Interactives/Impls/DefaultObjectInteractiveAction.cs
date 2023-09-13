using Gemini.Modules.UndoRedo.Services;
using NAudio.Gui;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects.Lane.Base;
using OngekiFumenEditor.Modules.FumenVisualEditor.Base;
using OngekiFumenEditor.Modules.FumenVisualEditor.Models;
using OngekiFumenEditor.Utils;
using OngekiFumenEditor.Utils.ObjectPool;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.Interactives.Impls
{
    public class DefaultObjectInteractiveAction : ObjectInteractiveActionBase
    {
        private Dictionary<OngekiObjectBase, DragInfo> dragInfoMap = new();

        private struct DragInfo
        {
            public Point CanvasPoint { get; set; }
            public Point Point { get; set; }

            public XGrid XGrid { get; set; }
            public TGrid TGrid { get; set; }

            public LaneStartBase RefLane { get; set; }
        }

        public override void OnDragEnd(OngekiObjectBase obj, Point point, FumenVisualEditorViewModel editor)
        {
            if (dragInfoMap.TryGetValue(obj, out var info))
                dragInfoMap.Remove(obj);
            else
                return;//不应该走到这

            var dragStartCanvasPoint = info.CanvasPoint;
            var x = obj is IHorizonPositionObject horizonPositionObject ? XGridCalculator.ConvertXGridToX(horizonPositionObject.XGrid, editor) : 0;
            var y = obj is ITimelineObject timelineObject ? TGridCalculator.ConvertTGridToY_DesignMode(timelineObject.TGrid, editor) : 0;

            OnDragMove(obj, point, editor);
            var newPos = new Point(x, y);

            editor.UndoRedoManager.ExecuteAction(LambdaUndoAction.Create("物件拖动",
                () =>
                {
                    OnMoveCanvas(obj, newPos, editor);
                }, () =>
                {
                    //不直接move了，直接设置位置就行
                    if (obj is IHorizonPositionObject horizonPositionObject)
                        horizonPositionObject.XGrid = info.XGrid;
                    if (obj is ITimelineObject timelineObject)
                        timelineObject.TGrid = info.TGrid;
                    if (obj is ILaneDockable dockable)
                        dockable.ReferenceLaneStart = info.RefLane;
                }));

            //Log.LogDebug($"OnObjectDragEnd: ({pos.X:F2},{pos.Y:F2}) -> ({x:F2},{y:F2})");
        }

        public override void OnDragMove(OngekiObjectBase obj, Point pos, FumenVisualEditorViewModel editor)
        {
            if (!dragInfoMap.TryGetValue(obj, out var info))
                return;//不应该走到这

            var dragStartCanvasPoint = info.CanvasPoint;
            var dragStartPoint = info.Point;

            var movePoint = new Point(
                dragStartCanvasPoint.X + (pos.X - dragStartPoint.X),
                dragStartCanvasPoint.Y + (pos.Y - dragStartPoint.Y)
                );

            //这里限制一下
            //movePoint.X = Math.Max(0, Math.Min(editor.TotalDurationHeight, movePoint.X));
            movePoint.Y = Math.Max(0, Math.Min(editor.TotalDurationHeight, movePoint.Y));

            //Log.LogDebug($"OnObjectDragMoving: ({pos.X:F2},{pos.Y:F2}) -> ({movePoint.X:F2},{movePoint.Y:F2})");

            OnMoveCanvas(obj, movePoint, editor);
        }

        public override void OnDragStart(OngekiObjectBase obj, Point pos, FumenVisualEditorViewModel editor)
        {
            var info = new DragInfo();

            var x = 0d;
            if (obj is IHorizonPositionObject horizonPositionObject)
            {
                x = XGridCalculator.ConvertXGridToX(horizonPositionObject.XGrid, editor);
                if (double.IsNaN(x))
                    x = default;
                info.XGrid = horizonPositionObject.XGrid.CopyNew();
            }

            var y = 0d;
            if (obj is ITimelineObject timelineObject)
            {
                y = TGridCalculator.ConvertTGridToY_DesignMode(timelineObject.TGrid, editor);
                if (double.IsNaN(y))
                    y = default;
                info.TGrid = timelineObject.TGrid.CopyNew();
            }

            if (obj is ILaneDockable dockable)
            {
                info.RefLane = dockable.ReferenceLaneStart;
            }

            info.CanvasPoint = new Point(x, y);
            info.Point = pos;

            dragInfoMap[obj] = info;

            //Log.LogDebug($"OnObjectDragStart: ({pos.X:F2},{pos.Y:F2}) -> ({x:F2},{y:F2})");
        }

        public override void OnMoveCanvas(OngekiObjectBase obj, Point point, FumenVisualEditorViewModel editor)
        {
            if (obj is ITimelineObject timeObj)
            {
                var ry = CheckAndAdjustY(timeObj, point.Y, editor);
                if (ry is double dry && TGridCalculator.ConvertYToTGrid_DesignMode(dry, editor) is TGrid tGrid)
                {
                    timeObj.TGrid = tGrid;
                    //Log.LogInfo($"Y: {ry} , TGrid: {timeObj.TGrid}");
                }
            }

            if (obj is IHorizonPositionObject posObj)
            {
                var rx = CheckAndAdjustX(posObj, point.X, editor);
                if (rx is double drx)
                {
                    var xGrid = XGridCalculator.ConvertXToXGrid(drx, editor);
                    posObj.XGrid = xGrid;
                }

                //Log.LogDebug($"x : {rx:F4} , posObj.XGrid.Unit : {posObj.XGrid.Unit} , xConvertBack : {XGridCalculator.ConvertXGridToX(posObj.XGrid, this)}");
            }
        }

        public virtual double? CheckAndAdjustY(ITimelineObject obj, double y, FumenVisualEditorViewModel editor)
        {
            var enableMagneticAdjust = !editor.Setting.DisableTGridMagneticDock;
            if (!enableMagneticAdjust)
                return y;

            var forceMagneticAdjust = editor.Setting.ForceMagneticDock;
            var fin = forceMagneticAdjust ? TGridCalculator.TryPickClosestBeatTime((float)y, editor, editor.Setting.TGridUnitLength) : TGridCalculator.TryPickMagneticBeatTime((float)y, 4, editor, editor.Setting.TGridUnitLength);
            var ry = fin.y;
            if (fin.tGrid == null)
                ry = y;
            //Log.LogDebug($"before y={y:F2} ,select:({fin.tGrid}) ,fin:{ry:F2}");
            return ry;
        }

        public virtual double? CheckAndAdjustX(IHorizonPositionObject obj, double x, FumenVisualEditorViewModel editor)
        {
            //todo 基于二分法查询最近
            var enableMagneticAdjust = !editor.Setting.DisableXGridMagneticDock;
            var forceMagneticAdjust = editor.Setting.ForceMagneticDock || editor.Setting.ForceXGridMagneticDock;
            var dockableTriggerDistance = forceMagneticAdjust ? int.MaxValue : 4;

            var nearestUnitLine = (enableMagneticAdjust ? editor.CachedMagneticXGridLines.Select(z =>
            (
                Math.Abs(z.X - x),
                z.X,
                true
            )).Where(z => z.Item1 < dockableTriggerDistance).OrderBy(x => x.Item1)
            : Enumerable.Empty<(double, float, bool)>()).FirstOrDefault();

            double? fin = nearestUnitLine.Item3 ? nearestUnitLine.Item2 : forceMagneticAdjust ? null : x;
            //Log.LogInfo($"nearestUnitLine x:{x:F2} distance:{nearestUnitLine?.distance:F2} fin:{fin}");
            return fin;
        }
    }
}
