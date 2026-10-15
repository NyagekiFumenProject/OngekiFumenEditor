using OngekiFumenEditor.Modules.FumenVisualEditor.Base;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Base.OngekiObjects.Lane.Base;
using OngekiFumenEditor.Properties;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.Interactives.Impls;
namespace OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels
{
 public partial class FumenVisualEditorViewModel
 {
        private SelectionGridDrag selectionGridDrag;

        internal sealed class SelectionGridDrag
        {
            internal sealed class Position
            {
                internal OngekiTimelineObjectBase Object;
                internal int T;
                internal int? X;
                internal LaneStartBase Lane;
                internal bool Selected;
            }

            internal readonly Position[] Before;
            internal readonly long AnchorT;
            internal readonly long AnchorX;
            internal Point GrabOffset;

            internal SelectionGridDrag(IEnumerable<OngekiTimelineObjectBase> selected, IEnumerable<OngekiTimelineObjectBase> related)
            {
                var targets = selected.Distinct().ToArray();
                Before = related.Concat(targets).Distinct().Select(o => new Position
                {
                    Object = o, T = o.TGrid.TotalGrid,
                    X = (o as IHorizonPositionObject)?.XGrid.TotalGrid,
                    Lane = (o as ILaneDockable)?.ReferenceLaneStart,
                    Selected = targets.Contains(o)
                }).ToArray();
                AnchorT = Before.Where(p => p.Selected).Min(p => (long)p.T);
                var xs = Before.Where(p => p.Selected && p.X.HasValue).Select(p => (long)p.X.Value).ToArray();
                AnchorX = xs.Length == 0 ? 0 : (long)Math.Round((xs.Min() + xs.Max()) / 2d);
            }

            internal bool TryMove(long dt, long dx)
            {
                long Time(OngekiTimelineObjectBase o)
                {
                    var p = Before.FirstOrDefault(p => ReferenceEquals(p.Object, o));
                    return p is null ? o.TGrid.TotalGrid : p.T + (p.Selected ? dt : 0);
                }
                foreach (var p in Before.Where(p => p.Selected))
                    if (dt < -(long)p.T || dt > (long)int.MaxValue - p.T ||
                        (p.X.HasValue && (dx < (long)int.MinValue - p.X.Value || dx > (long)int.MaxValue - p.X.Value)))
                        return false;
                foreach (var p in Before)
                {
                    var t = p.T + (p.Selected ? dt : 0);
                    var x = p.X.HasValue ? p.X.Value + (p.Selected ? dx : 0) : 0;
                    if (t < 0 || t > int.MaxValue || x < int.MinValue || x > int.MaxValue)
                        return false;
                    var end = p.Object switch
                    {
                        Hold h => (OngekiTimelineObjectBase)h.HoldEnd,
                        Soflan s => s.EndIndicator,
                        LaneBlockArea b => b.EndIndicator,
                        IndividualSoflanArea a => a.EndIndicator,
                        _ => null
                    };
                    if (p.Object is ConnectableStartObject lane && lane.Children.Any(child => Time(child) < t))
                        return false;
                    if (p.Object is ConnectableStartObject orderedLane)
                    {
                        var previous = t;
                        foreach (var child in orderedLane.Children)
                        {
                            var next = Time(child);
                            if (next < previous) return false;
                            previous = next;
                        }
                    }
                    if (end is not null && Time(end) < t)
                        return false;
                }
                Apply(Before, dt, dx);
                return true;
            }

            internal static void Apply(Position[] positions, long dt = 0, long dx = 0)
            {
                // 父物件先写入，尾节点随后恢复，避免持续物件 setter 钳制尾节点。
                foreach (var p in positions.OrderBy(p => p.Object is HoldEnd or Soflan.SoflanEndIndicator or LaneBlockArea.LaneBlockAreaEndIndicator or IndividualSoflanArea.IndividualSoflanAreaEndIndicator ? 1 : 0))
                {
                    if (p.Object is ILaneDockable dock && !ReferenceEquals(dock.ReferenceLaneStart, p.Lane))
                        dock.ReferenceLaneStart = p.Lane;
                    p.Object.TGrid = TGrid.FromTotalGrid((int)(p.T + (p.Selected ? dt : 0)));
                }
                // 附轨属性通知可能重算尾部 X；最后统一恢复真实横坐标。
                foreach (var p in positions)
                    if (p.Object is IHorizonPositionObject horizontal && p.X is int x)
                        horizontal.XGrid = XGrid.FromTotalGrid((int)(x + (p.Selected ? dx : 0)));
            }

