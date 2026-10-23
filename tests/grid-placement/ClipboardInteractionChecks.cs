using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Numerics;
using System.Windows;
using Caliburn.Micro;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.EditorObjects.LaneCurve;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Base.OngekiObjects.Projectiles;
using OngekiFumenEditor.Base.OngekiObjects.Lane;
using OngekiFumenEditor.Kernel.Graphics;
using OngekiFumenEditor.Modules.FumenObjectPropertyBrowser;
using OngekiFumenEditor.Modules.FumenVisualEditor;
using OngekiFumenEditor.Modules.FumenVisualEditor.Models;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using OngekiFumenEditor.Modules.FumenVisualEditor.Kernel;
using OngekiFumenEditor.Modules.FumenVisualEditor.Kernel.DefaultImpl;
using static OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.FumenVisualEditorViewModel;

public static class ClipboardInteractionChecks
{
    const BindingFlags F = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    public class BrowserProxy : DispatchProxy
    {
        protected override object Invoke(MethodInfo method, object[] args) => null;
    }

    public static void Run(Action<bool, string> check)
    {
        typeof(OngekiFumenEditor.Utils.Log).GetField("cacheInstance", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, new OngekiFumenEditor.Utils.Log(Array.Empty<OngekiFumenEditor.Utils.Logs.ILogOutput>()));
        var old = IoC.GetInstance;
        var browser = DispatchProxy.Create<IFumenObjectPropertyBrowser, BrowserProxy>();
        IoC.GetInstance = (t, k) => t == typeof(IFumenObjectPropertyBrowser) ? browser : old(t, k);
        try
        {
            var type = typeof(FumenVisualEditorViewModel);
            var editor = (FumenVisualEditorViewModel)RuntimeHelpers.GetUninitializedObject(type);
        type.GetProperty("InteractiveManager").SetValue(editor, new OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.Interactives.ObjectInteractiveManager());
            var screen = new Screen();
            for (var parent = typeof(Screen); parent != null; parent = parent.BaseType)
                foreach (var field in parent.GetFields(F | BindingFlags.DeclaredOnly))
                    field.SetValue(editor, field.GetValue(screen));
            var project = new EditorProjectDataModel
            {
                AudioDuration = TimeSpan.FromSeconds(30)
            };
            type.GetField("editorProjectData", F).SetValue(editor, project);
            var setting = new EditorSetting
            {
                AdjustPastedObjects = true,
                BeatSplit = 1,
                ForceMagneticDock = true,
                DisableXGridMagneticDock = false,
                DisableTGridMagneticDock = false
            };
            type.GetField("<Setting>k__BackingField", F).SetValue(editor, setting);
            type.GetField("renderDataLock", F).SetValue(editor, new System.Threading.ReaderWriterLockSlim(System.Threading.LockRecursionPolicy.SupportsRecursion));
            type.GetProperty("ViewWidth", F).SetValue(editor, 1200f);
            type.GetProperty("RectInDesignMode", F).SetValue(editor, new VisibleRect(new Vector2(1200, 0), new Vector2(0, 1000)));
            type.GetField("SelectionArea", F).SetValue(editor, new SelectionArea(editor)
            {
                IsActive = false
            });
            editor.TotalDurationHeight = 100000;
            var undo = new DefaultEditorUndoManager(editor);
            typeof(DefaultEditorUndoManager).GetField("isRecoveryCurrentTime", F)?.SetValue(undo, false);
            type.GetProperty("UndoRedoManager").SetValue(editor, undo);
            project.Fumen.BpmList.FirstBpm = 120;
            project.Fumen.BpmList.Add(new BPMChange
            {
                TGrid = TGrid.FromTotalGrid(1920),
                BPM = 240
            });
            project.Fumen.MeterChanges.Add(new MeterChange
            {
                TGrid = TGrid.FromTotalGrid(1920),
                BunShi = 3,
                Bunbo = 8
            });
            var clipboardType = type.Assembly.GetType("OngekiFumenEditor.Modules.FumenVisualEditor.Kernel.DefaultImpl.DefaultFumenEditorClipboard");
            void Paste(OngekiObjectBase[] input, int targetT, int targetX, PasteOption option = PasteOption.None)
            {
                var clipboard = Activator.CreateInstance(clipboardType, true);
                ((List<OngekiObjectBase>)clipboardType.GetField("currentCopiedSources", F).GetValue(clipboard)).AddRange(GridClipboardGeometry.CloneSet(input));
                var point = new Point(XGridCalculator.ConvertXGridToX(XGrid.FromTotalGrid(targetX), editor), editor.ConvertTGridToY_DesignMode(TGrid.FromTotalGrid(targetT)));
                ((System.Threading.Tasks.Task)clipboardType.GetMethod("PasteObjects").Invoke(clipboard, new object[] { editor, option, point })).GetAwaiter().GetResult();
            }

            Bell Tap(int t, int x) => new Bell
            {
                TGrid = TGrid.FromTotalGrid(t),
                XGrid = XGrid.FromTotalGrid(x)
            };
            var previouslySelected = new Bell
            {
                TGrid = TGrid.FromTotalGrid(123),
                XGrid = XGrid.Zero,
                IsSelected = true
            };
            project.Fumen.AddObject(previouslySelected);
            var input = new OngekiObjectBase[]
            {
                Tap(100, -1000),
                Tap(473, 1000),
                Tap(1033, 1900)
            };
            Paste(input, 1810, 700);
            var pasted = editor.SelectObjects.OfType<Bell>().OrderBy(x => x.TGrid.TotalGrid).ToArray();
            check(pasted.Length == 3, "真实PasteObjects创建三个独立物件");
            var raw = GridClipboardGeometry.CloneSet(input);
            GridClipboardGeometry.Place(raw, XGrid.FromTotalGrid(700), TGrid.FromTotalGrid(1810), OngekiFumenEditor.Modules.FumenVisualEditor.Base.GridPlacementOption.None);
            var action = new OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.Interactives.Impls.DefaultObjectInteractiveAction();
            var expected = raw.Cast<Bell>().Select(x => (t: editor.ConvertYToTGrid_DesignMode(action.CheckAndAdjustY(x, editor.ConvertTGridToY_DesignMode(x.TGrid), editor).Value).TotalGrid,
                x: XGridCalculator.ConvertXToXGrid(action.CheckAndAdjustX(x, XGridCalculator.ConvertXGridToX(x.XGrid, editor), editor).Value, editor).TotalGrid)).OrderBy(x => x.t).ToArray();
            check(pasted.Select(x => x.TGrid.TotalGrid).SequenceEqual(expected.Select(x => x.t)), "跨BPM拍号逐物件时间吸附，参考点不统一预吸附");
            check(pasted.Select(x => x.XGrid.TotalGrid).SequenceEqual(expected.Select(x => x.x)), "每个物件横向分别吸附");
            check(pasted[1].TGrid.TotalGrid - pasted[0].TGrid.TotalGrid != 373, "独立吸附允许原始间距微调");
            var times = pasted.Select(x => x.TGrid.TotalGrid).ToArray();
            var xs = pasted.Select(x => x.XGrid.TotalGrid).ToArray();
            undo.Undo(1);
            check(!pasted.Any(x => project.Fumen.Bells.Contains(x)), "真实Undo移除粘贴物件");
            check(previouslySelected.IsSelected, "真实Paste Undo恢复粘贴前仍在谱面的原选中集合");
            setting.DisableTGridMagneticDock = setting.DisableXGridMagneticDock = true;
            undo.Redo(1);
            check(pasted.Select(x => x.TGrid.TotalGrid).SequenceEqual(times) && pasted.Select(x => x.XGrid.TotalGrid).SequenceEqual(xs), "设置改变后Redo仍回放首次吸附坐标");
            check(!previouslySelected.IsSelected && pasted.All(x => x.IsSelected), "真实Paste Redo仅选中复制集合");
            Paste(input, 1811, 703);
            var unsnapped = editor.SelectObjects.OfType<Bell>().OrderBy(x => x.TGrid.TotalGrid).ToArray();
            check(unsnapped[0].TGrid.TotalGrid == 1811 && unsnapped[1].TGrid.TotalGrid - unsnapped[0].TGrid.TotalGrid == 373 && unsnapped[2].XGrid.TotalGrid - unsnapped[0].XGrid.TotalGrid == 2900, "关闭X/T吸附精确保留Grid偏移");
            setting.DisableTGridMagneticDock = false;
            Paste(input, 1810, 703);
            var timeOnly = editor.SelectObjects.OfType<Bell>().OrderBy(x => x.TGrid.TotalGrid).ToArray();
            check(timeOnly[2].XGrid.TotalGrid - timeOnly[0].XGrid.TotalGrid == 2900 && timeOnly.Select(x => x.TGrid.TotalGrid).SequenceEqual(expected.Select(x => x.t)), "只开时间吸附仍保留横向Grid差");
            setting.DisableTGridMagneticDock = true;
            setting.DisableXGridMagneticDock = false;
            Paste(input, 1811, 700);
            var xOnly = editor.SelectObjects.OfType<Bell>().OrderBy(x => x.TGrid.TotalGrid).ToArray();
            check(xOnly[0].TGrid.TotalGrid == 1811 && xOnly[1].TGrid.TotalGrid - xOnly[0].TGrid.TotalGrid == 373 && xOnly.All(x => x.XGrid.TotalGrid % XGrid.DEFAULT_RES_X == 0), "只开横向吸附仍保留时间Grid差");
            var lane = new LaneLeftStart
            {
                TGrid = TGrid.Zero,
                XGrid = XGrid.FromTotalGrid(-3000)
            };
            lane.AddChildObject(new LaneLeftNext
            {
                TGrid = TGrid.FromTotalGrid(10000),
                XGrid = XGrid.FromTotalGrid(7000)
            });
            project.Fumen.AddObject(lane);
            var hold = new Hold
            {
                TGrid = TGrid.FromTotalGrid(100),
                ReferenceLaneStart = lane,
                XGrid = XGrid.FromTotalGrid(-2900)
            };
            hold.SetHoldEnd(new HoldEnd
            {
                TGrid = TGrid.FromTotalGrid(473),
                XGrid = XGrid.FromTotalGrid(-2527)
            });
            setting.DisableTGridMagneticDock = false;
            Paste(new OngekiObjectBase[] { hold }, 2201, 6000);
            var copiedHold = editor.SelectObjects.OfType<Hold>().Single();
            check(ReferenceEquals(copiedHold.ReferenceLaneStart, lane) && copiedHold.XGrid == lane.CalulateXGrid(copiedHold.TGrid) && copiedHold.HoldEnd.XGrid == lane.CalulateXGrid(copiedHold.EndTGrid), "Hold头尾按各自吸附时间贴所属原轨，超过8px也保持引用");
            check(copiedHold.EndTGrid >= copiedHold.TGrid, "独立时间吸附不倒置Hold头尾");
            var curve = new LaneLeftStart
            {
                TGrid = TGrid.FromTotalGrid(100),
                XGrid = XGrid.FromTotalGrid(-1000)
            };
            var child = new LaneLeftNext
            {
                TGrid = TGrid.FromTotalGrid(1033),
                XGrid = XGrid.FromTotalGrid(1900)
            };
            curve.AddChildObject(child);
            child.AddControlObject(new LaneCurvePathControlObject
            {
                TGrid = TGrid.FromTotalGrid(473),
                XGrid = XGrid.FromTotalGrid(800)
            });
            Paste(new OngekiObjectBase[] { curve }, 2201, 700, PasteOption.SelectedRangeCenterTGridMirror);
            var mirrored = editor.SelectObjects.OfType<LaneLeftStart>().Single();
            check(mirrored.Children.Count() == 1 && mirrored.Children.Single().PathControls.Count() == 1 && mirrored.TGrid <= mirrored.Children.Single().TGrid, "镜像后逐节点吸附保留轨道父子和控制点");
            check(mirrored.Children.Single().PathControls.Single().RefCurveObject == mirrored.Children.Single(), "镜像控制点引用保持有效");
            var boundTap = new Tap
            {
                TGrid = TGrid.FromTotalGrid(100),
                ReferenceLaneStart = lane,
                XGrid = XGrid.FromTotalGrid(-2900)
            };
            Paste(new OngekiObjectBase[] { boundTap }, 2201, 6000);
            var pastedTap = editor.SelectObjects.OfType<Tap>().Single();
            check(ReferenceEquals(pastedTap.ReferenceLaneStart, lane) && pastedTap.XGrid == lane.CalulateXGrid(pastedTap.TGrid), "真实Tap粘贴按最终时间贴所属轨");
            var objectCount = project.Fumen.GetAllDisplayableObjects().Count();
            var undoCount = undo.ActionStack.Count;
            Paste(new OngekiObjectBase[] { boundTap, Tap(473, 1000) }, 20000, 700);
            check(project.Fumen.GetAllDisplayableObjects().Count() == objectCount, "任一附轨Tap越界时整组粘贴拒绝，不留下普通物件");
            var noLane = new Tap
            {
                TGrid = TGrid.FromTotalGrid(100),
                XGrid = XGrid.FromTotalGrid(200000)
            };
            Paste(new OngekiObjectBase[] { noLane, Tap(473, 1000) }, 2201, 200000);
            check(project.Fumen.GetAllDisplayableObjects().Count() == objectCount, "新粘贴无轨Tap无法首次附轨时整个事务拒绝");
            check(undo.ActionStack.Count == undoCount, "拒绝的粘贴不产生Undo记录");
        }
        finally
        {
            IoC.GetInstance = old;
        }
    }
}
