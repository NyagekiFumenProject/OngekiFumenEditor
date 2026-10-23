using System;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Numerics;
using System.Windows;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.EditorObjects.LaneCurve;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Base.OngekiObjects.Lane;
using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Kernel.Graphics;
using OngekiFumenEditor.Modules.FumenVisualEditor;
using OngekiFumenEditor.Modules.FumenVisualEditor.Models;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;

public static class StartCurveCacheChecks
{
    const BindingFlags F = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    static (LaneLeftStart lane, LaneLeftNext first, LaneLeftNext last) MakeLane()
    {
        var lane = new LaneLeftStart
        {
            TGrid = new TGrid(),
            XGrid = new XGrid(),
            CurveInterpolaterFactory = OngekiFumenEditor.Kernel.CurveInterpolater.DefaultImpl.Factory.DefaultCurveInterpolaterFactory.Default
        };
        var first = new LaneLeftNext
        {
            TGrid = TGrid.FromTotalGrid(1920),
            XGrid = XGrid.FromTotalGrid(4096)
        };
        var last = new LaneLeftNext
        {
            TGrid = TGrid.FromTotalGrid(3840),
            XGrid = new XGrid()
        };
        lane.AddChildObject(first);
        lane.AddChildObject(last);
        first.AddControlObject(new LaneCurvePathControlObject
        {
            TGrid = TGrid.FromTotalGrid(960),
            XGrid = XGrid.FromTotalGrid(3072)
        });
        return (lane, first, last);
    }

    public static void Run(Action<bool, string> check)
    {
        CheckTimeIndex(check);
        // 先缓存，再单独横移，避免纵向变动掩盖缓存遗漏。
        foreach (var component in new[]
        {
            "对象",
            "Unit",
            "Grid"
        })
        {
            var (lane, first, last) = MakeLane();
            _ = first.GetConnectionPaths();
            var unaffected = last.GetConnectionPaths();
            if (component == "对象")
                lane.XGrid = XGrid.FromTotalGrid(1024);
            else if (component == "Unit")
                lane.XGrid.Unit = 1;
            else
                lane.XGrid.Grid = 1024;
            check(first.GetConnectionPaths()[0].pos.X == lane.XGrid.TotalGrid, $"Start 横移（{component}）刷新曲线起点");
            var fresh = MakeLane();
            fresh.lane.XGrid = lane.XGrid.CopyNew();
            check(lane.CalulateXGrid(TGrid.FromTotalGrid(480)) == fresh.lane.CalulateXGrid(TGrid.FromTotalGrid(480)), $"Start 横移（{component}）内部插值与新曲线一致");
            check(ReferenceEquals(unaffected, last.GetConnectionPaths()), $"Start 横移（{component}）复用无关曲线段");
            check(ReferenceEquals(first.GetConnectionPaths(), first.GetConnectionPaths()), $"Start 横移（{component}）重复查询复用缓存");
        }

        foreach (var target in new[]
        {
            "Start",
            "中间点"
        })
            foreach (var axis in new[]
            {
                "XGrid",
                "TGrid"
            })
            {
                var (lane, first, last) = MakeLane();
                var node = target == "Start" ? (ConnectableObjectBase)lane : first;
                _ = first.GetConnectionPaths();
                _ = last.GetConnectionPaths();
                var freshDuringNotification = false;
                lane.ConnectableObjectsPropertyChanged += (sender, e) =>
                {
                    if (!ReferenceEquals(sender, node) || e.PropertyName != axis)
                        return;
                    var path = first.GetConnectionPaths();
                    freshDuringNotification = path[0].pos == new Vector2(lane.XGrid.TotalGrid, lane.TGrid.TotalGrid)
                    && path[path.Count - 1].pos == new Vector2(first.XGrid.TotalGrid, first.TGrid.TotalGrid);
                };
                if (axis == "XGrid")
                    node.XGrid = XGrid.FromTotalGrid(1024);
                else
                    node.TGrid = TGrid.FromTotalGrid(target == "Start" ? 240 : 2160);
                check(freshDuringNotification, $"{target} {axis} 通知监听者时路径已更新");
            }

        foreach (var useSelection in new[]
        {
            false,
            true
        })
        {
            var vmType = typeof(FumenVisualEditorViewModel);
            var editor = (FumenVisualEditorViewModel)RuntimeHelpers.GetUninitializedObject(vmType);
        vmType.GetProperty("InteractiveManager").SetValue(editor, new OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.Interactives.ObjectInteractiveManager());
            editor.IsNotifying = true;
            var project = new EditorProjectDataModel
            {
                AudioDuration = TimeSpan.FromSeconds(10)
            };
            vmType.GetField("editorProjectData", F).SetValue(editor, project);
            vmType.GetField("<Setting>k__BackingField", F).SetValue(editor, new EditorSetting
            {
                DisableTGridMagneticDock = true,
                DisableXGridMagneticDock = true,
                ForceXGridIntegerUnit = false
            });
            vmType.GetProperty("ViewWidth", F).SetValue(editor, 1200f);
            vmType.GetProperty("RectInDesignMode", F).SetValue(editor, new VisibleRect(new Vector2(1200, 0), new Vector2(0, 1000)));
            project.Fumen.BpmList.FirstBpm = 120;
            var (lane, first, last) = MakeLane();
            var tap = new Tap
            {
                TGrid = TGrid.FromTotalGrid(240),
                ReferenceLaneStart = lane
            };
            var hold = new Hold
            {
                TGrid = TGrid.FromTotalGrid(480),
                ReferenceLaneStart = lane
            };
            hold.SetHoldEnd(new HoldEnd
            {
                TGrid = TGrid.FromTotalGrid(1440)
            });
            project.Fumen.AddObject(lane);
            project.Fumen.AddObject(tap);
            project.Fumen.AddObject(hold);
            ConnectableObjectDockingHelper.RelocateDockableObjects(project.Fumen, lane);
            var notes = new OngekiMovableObjectBase[]
            {
                tap,
                hold,
                hold.HoldEnd
            };
            var oldX = notes.Select(n => n.XGrid.TotalGrid).ToArray();
            var otherCache = last.GetConnectionPaths();
            var fresh = MakeLane();
            fresh.lane.XGrid = XGrid.FromTotalGrid(1024);
            var label = useSelection ? "选择拖动入口" : "单节点拖动入口";
            object drag = null;
            Type dragType = null;
            if (useSelection)
            {
                lane.IsSelected = true;
                vmType.GetMethod("BeginSelectionGridDrag", F).Invoke(editor, new object[] { new Point(600, 0) });
                drag = editor.InteractiveManager.GetType().GetField("activeSelectionDrag", F).GetValue(editor.InteractiveManager);
                dragType = drag.GetType();
                var grab = (Point)dragType.GetField("GrabOffset", F).GetValue(drag);
                vmType.GetMethod("UpdateSelectionDrag", F).Invoke(editor, new object[] { new Point(grab.X + XGridCalculator.ConvertXGridToX(fresh.lane.XGrid, editor), grab.Y + editor.ConvertTGridToY_DesignMode(lane.TGrid)) });
            }
            else
            {
                var actionType = vmType.Assembly.GetType("OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.Interactives.Impls.ConnectableObjectInteractiveAction");
                actionType.GetMethod("OnMoveCanvas").Invoke(Activator.CreateInstance(actionType, true), new object[] { lane, new Point(XGridCalculator.ConvertXGridToX(fresh.lane.XGrid, editor), editor.ConvertTGridToY_DesignMode(lane.TGrid)), editor });
            }

            check(lane.XGrid.TotalGrid == 1024 && lane.TGrid.TotalGrid == 0, $"{label}确实只横移 Start");
            for (var i = 0; i < notes.Length; i++)
                check(notes[i].XGrid.TotalGrid != oldX[i] && notes[i].XGrid == fresh.lane.CalulateXGrid(notes[i].TGrid), $"{label} {notes[i].GetType().Name} 随新曲线贴轨");
            check(tap.TGrid.TotalGrid == 240 && hold.TGrid.TotalGrid == 480 && hold.HoldEnd.TGrid.TotalGrid == 1440, $"{label}未选音符时间不变");
            check(ReferenceEquals(otherCache, last.GetConnectionPaths()), $"{label}未重建不相关段");
            if (drag != null)
            {
                var after = dragType.GetMethod("CaptureCurrent", F).Invoke(drag, null);
                var before = dragType.GetField("Before", F).GetValue(drag);
                var apply = dragType.GetMethod("Apply", BindingFlags.Static | BindingFlags.NonPublic);
                apply.Invoke(null, new object[] { before, 0L, 0L });
                check(lane.XGrid.TotalGrid == 0 && notes.Select((n, i) => n.XGrid.TotalGrid == oldX[i]).All(x => x), "横移撤销恢复起点及音符");
                check(first.GetConnectionPaths()[0].pos.X == 0, "横移撤销刷新曲线");
                apply.Invoke(null, new object[] { after, 0L, 0L });
                check(lane.XGrid.TotalGrid == 1024 && first.GetConnectionPaths()[0].pos.X == 1024 && notes.All(n => n.XGrid == fresh.lane.CalulateXGrid(n.TGrid)), "横移重做恢复新曲线及贴轨音符");
            }
        }
    }

