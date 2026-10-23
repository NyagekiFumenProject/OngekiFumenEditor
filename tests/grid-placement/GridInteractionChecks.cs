using System;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Diagnostics;
using System.Numerics;
using System.Windows;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Base.OngekiObjects.Lane;
using OngekiFumenEditor.Kernel.Graphics;
using OngekiFumenEditor.Modules.FumenVisualEditor;
using OngekiFumenEditor.Modules.FumenVisualEditor.Models;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;

public static class GridInteractionChecks
{
    const BindingFlags F = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    public static void Run(Action<bool, string> check)
    {
        var vmType = typeof(FumenVisualEditorViewModel);
        var dragType = vmType.Assembly.GetType("OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.Interactives.SelectionGridDrag");
        var holds = Enumerable.Range(0, 200).Select(i =>
        {
            var hold = new Hold
            {
                TGrid = TGrid.FromTotalGrid(100)
            };
            hold.SetHoldEnd(new HoldEnd
            {
                TGrid = TGrid.FromTotalGrid(500),
                XGrid = XGrid.FromTotalGrid(i)
            });
            return hold;
        }).ToArray();
        var tails = holds.Select(h => (OngekiTimelineObjectBase)h.HoldEnd).ToArray();
        var related = holds.Cast<OngekiTimelineObjectBase>().Concat(tails).ToArray();
        var drag = Activator.CreateInstance(dragType, F, null, new object[] { tails, related }, null);
        var move = dragType.GetMethod("TryMove", F);
        var notifications = 0;
        foreach (var node in related)
            node.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == "TGrid" || e.PropertyName == "XGrid")
                    notifications++;
            };
        var timer = Stopwatch.StartNew();
        for (var i = 0; i < 100; i++)
            move.Invoke(drag, new object[] { 0L, 0L });
        timer.Stop();
        Console.WriteLine($"200个Hold尾/100次不变移动：坐标通知={notifications}，耗时={timer.Elapsed.TotalMilliseconds:F2}ms");
        check(notifications == 0, "不变移动不应触发任何坐标通知");

        var editor = (FumenVisualEditorViewModel)RuntimeHelpers.GetUninitializedObject(vmType);
        vmType.GetProperty("InteractiveManager").SetValue(editor, new OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.Interactives.ObjectInteractiveManager());
        editor.IsNotifying = true;
        var project = new EditorProjectDataModel
        {
            AudioDuration = TimeSpan.FromSeconds(10)
        };
        vmType.GetField("editorProjectData", F).SetValue(editor, project);
        var setting = new EditorSetting();
        vmType.GetField("<Setting>k__BackingField", F).SetValue(editor, setting);
        vmType.GetProperty("ViewWidth", F).SetValue(editor, 1200f);
        vmType.GetProperty("RectInDesignMode", F).SetValue(editor, new VisibleRect(new Vector2(1200, 0), new Vector2(0, 1000)));
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
        setting.BeatSplit = 1;
        setting.DisableTGridMagneticDock = false;
        setting.ForceMagneticDock = true;
        setting.DisableXGridMagneticDock = true;
        setting.ForceXGridMagneticDock = false;
        setting.ForceTapHoldMagneticDockToLane = true;
        Hold MakeHold(int offset)
        {
            var lane = new LaneLeftStart
            {
                TGrid = TGrid.Zero,
                XGrid = XGrid.FromTotalGrid(offset)
            };
            lane.AddChildObject(new LaneLeftNext
            {
                TGrid = TGrid.FromTotalGrid(10000),
                XGrid = XGrid.FromTotalGrid(offset + 10000)
            });
            var h = new Hold
            {
                TGrid = TGrid.FromTotalGrid(100),
                ReferenceLaneStart = lane
            };
            h.SetHoldEnd(new HoldEnd
            {
                TGrid = TGrid.FromTotalGrid(960)
            });
            return h;
        }

        var a = MakeHold(1000);
        var b = MakeHold(-3000);
        var tap = new Tap
        {
            TGrid = TGrid.FromTotalGrid(1300),
            XGrid = XGrid.FromTotalGrid(1234)
        };
        var selected = new OngekiTimelineObjectBase[]
        {
            a.HoldEnd,
            b.HoldEnd,
            tap
        };
        var all = new OngekiTimelineObjectBase[]
        {
            a,
            a.HoldEnd,
            b,
            b.HoldEnd,
            tap
        };
        drag = Activator.CreateInstance(dragType, F, null, new object[] { selected, all }, null);
        editor.InteractiveManager.GetType().GetField("activeSelectionDrag", F).SetValue(editor.InteractiveManager, drag);
        for (var i = 0; i < 10000; i++)
            project.Fumen.AddObject(new Tap
            {
                TGrid = TGrid.FromTotalGrid(10000 + i),
                XGrid = XGrid.Zero
            });
        var metrics = 0;
        editor.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == "ReverseVerticalScrollValue")
                metrics++;
        };
        var modified = vmType.GetMethod("OnFumenObjectModifiedChanged", F);
        foreach (var obj in all)
            obj.PropertyChanged += (_, e) => modified.Invoke(editor, new object[] { obj, e });
        var point = new Point(XGridCalculator.ConvertXGridToX(XGrid.FromTotalGrid((int)(long)dragType.GetField("AnchorX", F).GetValue(drag)), editor), editor.ConvertTGridToY_DesignMode(TGrid.FromTotalGrid(2640)) + 1);
        vmType.GetMethod("UpdateSelectionDrag", F).Invoke(editor, new object[] { point });
        check(a.HoldEnd.TGrid.TotalGrid == 2640 && b.HoldEnd.TGrid.TotalGrid == 2640 && tap.TGrid.TotalGrid == 2880, "真实Move跨BPM逐物件时间吸附");
        check(a.HoldEnd.XGrid == a.ReferenceLaneStart.CalulateXGrid(a.HoldEnd.TGrid) && b.HoldEnd.XGrid == b.ReferenceLaneStart.CalulateXGrid(b.HoldEnd.TGrid), "多个Hold尾各自贴原轨");
        check(a.TGrid.TotalGrid == 100 && b.TGrid.TotalGrid == 100, "未选Hold头不移动");
        Console.WriteLine($"两Hold尾+Tap/10000物件：单次真实Move滚动全谱扫描={metrics}");
        check(metrics == 1, "一次鼠标更新仅刷新一次滚动metrics");
        var restoreDrag = drag;
        var perfHolds = Enumerable.Range(0, 200).Select(i => MakeHold(i * 10)).ToArray();
        var perfTails = perfHolds.Select(h => (OngekiTimelineObjectBase)h.HoldEnd).ToArray();
        var perfAll = perfHolds.Cast<OngekiTimelineObjectBase>().Concat(perfTails).ToArray();
        foreach (var node in perfAll)
            node.PropertyChanged += (_, e) => modified.Invoke(editor, new object[] { node, e });
        drag = Activator.CreateInstance(dragType, F, null, new object[] { perfTails, perfAll }, null);
        editor.InteractiveManager.GetType().GetField("activeSelectionDrag", F).SetValue(editor.InteractiveManager, drag);
        metrics = 0;
        timer.Restart();
        for (var i = 1; i <= 20; i++)
            vmType.GetMethod("UpdateSelectionDrag", F).Invoke(editor, new object[] { new Point(600, editor.ConvertTGridToY_DesignMode(TGrid.FromTotalGrid(960 + i * 120))) });
        timer.Stop();
        Console.WriteLine($"200个Hold尾/10000物件/20次变动：滚动全谱扫描={metrics}，耗时={timer.Elapsed.TotalMilliseconds:F2}ms");
        check(metrics <= 20, "实际移动时每事件至多一次全谱扫描");
        drag = restoreDrag;
        editor.InteractiveManager.GetType().GetField("activeSelectionDrag", F).SetValue(editor.InteractiveManager, drag);
        var snapshot = dragType.GetMethod("CaptureCurrent", F).Invoke(drag, null);
        var before = dragType.GetField("Before", F).GetValue(drag);
        var apply = dragType.GetMethod("Apply", BindingFlags.Static | BindingFlags.NonPublic);
        apply.Invoke(null, new object[] { before, 0L, 0L });
        check(a.HoldEnd.TGrid.TotalGrid == 960 && tap.TGrid.TotalGrid == 1300, "取消或撤销恢复原Grid");
        apply.Invoke(null, new object[] { snapshot, 0L, 0L });
        check(a.HoldEnd.TGrid.TotalGrid == 2640 && a.HoldEnd.XGrid == a.ReferenceLaneStart.CalulateXGrid(a.HoldEnd.TGrid), "重做恢复吸附后的尾点Grid");
        setting.ForceTapHoldMagneticDockToLane = false;
        setting.ForceMagneticDock = false;
        setting.DisableTGridMagneticDock = true;
        var beforeX = selected.Select(o => ((IHorizonPositionObject)o).XGrid.TotalGrid).ToArray();
        drag = Activator.CreateInstance(dragType, F, null, new object[] { selected, all }, null);
        editor.InteractiveManager.GetType().GetField("activeSelectionDrag", F).SetValue(editor.InteractiveManager, drag);
        point = new Point(XGridCalculator.ConvertXGridToX(XGrid.FromTotalGrid((int)(long)dragType.GetField("AnchorX", F).GetValue(drag) + 500), editor), editor.ConvertTGridToY_DesignMode(TGrid.FromTotalGrid(2701)));
        vmType.GetMethod("UpdateSelectionDrag", F).Invoke(editor, new object[] { point });
        var deltas = selected.Select((o, i) => ((IHorizonPositionObject)o).XGrid.TotalGrid - beforeX[i]).ToArray();
        check(a.HoldEnd.XGrid == a.ReferenceLaneStart.CalulateXGrid(a.HoldEnd.TGrid) && b.HoldEnd.XGrid == b.ReferenceLaneStart.CalulateXGrid(b.HoldEnd.TGrid), "关闭吸附开关，Hold尾仍始终贴所属轨道");
        check(a.HoldEnd.TGrid.TotalGrid == 2701 && tap.TGrid.TotalGrid == 2941, "关闭时间吸附使用原始Grid");
        var flat = new LaneLeftStart
        {
            TGrid = TGrid.FromTotalGrid(3000),
            XGrid = XGrid.FromTotalGrid(-2000)
        };
        flat.AddChildObject(new LaneLeftNext
        {
            TGrid = TGrid.FromTotalGrid(3000),
            XGrid = XGrid.FromTotalGrid(2000)
        });
        var flatTap = new Tap
        {
            TGrid = TGrid.FromTotalGrid(3000),
            XGrid = XGrid.Zero,
            ReferenceLaneStart = flat
        };
        var action = new OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.Interactives.Impls.DockableObjectInteractiveAction();
        action.OnMoveCanvas(flatTap, new Point(XGridCalculator.ConvertXGridToX(XGrid.FromTotalGrid(1000), editor), editor.ConvertTGridToY_DesignMode(flatTap.TGrid)), editor);
        check(Math.Abs(flatTap.XGrid.TotalGrid - 1000) <= 1, "单选同T水平段保留鼠标在区间内的X");
        action.OnMoveCanvas(flatTap, new Point(XGridCalculator.ConvertXGridToX(XGrid.FromTotalGrid(5000), editor), editor.ConvertTGridToY_DesignMode(flatTap.TGrid)), editor);
        check(flatTap.XGrid.TotalGrid == 2000, "单选同T水平段夹至最近端点");
        flatTap.XGrid = XGrid.Zero;
        drag = Activator.CreateInstance(dragType, F, null, new object[] { new OngekiTimelineObjectBase[] { flatTap }, new OngekiTimelineObjectBase[] { flatTap } }, null);
        dragType.GetMethod("TryMoveWithSnapping", F).Invoke(drag, new object[] { 0L, 1000L, true });
        check(flatTap.XGrid.TotalGrid == 1000, "多选同T水平段保留公共dx目标");
        action.OnMoveCanvas(flatTap, new Point(XGridCalculator.ConvertXGridToX(XGrid.FromTotalGrid(1234), editor), editor.ConvertTGridToY_DesignMode(TGrid.FromTotalGrid(3501))), editor);
        check(flatTap.TGrid.TotalGrid == 3000 && flatTap.XGrid.TotalGrid == 1000 && ReferenceEquals(flatTap.ReferenceLaneStart, flat), "单选Tap离开原轨有效区间时拒绝移动");
        var flatHold = new Hold
        {
            TGrid = TGrid.FromTotalGrid(100),
            ReferenceLaneStart = flat
        };
        flatHold.SetHoldEnd(new HoldEnd
        {
            TGrid = TGrid.FromTotalGrid(3000),
            XGrid = XGrid.Zero
        });
        var tailAction = new OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.Interactives.Impls.HoldEndObjectInteractiveAction();
        tailAction.OnMoveCanvas(flatHold.HoldEnd, new Point(XGridCalculator.ConvertXGridToX(XGrid.FromTotalGrid(-1000), editor), editor.ConvertTGridToY_DesignMode(TGrid.FromTotalGrid(3000))), editor);
        check(Math.Abs(flatHold.HoldEnd.XGrid.TotalGrid + 1000) <= 1, "单选Hold尾同T水平段保留原始鼠标X");
        tailAction.OnMoveCanvas(flatHold.HoldEnd, new Point(XGridCalculator.ConvertXGridToX(XGrid.FromTotalGrid(1234), editor), editor.ConvertTGridToY_DesignMode(TGrid.FromTotalGrid(3501))), editor);
        check(flatHold.HoldEnd.TGrid.TotalGrid == 3000 && Math.Abs(flatHold.HoldEnd.XGrid.TotalGrid + 1000) <= 1, "单选Hold尾离开原轨区间时拒绝移动");
        foreach (var node in project.Fumen.GetAllDisplayableObjects().OfType<ISelectableObject>())
            node.IsSelected = false;
        setting.DisableTGridMagneticDock = setting.DisableXGridMagneticDock = true;
        var movingLane = new LaneLeftStart
        {
            TGrid = TGrid.Zero,
            XGrid = XGrid.Zero
        };
        var middle = new LaneLeftNext
        {
            TGrid = TGrid.FromTotalGrid(1920),
            XGrid = XGrid.FromTotalGrid(1920)
        };
        var finish = new LaneLeftNext
        {
            TGrid = TGrid.FromTotalGrid(3840),
            XGrid = XGrid.Zero
        };
        movingLane.AddChildObject(middle);
        movingLane.AddChildObject(finish);
        var curveControl = new OngekiFumenEditor.Base.EditorObjects.LaneCurve.LaneCurvePathControlObject
        {
            TGrid = TGrid.FromTotalGrid(2880),
            XGrid = XGrid.FromTotalGrid(300)
        };
        finish.AddControlObject(curveControl);
        var passiveTap = new Tap
        {
            TGrid = TGrid.FromTotalGrid(960),
            ReferenceLaneStart = movingLane,
            XGrid = XGrid.FromTotalGrid(960)
        };
        var passiveHold = new Hold
        {
            TGrid = TGrid.FromTotalGrid(1200),
            ReferenceLaneStart = movingLane
        };
        passiveHold.SetHoldEnd(new HoldEnd
        {
            TGrid = TGrid.FromTotalGrid(2400)
        });
        project.Fumen.AddObject(movingLane);
        project.Fumen.AddObject(passiveTap);
        project.Fumen.AddObject(passiveHold);
        movingLane.IsSelected = middle.IsSelected = finish.IsSelected = true;
        var begin = vmType.GetMethod("BeginSelectionGridDrag", F);
        var realMove = vmType.GetMethod("UpdateSelectionDrag", F);
        begin.Invoke(editor, new object[] { new Point(600, 0) });
        drag = editor.InteractiveManager.GetType().GetField("activeSelectionDrag", F).GetValue(editor.InteractiveManager);
        void MoveDelta(int deltaT, int deltaX)
        {
            var grab = (Point)dragType.GetField("GrabOffset", F).GetValue(drag);
            var anchorT = (long)dragType.GetField("AnchorT", F).GetValue(drag);
            var anchorX = (long)dragType.GetField("AnchorX", F).GetValue(drag);
            realMove.Invoke(editor, new object[] { new Point(grab.X + XGridCalculator.ConvertXGridToX(XGrid.FromTotalGrid((int)anchorX + deltaX), editor), grab.Y + editor.ConvertTGridToY_DesignMode(TGrid.FromTotalGrid((int)anchorT + deltaT))) });
        }

        MoveDelta(240, 500);
        check(movingLane.TGrid.TotalGrid == 240 && middle.TGrid.TotalGrid == 2160 && finish.TGrid.TotalGrid == 4080, "整线已选节点使用公共Grid偏移");
        check(passiveTap.TGrid.TotalGrid == 960 && passiveHold.TGrid.TotalGrid == 1200 && passiveHold.EndTGrid.TotalGrid == 2400, "整线移动未选Tap和Hold头尾保持原时间");
        check(passiveTap.XGrid == movingLane.CalulateXGrid(passiveTap.TGrid) && passiveHold.XGrid == movingLane.CalulateXGrid(passiveHold.TGrid) && passiveHold.HoldEnd.XGrid == movingLane.CalulateXGrid(passiveHold.EndTGrid), "整线变化后所有未选关联头尾重新贴轨");
        check(curveControl.TGrid.TotalGrid == 3120 && curveControl.XGrid.TotalGrid == 800, "整线控制点跟随相同Grid增量保持曲线形状");
        var changed = dragType.GetMethod("CaptureCurrent", F).Invoke(drag, null);
        var original = dragType.GetField("Before", F).GetValue(drag);
        apply.Invoke(null, new object[] { original, 0L, 0L });
        check(movingLane.TGrid.TotalGrid == 0 && passiveTap.TGrid.TotalGrid == 960 && passiveTap.XGrid.TotalGrid == 960, "撤销包含未选附轨物件原坐标");
        apply.Invoke(null, new object[] { changed, 0L, 0L });
        check(passiveTap.XGrid == movingLane.CalulateXGrid(passiveTap.TGrid), "重做包含未选物件投影后的最终坐标");
        foreach (var node in project.Fumen.GetAllDisplayableObjects().OfType<ISelectableObject>())
            node.IsSelected = false;
        middle.IsSelected = true;
        var freeTap = new Tap
        {
            TGrid = TGrid.FromTotalGrid(1301),
            XGrid = XGrid.FromTotalGrid(333)
        };
        project.Fumen.AddObject(freeTap);
        freeTap.IsSelected = true;
        var startTime = movingLane.TGrid.TotalGrid;
        var endTime = finish.TGrid.TotalGrid;
        var startX = movingLane.XGrid.TotalGrid;
        var endX = finish.XGrid.TotalGrid;
        begin.Invoke(editor, new object[] { new Point(600, 0) });
        drag = editor.InteractiveManager.GetType().GetField("activeSelectionDrag", F).GetValue(editor.InteractiveManager);
        MoveDelta(120, 777);
        check(movingLane.TGrid.TotalGrid == startTime && finish.TGrid.TotalGrid == endTime && movingLane.XGrid.TotalGrid == startX && finish.XGrid.TotalGrid == endX, "局部中间点混选移动不改变未选轨节点");
        check(passiveTap.TGrid.TotalGrid == 960 && passiveTap.XGrid == movingLane.CalulateXGrid(passiveTap.TGrid) && passiveHold.EndTGrid.TotalGrid == 2400 && passiveHold.HoldEnd.XGrid == movingLane.CalulateXGrid(passiveHold.EndTGrid), "局部轨形变化仅更新未选关联物件X");
        setting.DisableTGridMagneticDock = setting.DisableXGridMagneticDock = false;
        setting.ForceMagneticDock = true;
        begin.Invoke(editor, new object[] { new Point(600, 0) });
        drag = editor.InteractiveManager.GetType().GetField("activeSelectionDrag", F).GetValue(editor.InteractiveManager);
        MoveDelta(133, 900);
        var basicAction = new OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.Interactives.Impls.DefaultObjectInteractiveAction();
        check(freeTap.XGrid.TotalGrid % XGrid.DEFAULT_RES_X == 0 && middle.XGrid.TotalGrid % XGrid.DEFAULT_RES_X == 0, "混选普通物件各自落横向网格线");
        var flatPassive = new Tap
        {
            TGrid = TGrid.FromTotalGrid(9000),
            XGrid = XGrid.FromTotalGrid(1234),
            ReferenceLaneStart = movingLane
        };
        project.Fumen.AddObject(flatPassive);
        begin.Invoke(editor, new object[] { new Point(600, 0) });
        drag = editor.InteractiveManager.GetType().GetField("activeSelectionDrag", F).GetValue(editor.InteractiveManager);
        var middleBeforeRejectedX = middle.XGrid.TotalGrid;
        editor.IsDirty = false;
        var rejectUndo = new OngekiFumenEditor.Modules.FumenVisualEditor.Kernel.DefaultImpl.DefaultEditorUndoManager(editor);
        vmType.GetProperty("UndoRedoManager").SetValue(editor, rejectUndo);
        MoveDelta(0, 100);
        check(middle.XGrid.TotalGrid == middleBeforeRejectedX && !editor.IsDirty, "无有效投影时回滚整次移动且不改变Dirty");
        vmType.GetMethod("CommitSelectionDrag", F).Invoke(editor, null);
        check(rejectUndo.ActionStack.Count == 0, "拒绝的拖动结束不增加Undo记录");
        check(flatPassive.TGrid.TotalGrid == 9000 && flatPassive.XGrid.TotalGrid == 1234, "未选附轨物件超出轨道有效时间时保留原坐标");
        project.Fumen.RemoveObject(flatPassive);
        setting.DisableTGridMagneticDock = setting.DisableXGridMagneticDock = true;
        foreach (var node in project.Fumen.GetAllDisplayableObjects().OfType<ISelectableObject>())
            node.IsSelected = false;
        foreach (var node in OngekiFumenEditor.Modules.FumenVisualEditor.Kernel.DefaultImpl.GridClipboardGeometry.Enumerate(movingLane).Concat(new OngekiObjectBase[] { passiveTap, passiveHold, passiveHold.HoldEnd }))
            node.PropertyChanged += (_, e) => modified.Invoke(editor, new object[] { node, e });
        middle.IsSelected = true;
        begin.Invoke(editor, new object[] { new Point(600, 0) });
        drag = editor.InteractiveManager.GetType().GetField("activeSelectionDrag", F).GetValue(editor.InteractiveManager);
        check(drag != null, "单个轨道中间点进入统一Grid拖动");
        var priorTapTime = passiveTap.TGrid.TotalGrid;
        var priorMiddleTime = middle.TGrid.TotalGrid;
        metrics = 0;
        MoveDelta(0, 333);
        check(middle.TGrid.TotalGrid == priorMiddleTime && passiveTap.TGrid.TotalGrid == priorTapTime && passiveTap.XGrid == movingLane.CalulateXGrid(passiveTap.TGrid), "单个轨点横移后未选Tap只投影X");
        check(metrics == 0, "纯X移动不触发全谱最大时间扫描");
        middle.IsSelected = false;
        curveControl.IsSelected = true;
        var oldControlX = curveControl.XGrid.TotalGrid;
        begin.Invoke(editor, new object[] { new Point(600, 0) });
        drag = editor.InteractiveManager.GetType().GetField("activeSelectionDrag", F).GetValue(editor.InteractiveManager);
        check(drag != null, "单个曲线控制点进入统一Grid拖动");
        metrics = 0;
        MoveDelta(0, 111);
        check(curveControl.XGrid.TotalGrid == oldControlX + 111 && passiveHold.HoldEnd.TGrid.TotalGrid == 2400 && passiveHold.HoldEnd.XGrid == movingLane.CalulateXGrid(passiveHold.EndTGrid), "单控制点改变曲线后未选Hold尾保持时间并重投影");
        check(metrics == 0, "曲线控制点纯X变化也无全谱时间扫描");
        foreach (var node in project.Fumen.GetAllDisplayableObjects().OfType<ISelectableObject>())
            node.IsSelected = false;
        var boundaryLane = new LaneLeftStart
        {
            TGrid = TGrid.FromTotalGrid(500),
            XGrid = XGrid.Zero
        };
        var boundaryEnd = new LaneLeftNext
        {
            TGrid = TGrid.FromTotalGrid(1500),
            XGrid = XGrid.FromTotalGrid(1000)
        };
        boundaryLane.AddChildObject(boundaryEnd);
        var boundaryTap = new Tap
        {
            TGrid = TGrid.FromTotalGrid(600),
            XGrid = XGrid.FromTotalGrid(100),
            ReferenceLaneStart = boundaryLane
        };
        project.Fumen.AddObject(boundaryLane);
        project.Fumen.AddObject(boundaryTap);
        boundaryLane.IsSelected = true;
        begin.Invoke(editor, new object[] { new Point(600, 0) });
        drag = editor.InteractiveManager.GetType().GetField("activeSelectionDrag", F).GetValue(editor.InteractiveManager);
        MoveDelta(200, 0);
        check(boundaryLane.TGrid.TotalGrid == 600 && boundaryTap.TGrid.TotalGrid == 600 && boundaryTap.XGrid == boundaryLane.CalulateXGrid(boundaryTap.TGrid), "起点5拖到7时停在未选Tap的6且音符时间不变");
        project.Fumen.RemoveObject(boundaryTap);
        begin.Invoke(editor, new object[] { new Point(600, 0) });
        drag = editor.InteractiveManager.GetType().GetField("activeSelectionDrag", F).GetValue(editor.InteractiveManager);
        MoveDelta(100, 0);
        check(boundaryLane.TGrid.TotalGrid == 700, "删除边界Tap后轨道起点可以移动到7");
        boundaryTap.TGrid = TGrid.FromTotalGrid(1000);
        project.Fumen.AddObject(boundaryTap);
        boundaryLane.IsSelected = false;
        boundaryEnd.IsSelected = true;
        begin.Invoke(editor, new object[] { new Point(600, 0) });
        drag = editor.InteractiveManager.GetType().GetField("activeSelectionDrag", F).GetValue(editor.InteractiveManager);
        MoveDelta(-1000, 0);
        check(boundaryEnd.TGrid.TotalGrid == 1000 && boundaryTap.TGrid.TotalGrid == 1000, "轨道终点向前拖动停在最后需要覆盖的未选音符时间");
        boundaryLane.IsSelected = true;
        var originalLength = boundaryEnd.TGrid.TotalGrid - boundaryLane.TGrid.TotalGrid;
        begin.Invoke(editor, new object[] { new Point(600, 0) });
        drag = editor.InteractiveManager.GetType().GetField("activeSelectionDrag", F).GetValue(editor.InteractiveManager);
        MoveDelta(1000, 0);
        check(boundaryLane.TGrid.TotalGrid == 1000 && boundaryEnd.TGrid.TotalGrid - boundaryLane.TGrid.TotalGrid == originalLength, "整根线触及未选音符边界时统一限制dt且不压缩轨道");
        foreach (var node in project.Fumen.GetAllDisplayableObjects().OfType<ISelectableObject>())
            node.IsSelected = false;
        var outsideImportedTap = new Tap
        {
            TGrid = TGrid.FromTotalGrid(500),
            XGrid = XGrid.FromTotalGrid(123),
            ReferenceLaneStart = boundaryLane
        };
        project.Fumen.AddObject(outsideImportedTap);
        boundaryLane.IsSelected = true;
        var importedStartT = boundaryLane.TGrid.TotalGrid;
        begin.Invoke(editor, new object[] { new Point(600, 0) });
        drag = editor.InteractiveManager.GetType().GetField("activeSelectionDrag", F).GetValue(editor.InteractiveManager);
        MoveDelta(0, 333);
        check(boundaryLane.TGrid.TotalGrid == importedStartT && outsideImportedTap.TGrid.TotalGrid == 500, "原有轨道区间外音符不会让纯X拖动擅自修改轨道起点时间");
        var bezierLane = new LaneLeftStart
        {
            TGrid = TGrid.FromTotalGrid(5000),
            XGrid = XGrid.Zero
        };
        var bezierEnd = new LaneLeftNext
        {
            TGrid = TGrid.FromTotalGrid(5000),
            XGrid = XGrid.Zero
        };
        bezierLane.AddChildObject(bezierEnd);
        bezierEnd.AddControlObject(new OngekiFumenEditor.Base.EditorObjects.LaneCurve.LaneCurvePathControlObject
        {
            TGrid = TGrid.FromTotalGrid(5000),
            XGrid = XGrid.FromTotalGrid(10000)
        });
        bezierEnd.AddControlObject(new OngekiFumenEditor.Base.EditorObjects.LaneCurve.LaneCurvePathControlObject
        {
            TGrid = TGrid.FromTotalGrid(5000),
            XGrid = XGrid.FromTotalGrid(10000)
        });
        var projector = typeof(OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.Interactives.Impls.DockableObjectInteractiveAction).GetMethod("CalculateOriginalLaneX", BindingFlags.Static | BindingFlags.NonPublic);
        var actualBezier = (XGrid)projector.Invoke(null, new object[] { bezierLane, TGrid.FromTotalGrid(5000), XGrid.FromTotalGrid(10000) });
        check(actualBezier.TotalGrid <= 7500 && actualBezier.TotalGrid >= 7400, "同T三次Bezier贴合真实曲线范围而非控制点凸包");
        foreach (var node in project.Fumen.GetAllDisplayableObjects().OfType<ISelectableObject>())
            node.IsSelected = false;
        var offbeatLane = new LaneLeftStart
        {
            TGrid = TGrid.FromTotalGrid(245),
            XGrid = XGrid.FromTotalGrid(123)
        };
        var offbeatEnd = new LaneLeftNext
        {
            TGrid = TGrid.FromTotalGrid(1593),
            XGrid = XGrid.FromTotalGrid(2001)
        };
        offbeatLane.AddChildObject(offbeatEnd);
        var offbeatControl = new OngekiFumenEditor.Base.EditorObjects.LaneCurve.LaneCurvePathControlObject
        {
            TGrid = TGrid.FromTotalGrid(777),
            XGrid = XGrid.FromTotalGrid(999)
        };
        offbeatEnd.AddControlObject(offbeatControl);
        project.Fumen.AddObject(offbeatLane);
        offbeatLane.IsSelected = offbeatEnd.IsSelected = true;
        setting.DisableTGridMagneticDock = setting.DisableXGridMagneticDock = false;
        setting.ForceMagneticDock = true;
        begin.Invoke(editor, new object[] { new Point(600, 0) });
        drag = editor.InteractiveManager.GetType().GetField("activeSelectionDrag", F).GetValue(editor.InteractiveManager);
        MoveDelta(333, 777);
        check(offbeatEnd.TGrid.TotalGrid - offbeatLane.TGrid.TotalGrid == 1348 && offbeatControl.TGrid.TotalGrid - offbeatLane.TGrid.TotalGrid == 532, "非网格整线吸附后仍保持节点和控制点原时间差");
        check(offbeatEnd.XGrid.TotalGrid - offbeatLane.XGrid.TotalGrid == 1878 && offbeatControl.XGrid.TotalGrid - offbeatLane.XGrid.TotalGrid == 876, "整线按实际起点吸附统一dx保留形状");
    }
}
