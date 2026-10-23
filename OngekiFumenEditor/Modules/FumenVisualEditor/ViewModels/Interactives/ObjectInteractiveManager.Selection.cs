using OngekiFumenEditor.Modules.FumenVisualEditor.Base;
using System;
using System.Collections.Generic;
using System.Linq;
using System.ComponentModel;
using System.Windows;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Base.OngekiObjects.Lane.Base;
using OngekiFumenEditor.Properties;
using OngekiFumenEditor.Base.EditorObjects.LaneCurve;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.Interactives.Impls;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.Interactives
{
    public partial class ObjectInteractiveManager
    {
        internal SelectionGridDrag activeSelectionDrag;
        private bool isDirtyBeforeDrag;
        private object[] undoStackBeforeDrag;
        private int undoCountBeforeDrag;
        private bool hasExternalChangesDuringDrag;
        private bool isApplyingDragSnapshots;
        private OngekiFumen dragFumen;

        private void ObserveSelectionChanges(OngekiObjectBase obj, PropertyChangedEventArgs args)
        {
            if (!isApplyingDragSnapshots && args.PropertyName is not (nameof(ISelectableObject.IsSelected) or nameof(ConnectableChildObjectBase.IsAnyControlSelecting)))
                hasExternalChangesDuringDrag = true;
        }

        public bool IsSelectionDragging => activeSelectionDrag != null;
        internal void BeginSelectionDrag(FumenVisualEditorViewModel editor, Point point, IEnumerable<OngekiTimelineObjectBase> objects = null)
        {
            var selected = (objects ?? editor.SelectObjects.OfType<OngekiTimelineObjectBase>()).Distinct().ToArray();
            if (selected.Length == 0 ||
                selected.Length == 1 && selected[0] is not (Tap or Hold or HoldEnd or ConnectableObjectBase or LaneCurvePathControlObject))
                return;
            var related = new HashSet<OngekiTimelineObjectBase>(selected);
            foreach (var o in selected)
            {
                if (o is Hold h && h.HoldEnd is not null)
                    related.Add(h.HoldEnd);
                if (o is HoldEnd e && e.RefHold is not null)
                    related.Add(e.RefHold);
                if (o is Soflan s)
                    related.Add(s.EndIndicator);
                if (o is Soflan.SoflanEndIndicator se)
                    related.Add(se.RefSoflan);
                if (o is LaneBlockArea b)
                    related.Add(b.EndIndicator);
                if (o is LaneBlockArea.LaneBlockAreaEndIndicator be)
                    related.Add(be.RefLaneBlockArea);
                if (o is IndividualSoflanArea area)
                    related.Add(area.EndIndicator);
                if (o is IndividualSoflanArea.IndividualSoflanAreaEndIndicator areaEnd)
                    related.Add(areaEnd.RefIndividualSoflanArea);
                var laneStart = o switch
                {
                    ConnectableObjectBase c => c.ReferenceStartObject,
                    LaneCurvePathControlObject control => control.RefCurveObject?.ReferenceStartObject,
                    _ => null
                };
                if (laneStart != null)
                {
                    related.Add(laneStart);
                    foreach (var child in laneStart.Children)
                    {
                        related.Add(child);
                        foreach (var control in child.PathControls)
                            related.Add(control);
                    }
                }
            }

            var lanes = selected.Select(o => o switch
            {
                ConnectableObjectBase c => c.ReferenceStartObject,
                LaneCurvePathControlObject control => control.RefCurveObject?.ReferenceStartObject,
                _ => null
            }).Where(x => x != null).ToHashSet();
            if (lanes.Count > 0)
                foreach (var dock in editor.Fumen.GetAllDisplayableObjects().OfType<OngekiTimelineObjectBase>().Where(o => o is ILaneDockable d && lanes.Contains(d.ReferenceLaneStart)))
                    related.Add(dock);
            // 附轨通知可能改写未选 Hold 的尾点，也纳入恢复快照。
            foreach (var hold in related.OfType<Hold>().ToArray())
                if (hold.HoldEnd is not null)
                    related.Add(hold.HoldEnd);
            if (dragFumen != null)
                dragFumen.ObjectModifiedChanged -= ObserveSelectionChanges;
            isDirtyBeforeDrag = editor.IsDirty;
            undoStackBeforeDrag = editor.UndoRedoManager.ActionStack.Cast<object>().ToArray();
            undoCountBeforeDrag = editor.UndoRedoManager.UndoActionCount;
            hasExternalChangesDuringDrag = false;
            dragFumen = editor.Fumen;
            dragFumen.ObjectModifiedChanged += ObserveSelectionChanges;
            activeSelectionDrag = new SelectionGridDrag(selected, related);
            var anchor = new Point(XGridCalculator.ConvertXGridToX(XGrid.FromTotalGrid((int)activeSelectionDrag.AnchorX), editor),
                editor.ConvertTGridToY_DesignMode(TGrid.FromTotalGrid((int)activeSelectionDrag.AnchorT)));
            activeSelectionDrag.GrabOffset = new Point(point.X - anchor.X, point.Y - anchor.Y);
        }

        internal void UpdateSelectionDrag(FumenVisualEditorViewModel editor, Point point)
        {
            var drag = activeSelectionDrag;
            if (drag is null)
                return;
            var x = point.X - drag.GrabOffset.X;
            var y = point.Y - drag.GrabOffset.Y;
            var targetT = editor.ConvertYToTGrid_DesignMode(y);
            if (targetT is null)
                return;
            var rawX = XGridCalculator.ConvertXToXGridTotalUnit(x, editor) * XGrid.DEFAULT_RES_X;
            if (double.IsNaN(rawX) || double.IsInfinity(rawX) || rawX < int.MinValue || rawX > int.MaxValue)
                return;
            var targetX = XGridCalculator.ConvertXToXGrid(x, editor);
            var wasDirty = editor.IsDirty;
            editor.ExecuteGridDragUpdate(() =>
            {
                isApplyingDragSnapshots = true;
                try
                {
                    if (!drag.TryMoveInEditor((long)targetT.TotalGrid - drag.AnchorT, (long)targetX.TotalGrid - drag.AnchorX, editor))
                        editor.IsDirty = wasDirty;
                }
                finally
                {
                    isApplyingDragSnapshots = false;
                }
            });
        }

        internal void CommitSelectionDrag(FumenVisualEditorViewModel editor) => FinishSelectionDrag(editor, abort: false);

        internal void AbortSelectionDrag(FumenVisualEditorViewModel editor) => FinishSelectionDrag(editor, abort: true);

        private void FinishSelectionDrag(FumenVisualEditorViewModel editor, bool abort)
        {
            var drag = activeSelectionDrag;
            activeSelectionDrag = null;
            if (drag is null)
                return;
            if (dragFumen != null)
            {
                dragFumen.ObjectModifiedChanged -= ObserveSelectionChanges;
                dragFumen = null;
            }
            var after = drag.CaptureCurrent();
            if (abort)
            {
                editor.ExecuteGridDragUpdate(() => SelectionGridDrag.Apply(drag.Before));
                editor.IsDirty = isDirtyBeforeDrag || hasExternalChangesDuringDrag ||
                    undoStackBeforeDrag != null && (undoCountBeforeDrag != editor.UndoRedoManager.UndoActionCount ||
                    !undoStackBeforeDrag.SequenceEqual(editor.UndoRedoManager.ActionStack.Cast<object>()));
            }
            else if (after.Where((p, i) => p.T != drag.Before[i].T || p.X != drag.Before[i].X || !ReferenceEquals(p.Lane, drag.Before[i].Lane)).Any())
                editor.UndoRedoManager.ExecuteAction(LambdaUndoAction.Create(Resources.DragObjects,
                    () => editor.ExecuteGridDragUpdate(() => SelectionGridDrag.Apply(after)),
                    () => editor.ExecuteGridDragUpdate(() => SelectionGridDrag.Apply(drag.Before))));
        }
    }
}