    static void CheckTimeIndex(Action<bool, string> check)
    {
        var fumen = new OngekiFumen();
        var lane = new LaneLeftStart
        {
            TGrid = new TGrid(1),
            XGrid = new XGrid()
        };
        lane.AddChildObject(new LaneLeftNext
        {
            TGrid = new TGrid(3),
            XGrid = new XGrid(2)
        });
        fumen.AddObject(lane);
        _ = lane.MinTGrid;
        _ = lane.MaxTGrid;
        var notifications = 0;
        lane.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is "MinTGrid" or "MaxTGrid")
                notifications++;
        };
        for (var i = 1; i <= 200; i++)
        {
            lane.XGrid.Unit = i;
            lane.XGrid.Grid = i;
        }

        check(notifications == 0, "400 次纯 X 分量更新不重建时间索引");
        check(fumen.Lanes.GetVisibleStartObjects(new TGrid(1), new TGrid(1)).Contains(lane), "X 分量更新保留区间查询");
        var xNotifications = notifications;
        lane.TGrid.Unit = 2;
        check(lane.MinTGrid.TotalGrid == 3840 && !fumen.Lanes.GetVisibleStartObjects(new TGrid(1), new TGrid(1)).Contains(lane), "T Unit 更新仍刷新索引");
        lane.TGrid.Grid = 240;
        check(lane.MinTGrid.TotalGrid == 4080 && !fumen.Lanes.GetVisibleStartObjects(new TGrid(2), new TGrid(2)).Contains(lane), "T Grid 更新仍刷新索引");
        var (_, first, _) = MakeLane();
        var path = first.GetConnectionPaths();
        first.CurvePrecision /= 2;
        check(!ReferenceEquals(path, first.GetConnectionPaths()), "保留官方 CurvePrecision 缓存刷新");
        Console.WriteLine($"纯 X 分量 400 次更新：时间索引通知={xNotifications}");
    }

}
