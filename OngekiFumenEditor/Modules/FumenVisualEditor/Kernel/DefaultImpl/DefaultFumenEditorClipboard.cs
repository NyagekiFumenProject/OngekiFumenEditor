using Caliburn.Micro;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.EditorObjects.LaneCurve;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Base.OngekiObjects.Lane.Base;
using OngekiFumenEditor.Modules.FumenObjectPropertyBrowser;
using OngekiFumenEditor.Modules.FumenVisualEditor.Base;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.Interactives.Impls;
using OngekiFumenEditor.Properties;
using OngekiFumenEditor.Utils;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading.Tasks;
using SystemAction = System.Action;
using System.Windows;
namespace OngekiFumenEditor.Modules.FumenVisualEditor.Kernel.DefaultImpl
{
    [Export(typeof(IFumenEditorClipboard))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    internal class DefaultFumenEditorClipboard : IFumenEditorClipboard
    {
        private readonly List<OngekiObjectBase> currentCopiedSources = new();
        public bool ContainPastableObjects => currentCopiedSources.Count > 0;
        public IReadOnlyCollection<OngekiObjectBase> CurrentCopiedObjects => currentCopiedSources;

        public Task CopyObjects(FumenVisualEditorViewModel sourceEditor, IEnumerable<ISelectableObject> objects)
        {
            if (sourceEditor.Fumen is null)
                return Task.CompletedTask;
            if (!sourceEditor.IsDesignMode)
            {
                sourceEditor.ToastNotify(Resources.EditorMustBeDesignMode);
                return Task.CompletedTask;
            }

            var sources = new List<OngekiObjectBase>();
            foreach (var obj in objects.OfType<OngekiObjectBase>().Distinct())
            {
                if (obj is ConnectableObjectBase and not ConnectableStartObject ||
                    obj is LaneCurvePathControlObject || obj is HoldEnd ||
                    obj is LaneBlockArea.LaneBlockAreaEndIndicator || obj is Soflan.SoflanEndIndicator ||
                    obj is IndividualSoflanArea.IndividualSoflanAreaEndIndicator)
                    continue;
                if (obj is ConnectableStartObject start && !start.Children.Append<ConnectableObjectBase>(start).All(x => x.IsSelected))
                    continue;
                sources.Add(obj);
            }

            var copies = GridClipboardGeometry.CloneSet(sources);
            currentCopiedSources.Clear();
            currentCopiedSources.AddRange(copies);
            sourceEditor.ToastNotify(currentCopiedSources.Count == 0 ? Resources.ClearCopyList :
                currentCopiedSources.Count == 1 ? Resources.CopiedObjectsBrushAllowed.Format(1) : Resources.CopiedObjects.Format(currentCopiedSources.Count));
            return Task.CompletedTask;
        }

        public Task PasteObjects(FumenVisualEditorViewModel targetEditor, PasteOption pasteOption, Point? placePoint = null)
        {
            if (targetEditor.Fumen is null || !ContainPastableObjects)
                return Task.CompletedTask;
            if (!targetEditor.IsDesignMode)
            {
                targetEditor.ToastNotify(Resources.EditorMustBeDesignMode);
                return Task.CompletedTask;
            }

            var copies = GridClipboardGeometry.CloneSet(currentCopiedSources);
            if (copies.Length == 0)
                return Task.CompletedTask;
            var anchor = GridClipboardGeometry.ReferencePoint(copies);
            var targetX = anchor.x;
            var targetT = anchor.t;
            if (pasteOption != PasteOption.Direct && placePoint is Point target)
            {
                // 参考点只决定 Grid 平移；每个新物件在平移后独立吸附。
                var targetUnit = XGridCalculator.ConvertXToXGridTotalUnit(target.X, targetEditor);
                var targetTotalGrid = targetUnit * XGrid.DEFAULT_RES_X;
                if (!double.IsFinite(targetTotalGrid) || targetTotalGrid < int.MinValue || targetTotalGrid > int.MaxValue)
                    return Task.CompletedTask;
                targetX = new XGrid((float)targetUnit);
                targetT = targetEditor.ConvertYToTGrid_DesignMode(target.Y);
                if (targetT is null)
                    return Task.CompletedTask;
            }

            if (!GridClipboardGeometry.Place(copies, targetX, targetT, pasteOption) ||
                (targetEditor.Setting.AdjustPastedObjects && !targetEditor.InteractiveManager.AdjustPastedObjects(copies, targetEditor)))
            {
                targetEditor.ToastNotify(Resources.PasteInvalidGridPosition);
                return Task.CompletedTask;
            }

            // 附轨与坐标先在未加入谱面的克隆上完整预检，失败时不产生任何局部粘贴。
            Task RejectMissingLane()
            {
                targetEditor.ToastNotify(Resources.CantPlaceObjectsByNoSuiteLanes.Format("Tap / Hold"));
                return Task.CompletedTask;
            }

            var availableLanes = targetEditor.Fumen.Lanes.Concat(copies.OfType<LaneStartBase>()).Where(x => x.IsDockableLane).ToArray();
            var projectors = new Dictionary<LaneStartBase, Func<TGrid, XGrid, XGrid>>();
            XGrid Project(LaneStartBase lane, TGrid time, XGrid x)
            {
                if (!projectors.TryGetValue(lane, out var project))
                    projectors[lane] = project = LaneGeometry.CreateProjector(lane);
                return project(time, x);
            }

            foreach (var copied in copies)
                if (copied is Tap or Hold && copied is ILaneDockable attached)
                {
                    var lane = attached.ReferenceLaneStart;
                    if (lane == null || !availableLanes.Contains(lane))
                        lane = availableLanes.Select(x => (lane: x, position: Project(x, attached.TGrid, attached.XGrid)))
                            .Where(x => x.position != null)
                            .Select(x => (x.lane, distance: Math.Abs(XGridCalculator.ConvertXGridToX(x.position, targetEditor) - XGridCalculator.ConvertXGridToX(attached.XGrid, targetEditor))))
                            .Where(x => x.distance <= LaneGeometry.LaneSnapDistancePixels).OrderBy(x => x.distance).Select(x => x.lane).FirstOrDefault();
                    if (lane == null)
                        return RejectMissingLane();
                    var coordinates = GridClipboardGeometry.Enumerate(copied).Select(node => (node,
                        t: (node as ITimelineObject)?.TGrid?.CopyNew(), x: (node as IHorizonPositionObject)?.XGrid?.CopyNew())).ToArray();
                    attached.ReferenceLaneStart = lane;
                    foreach (var point in coordinates)
                    {
                        if (point.t != null)
                            ((ITimelineObject)point.node).TGrid = point.t;
                        if (point.x != null)
                            ((IHorizonPositionObject)point.node).XGrid = point.x;
                    }

                    foreach (var node in GridClipboardGeometry.Enumerate(copied).OfType<ILaneDockable>())
                    {
                        var laneX = Project(lane, node.TGrid, node.XGrid);
                        if (laneX == null)
                            return RejectMissingLane();
                        node.XGrid = laneX;
                    }
                }

            var selectedBeforePaste = targetEditor.SelectObjects.ToArray();
            void ClearSelection()
            {
                foreach (var selected in targetEditor.SelectObjects.ToArray())
                    selected.IsSelected = false;
            }

            var redo = new SystemAction(ClearSelection);
            var undo = new SystemAction(ClearSelection);
            foreach (var copied in copies.OrderBy(x => x is ConnectableStartObject ? 0 : 1))
            {
                if (copied is Flick flick && (pasteOption == PasteOption.XGridZeroMirror || pasteOption == PasteOption.SelectedRangeCenterXGridMirror))
                    flick.Direction = (Flick.FlickDirection)(-(int)flick.Direction);
                if (copied is IBulletPalleteReferencable bullet && bullet.ReferenceBulletPallete is BulletPallete pallete)
                {
                    // 同 ID 且等价则复用，否则追加副本。
                    var isAppend = false;
                    BulletPallete existPallete = default;
                    if (targetEditor.Fumen.BulletPalleteList.FirstOrDefault(x => x.StrID == pallete.StrID) is BulletPallete e)
                    {
                        existPallete = e;
                        isAppend = !GridClipboardGeometry.PalettesEquivalent(pallete, existPallete);
                    }
                    else
                        isAppend = true;

                    BulletPallete pickPallete = default;
                    if (isAppend)
                    {
                        var newPallete = pallete.CopyNew() as BulletPallete;
                        newPallete.EditorName = $"{(string.IsNullOrWhiteSpace(newPallete.EditorName) ? newPallete.StrID : newPallete.EditorName)} - {Resources.Copy}";
                        pickPallete = newPallete;
                    }
                    else
                    {
                        pickPallete = existPallete;
                    }

                    redo += () =>
                    {
                        if (isAppend)
                            targetEditor.Fumen.AddObject(pickPallete);
                        bullet.ReferenceBulletPallete = pickPallete;
                    };
                    undo += () =>
                    {
                        if (isAppend)
                            targetEditor.Fumen.RemoveObject(pickPallete);
                        bullet.ReferenceBulletPallete = default;
                    };
                }

                // 附轨 setter 可能改写尾点，先保存再恢复完整坐标。
                var coordinates = GridClipboardGeometry.Enumerate(copied).Select(x => (obj: x,
                    t: (x as ITimelineObject)?.TGrid?.CopyNew(),
                    x: (x as IHorizonPositionObject)?.XGrid?.CopyNew())).ToArray();
                void RestoreCoordinates()
                {
                    foreach (var point in coordinates)
                    {
                        if (point.t != null)
                            ((ITimelineObject)point.obj).TGrid = point.t.CopyNew();
                        if (point.x != null)
                            ((IHorizonPositionObject)point.obj).XGrid = point.x.CopyNew();
                    }
                }

                var dockable = copied as ILaneDockable;
                var preferredLane = dockable?.ReferenceLaneStart;
                var resolvedLane = preferredLane;
                redo += () =>
                {
                    if (copied is ConnectableStartObject start)
                        start.RecordId = -1;
                    if (dockable != null)
                        dockable.ReferenceLaneStart = resolvedLane;
                    targetEditor.Fumen.AddObject(copied);
                    RestoreCoordinates();
                    foreach (var node in GridClipboardGeometry.Enumerate(copied).OfType<ISelectableObject>())
                        node.IsSelected = true;
                };
                undo += () =>
                {
                    targetEditor.RemoveObject(copied);
                    if (dockable != null)
                        dockable.ReferenceLaneStart = null;
                };
            }

            undo += () =>
            {
                var remaining = targetEditor.Fumen.GetAllDisplayableObjects().OfType<ISelectableObject>().ToHashSet();
                foreach (var selected in selectedBeforePaste)
                    if (remaining.Contains(selected))
                        selected.IsSelected = true;
            };
            redo += () => IoC.Get<IFumenObjectPropertyBrowser>().RefreshSelected(targetEditor);
            undo += () => IoC.Get<IFumenObjectPropertyBrowser>().RefreshSelected(targetEditor);
            targetEditor.UndoRedoManager.ExecuteAction(LambdaUndoAction.Create(Resources.CopyAndPaste, redo, undo));
            return Task.CompletedTask;
        }
    }
}
