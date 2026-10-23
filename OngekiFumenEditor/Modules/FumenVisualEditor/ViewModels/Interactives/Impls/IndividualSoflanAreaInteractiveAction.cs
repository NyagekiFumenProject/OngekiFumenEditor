using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Modules.FumenVisualEditor.Base;
using OngekiFumenEditor.Properties;
using System;
using System.Collections.Generic;
using System.Windows;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.Interactives.Impls
{
    /// <summary>
    /// ISA 的两个三角角标是矩形的左下 / 右上顶点。
    /// 拖动期间被拖的顶点直接跟手（允许暂时越过另一顶点，矩形按 min/max 绘制）；
    /// 拖动结束后调用 <see cref="IndividualSoflanArea.NormalizeCorners"/>，把左下顶点写回 ISA 本体、
    /// 右上顶点写回 End，因此无论从哪一侧拖、拖到哪里，松手后语义都保持「本体=左下、End=右上」。
    /// </summary>
    internal sealed class IndividualSoflanAreaInteractiveAction : DefaultObjectInteractiveAction
    {
        private sealed class DragInfo
        {
            public OngekiObjectBase Dragged { get; init; }
            public IndividualSoflanArea Area { get; init; }

            public double CanvasX { get; init; }
            public double CanvasY { get; init; }
            public Point Point { get; init; }

            public XGrid StartX { get; init; }
            public TGrid StartT { get; init; }
            public XGrid EndX { get; init; }
            public TGrid EndT { get; init; }
        }

        private readonly Dictionary<OngekiObjectBase, DragInfo> dragInfoMap = new();

        public override void OnDragStart(OngekiObjectBase obj, Point pos, FumenVisualEditorViewModel editor)
        {
            if (ResolveArea(obj) is not { } area)
                return;

            var canvasX = 0d;
            if (obj is IHorizonPositionObject horizonPositionObject)
            {
                canvasX = XGridCalculator.ConvertXGridToX(horizonPositionObject.XGrid, editor);
                if (double.IsNaN(canvasX))
                    canvasX = default;
            }

            var canvasY = 0d;
            if (obj is ITimelineObject timelineObject)
            {
                canvasY = editor.ConvertTGridToY_DesignMode(timelineObject.TGrid);
                if (double.IsNaN(canvasY))
                    canvasY = default;
            }

            dragInfoMap[obj] = new DragInfo
            {
                Dragged = obj,
                Area = area,
                CanvasX = canvasX,
                CanvasY = canvasY,
                Point = pos,
                StartX = area.XGrid.CopyNew(),
                StartT = area.TGrid.CopyNew(),
                EndX = area.EndIndicator.XGrid.CopyNew(),
                EndT = area.EndIndicator.TGrid.CopyNew(),
            };
        }

        public override void OnDragMove(OngekiObjectBase obj, Point pos, FumenVisualEditorViewModel editor)
        {
            if (dragInfoMap.TryGetValue(obj, out var info))
                ApplyDrag(info, pos, editor);
        }

        public override void OnDragEnd(OngekiObjectBase obj, Point point, FumenVisualEditorViewModel editor)
        {
            if (!dragInfoMap.TryGetValue(obj, out var info))
                return;

            dragInfoMap.Remove(obj);

            ApplyDrag(info, point, editor);

            var area = info.Area;
            area.NormalizeCorners();

            var finalStartX = area.XGrid.CopyNew();
            var finalStartT = area.TGrid.CopyNew();
            var finalEndX = area.EndIndicator.XGrid.CopyNew();
            var finalEndT = area.EndIndicator.TGrid.CopyNew();

            editor.UndoRedoManager.ExecuteAction(LambdaUndoAction.Create(Resources.DragObjects,
                () =>
                {
                    area.XGrid = finalStartX.CopyNew();
                    area.TGrid = finalStartT.CopyNew();
                    area.EndIndicator.XGrid = finalEndX.CopyNew();
                    area.EndIndicator.TGrid = finalEndT.CopyNew();
                },
                () =>
                {
                    area.XGrid = info.StartX.CopyNew();
                    area.TGrid = info.StartT.CopyNew();
                    area.EndIndicator.XGrid = info.EndX.CopyNew();
                    // 拖动前可能正好是反转状态，恢复时不能走会夹到本体位置的 setter。
                    area.EndIndicator.SetTGridUnclamped(info.EndT.CopyNew());
                }));
        }

        private void ApplyDrag(DragInfo info, Point pos, FumenVisualEditorViewModel editor)
        {
            var movePoint = new Point(
                info.CanvasX + (pos.X - info.Point.X),
                info.CanvasY + (pos.Y - info.Point.Y));
            movePoint.Y = Math.Max(0, Math.Min(editor.TotalDurationHeight, movePoint.Y));

            if (CheckAndAdjustY(info.Area, movePoint.Y, editor) is not double adjustedY)
                return;
            if (CheckAndAdjustX(info.Area, movePoint.X, editor) is not double adjustedX)
                return;

            var tGrid = editor.ConvertYToTGrid_DesignMode(adjustedY);
            var xGrid = XGridCalculator.ConvertXToXGrid(adjustedX, editor);
            if (editor.Setting.ForceXGridIntegerUnit)
                xGrid = XGridCalculator.RoundToIntegerUnit(xGrid);

            if (info.Dragged is IndividualSoflanArea.IndividualSoflanAreaEndIndicator endIndicator)
            {
                endIndicator.XGrid = xGrid;
                endIndicator.SetTGridUnclamped(tGrid);
            }
            else
            {
                info.Area.XGrid = xGrid;
                info.Area.TGrid = tGrid;
            }
        }

        private static IndividualSoflanArea ResolveArea(OngekiObjectBase obj) => obj switch
        {
            IndividualSoflanArea area => area,
            IndividualSoflanArea.IndividualSoflanAreaEndIndicator endIndicator => endIndicator.RefIndividualSoflanArea,
            _ => null,
        };
    }
}