            internal Position[] CaptureCurrent() => Before.Select(p => new Position
            {
                Object = p.Object, T = p.Object.TGrid.TotalGrid,
                X = (p.Object as IHorizonPositionObject)?.XGrid.TotalGrid,
                Lane = (p.Object as ILaneDockable)?.ReferenceLaneStart
            }).ToArray();
        }

        private void BeginSelectionGridDrag(Point point)
        {
            var selected = SelectObjects.OfType<OngekiTimelineObjectBase>().Distinct().ToArray();
            if (selected.Length < 2)
                return;
            var related = new HashSet<OngekiTimelineObjectBase>(selected);
            foreach (var o in selected)
            {
                if (o is Hold h && h.HoldEnd is not null) related.Add(h.HoldEnd);
                if (o is HoldEnd e && e.RefHold is not null) related.Add(e.RefHold);
                if (o is Soflan s) related.Add(s.EndIndicator);
                if (o is Soflan.SoflanEndIndicator se) related.Add(se.RefSoflan);
                if (o is LaneBlockArea b) related.Add(b.EndIndicator);
                if (o is LaneBlockArea.LaneBlockAreaEndIndicator be) related.Add(be.RefLaneBlockArea);
                if (o is IndividualSoflanArea area) related.Add(area.EndIndicator);
                if (o is IndividualSoflanArea.IndividualSoflanAreaEndIndicator areaEnd) related.Add(areaEnd.RefIndividualSoflanArea);
                if (o is ConnectableObjectBase c)
                {
                    related.Add(c.ReferenceStartObject);
                    foreach (var child in c.ReferenceStartObject.Children) related.Add(child);
                }
            }
            var lanes = selected.OfType<ConnectableObjectBase>().Select(c => c.ReferenceStartObject).ToArray();
            foreach (var dock in Fumen.GetAllDisplayableObjects().OfType<OngekiTimelineObjectBase>().Where(o => o is ILaneDockable d && lanes.Contains(d.ReferenceLaneStart)))
                related.Add(dock);
            // 附轨通知可能改写未选 Hold 的尾点，也纳入恢复快照。
            foreach (var hold in related.OfType<Hold>().ToArray())
                if (hold.HoldEnd is not null) related.Add(hold.HoldEnd);
            selectionGridDrag = new SelectionGridDrag(selected, related);
            var anchor = new Point(XGridCalculator.ConvertXGridToX(XGrid.FromTotalGrid((int)selectionGridDrag.AnchorX), this),
                ConvertTGridToY_DesignMode(TGrid.FromTotalGrid((int)selectionGridDrag.AnchorT)));
            selectionGridDrag.GrabOffset = new Point(point.X - anchor.X, point.Y - anchor.Y);
        }

        private void MoveSelectionGridDrag(Point point)
        {
            var drag = selectionGridDrag;
            if (drag is null) return;
            var x = point.X - drag.GrabOffset.X;
            var y = point.Y - drag.GrabOffset.Y;
            var adjust = new Interactives.Impls.DefaultObjectInteractiveAction();
            var snappedX = adjust.CheckAndAdjustX(null, x, this) ?? x;
            var snappedY = adjust.CheckAndAdjustY(null, y, this) ?? y;
            var targetT = ConvertYToTGrid_DesignMode(snappedY);
            if (targetT is null) return;
            var rawX = XGridCalculator.ConvertXToXGridTotalUnit(snappedX, this) * XGrid.DEFAULT_RES_X;
            if (double.IsNaN(rawX) || double.IsInfinity(rawX) || rawX < int.MinValue || rawX > int.MaxValue)
                return;
            var targetX = XGridCalculator.ConvertXToXGrid(snappedX, this);
            drag.TryMove((long)targetT.TotalGrid - drag.AnchorT, (long)targetX.TotalGrid - drag.AnchorX);
        }

        private void EndSelectionGridDrag(bool cancel = false)
        {
            var drag = selectionGridDrag;
            selectionGridDrag = null;
            if (drag is null) return;
            var after = drag.CaptureCurrent();
            if (cancel) SelectionGridDrag.Apply(drag.Before);
            else if (after.Where((p, i) => p.T != drag.Before[i].T || p.X != drag.Before[i].X || !ReferenceEquals(p.Lane, drag.Before[i].Lane)).Any())
                UndoRedoManager.ExecuteAction(LambdaUndoAction.Create(Resources.DragObjects,
                    () => SelectionGridDrag.Apply(after), () => SelectionGridDrag.Apply(drag.Before)));
        }
 }
}
