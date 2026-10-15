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
using System.Windows;
using static OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.FumenVisualEditorViewModel;
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
            if (sourceEditor.Fumen is null) return Task.CompletedTask;
            if (!sourceEditor.IsDesignMode) { sourceEditor.ToastNotify(Resources.EditorMustBeDesignMode); return Task.CompletedTask; }
            currentCopiedSources.Clear();
            var sources = new List<OngekiObjectBase>();
            foreach (var obj in objects.OfType<OngekiObjectBase>().Distinct())
            {
                if (obj is ConnectableObjectBase and not ConnectableStartObject ||
                    obj is LaneCurvePathControlObject || obj is HoldEnd ||
                    obj is LaneBlockArea.LaneBlockAreaEndIndicator || obj is Soflan.SoflanEndIndicator ||
                    obj is IndividualSoflanArea.IndividualSoflanAreaEndIndicator) continue;
                if (obj is ConnectableStartObject start && !start.Children.Append<ConnectableObjectBase>(start).All(x => x.IsSelected)) continue;
                sources.Add(obj);
            }
            currentCopiedSources.AddRange(GridClipboardGeometry.CloneSet(sources));
            sourceEditor.ToastNotify(currentCopiedSources.Count == 0 ? Resources.ClearCopyList :
                currentCopiedSources.Count == 1 ? Resources.CopiedObjectsBrushAllowed.Format(1) : Resources.CopiedObjects.Format(currentCopiedSources.Count));
            return Task.CompletedTask;
        }

        public Task PasteObjects(FumenVisualEditorViewModel targetEditor, PasteOption pasteOption, Point? placePoint = null)
        {
            if (targetEditor.Fumen is null || !ContainPastableObjects) return Task.CompletedTask;
            if (!targetEditor.IsDesignMode) { targetEditor.ToastNotify(Resources.EditorMustBeDesignMode); return Task.CompletedTask; }
            var copies = GridClipboardGeometry.CloneSet(currentCopiedSources);
            if (copies.Length == 0) return Task.CompletedTask;
            var anchor = GridClipboardGeometry.ReferencePoint(copies);
            var targetX = anchor.x; var targetT = anchor.t;
            if (pasteOption != PasteOption.Direct && placePoint is Point target)
            {
                // 只吸附一个参考点，其余物件直接应用完全相同的 Grid 偏移。
                if (targetEditor.Setting.AdjustPastedObjects)
                {
                    var interaction = new DefaultObjectInteractiveAction();
                    target.X = interaction.CheckAndAdjustX(null, target.X, targetEditor) ?? target.X;
                    target.Y = interaction.CheckAndAdjustY(null, target.Y, targetEditor) ?? target.Y;
                }
                var rawX = XGridCalculator.ConvertXToXGridTotalUnit(target.X, targetEditor) * XGrid.DEFAULT_RES_X;
                if (double.IsNaN(rawX) || double.IsInfinity(rawX) || rawX < int.MinValue || rawX > int.MaxValue)
                    return Task.CompletedTask;
                targetX = XGridCalculator.ConvertXToXGrid(target.X, targetEditor);
                targetT = targetEditor.ConvertYToTGrid_DesignMode(target.Y);
                if (targetX is null || targetT is null) return Task.CompletedTask;
            }
            if (!GridClipboardGeometry.Place(copies, targetX, targetT, pasteOption))
            {
                targetEditor.ToastNotify(OngekiFumenEditor.Properties.Resources.PasteInvalidGridPosition);
                return Task.CompletedTask;
            }
            var redo = new System.Action(() => targetEditor.TryCancelAllObjectSelecting());
            var undo = new System.Action(() => targetEditor.TryCancelAllObjectSelecting());
            foreach (var copied in copies.OrderBy(x => x is ConnectableStartObject ? 0 : 1))
            {
                if (copied is Flick flick && (pasteOption == PasteOption.XGridZeroMirror || pasteOption == PasteOption.SelectedRangeCenterXGridMirror))
                    flick.Direction = (Flick.FlickDirection)(-(int)flick.Direction);
                if (copied is IBulletPalleteReferencable bullet && bullet.ReferenceBulletPallete is BulletPallete pallete)
                {
                    //如果IsAppend为false,那就直接改引用直接成这个。否则就新建一个
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

                //粘贴特殊处理Tap/Hold(特别是跨编辑器粘贴时)
                var coordinates = GridClipboardGeometry.Enumerate(copied).Select(x => (obj: x,
                    t: (x as ITimelineObject)?.TGrid?.CopyNew(),
                    x: (x as IHorizonPositionObject)?.XGrid?.CopyNew())).ToArray();
                void RestoreCoordinates()
                {
                    foreach (var point in coordinates)
                    {
                        if (point.t != null) ((ITimelineObject)point.obj).TGrid = point.t.CopyNew();
                        if (point.x != null) ((IHorizonPositionObject)point.obj).XGrid = point.x.CopyNew();
                    }
                }
                var dockable = copied as ILaneDockable;
                var preferredLane = dockable?.ReferenceLaneStart;
                var rootTime = (copied as ITimelineObject)?.TGrid?.CopyNew();
                var rootX = (copied as IHorizonPositionObject)?.XGrid?.CopyNew();
                redo += () =>
                {
                    if (copied is ConnectableStartObject start) start.RecordId = -1;
                    if (dockable != null && rootTime != null && rootX != null)
                    {
                        var lanes = targetEditor.Fumen.Lanes.GetVisibleStartObjects(rootTime, rootTime)
                            .Where(x => x.IsDockableLane)
                            .Select(x => (lane: x, distance: x.CalulateXGrid(rootTime) is XGrid lx ?
                                Math.Abs(XGridCalculator.ConvertXGridToX(lx, targetEditor) - XGridCalculator.ConvertXGridToX(rootX, targetEditor)) : double.MaxValue))
                            .Where(x => x.distance <= 8).OrderBy(x => x.distance).ToArray();
                        dockable.ReferenceLaneStart = (preferredLane != null && copies.Contains(preferredLane) ? preferredLane : null) ??
                            lanes.FirstOrDefault(x => x.lane == preferredLane).lane ??
                            lanes.FirstOrDefault(x => x.lane.LaneType == preferredLane?.LaneType).lane ?? lanes.FirstOrDefault().lane;
                    }
                    targetEditor.Fumen.AddObject(copied);
                    // Hold 的附轨动作会修改尾点，因此先建立关联，再恢复已计算的全部坐标。
                    RestoreCoordinates();
                    foreach (var node in GridClipboardGeometry.Enumerate(copied).OfType<ISelectableObject>()) node.IsSelected = true;
                };
                undo += () =>
                {
                    targetEditor.RemoveObject(copied);
                    if (dockable != null) dockable.ReferenceLaneStart = null;
                };
            }
            redo += () => IoC.Get<IFumenObjectPropertyBrowser>().RefreshSelected(targetEditor);
            undo += () => IoC.Get<IFumenObjectPropertyBrowser>().RefreshSelected(targetEditor);
            targetEditor.UndoRedoManager.ExecuteAction(LambdaUndoAction.Create(Resources.CopyAndPaste, redo, undo));
            return Task.CompletedTask;
        }
    }
}
