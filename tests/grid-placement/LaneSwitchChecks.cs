using System;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Numerics;
using System.Windows;
using System.Diagnostics;
using Caliburn.Micro;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Base.OngekiObjects.Lane;
using OngekiFumenEditor.Base.OngekiObjects.Lane.Base;
using OngekiFumenEditor.Kernel.Graphics;
using OngekiFumenEditor.Modules.FumenVisualEditor;
using OngekiFumenEditor.Modules.FumenVisualEditor.Models;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.Interactives.Impls;
using OngekiFumenEditor.Modules.FumenVisualEditor.Kernel.DefaultImpl;

public static class LaneSwitchChecks
{
    const BindingFlags F = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    static FumenVisualEditorViewModel Editor()
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
        type.GetField("<Setting>k__BackingField", F).SetValue(editor, new EditorSetting
        {
            BeatSplit = 1,
            ForceMagneticDock = true,
            DisableXGridMagneticDock = false,
            DisableTGridMagneticDock = false
        });
        type.GetField("renderDataLock", F).SetValue(editor, new System.Threading.ReaderWriterLockSlim(System.Threading.LockRecursionPolicy.SupportsRecursion));
        type.GetProperty("ViewWidth", F).SetValue(editor, 1200f);
        type.GetProperty("RectInDesignMode", F).SetValue(editor, new VisibleRect(new Vector2(1200, 0), new Vector2(0, 1000)));
        typeof(EditorSetting).GetProperty("ForceXGridIntegerUnit")?.SetValue(editor.Setting, true);
        editor.TotalDurationHeight = 100000;
        var undo = new DefaultEditorUndoManager(editor);
        typeof(DefaultEditorUndoManager).GetField("isRecoveryCurrentTime", F)?.SetValue(undo, false);
        type.GetProperty("UndoRedoManager").SetValue(editor, undo);
        project.Fumen.BpmList.FirstBpm = 120;
        return editor;
    }

    static LaneLeftStart Lane(FumenVisualEditorViewModel e, float x, float endX, int end = 6000)
    {
        var lane = new LaneLeftStart
        {
            TGrid = TGrid.Zero,
            XGrid = new XGrid(x)
        };
        lane.AddChildObject(new LaneLeftNext
        {
            TGrid = TGrid.FromTotalGrid(end),
            XGrid = new XGrid(endX)
        });
        e.Fumen.AddObject(lane);
        return lane;
    }

    static Tap Tap(FumenVisualEditorViewModel e, LaneStartBase lane, int t = 173)
    {
        var tap = new Tap
        {
            TGrid = TGrid.FromTotalGrid(t),
            ReferenceLaneStart = lane,
            XGrid = lane.CalulateXGrid(TGrid.FromTotalGrid(t))
        };
        e.Fumen.AddObject(tap);
        return tap;
    }

    static Hold Hold(FumenVisualEditorViewModel e, LaneStartBase lane)
    {
        var h = new Hold
        {
            TGrid = TGrid.FromTotalGrid(173),
            XGrid = lane.CalulateXGrid(TGrid.FromTotalGrid(173)),
            ReferenceLaneStart = lane
        };
        h.SetHoldEnd(new HoldEnd
        {
            TGrid = TGrid.FromTotalGrid(823)
        });
        e.Fumen.AddObject(h);
        return h;
    }

    static Point Point(FumenVisualEditorViewModel e, LaneStartBase lane, int t, double extraX = 0) => new(XGridCalculator.ConvertXGridToX(lane.CalulateXGrid(TGrid.FromTotalGrid(t)), e) + extraX, e.ConvertTGridToY_DesignMode(TGrid.FromTotalGrid(t)));
    static object Drag(OngekiTimelineObjectBase[] selected, OngekiTimelineObjectBase[] related)
        => Activator.CreateInstance(typeof(FumenVisualEditorViewModel).Assembly.GetType("OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.Interactives.SelectionGridDrag"), F, null, new object[] { selected, related }, null);
    static bool Move(object drag, long t, long x, FumenVisualEditorViewModel e) => (bool)drag.GetType().GetMethod("TryMoveInEditor", F).Invoke(drag, new object[] { t, x, e });
    public static void Run(Action<bool, string> check)
    {
        var settings = OngekiFumenEditor.Properties.EditorGlobalSetting.Default;
        var saved = new[]
        {
            "BeatSplit",
            "ForceMagneticDock",
            "DisableXGridMagneticDock",
            "DisableTGridMagneticDock",
            "ForceXGridIntegerUnit"
        }.Where(n => settings.Properties[n] != null).ToDictionary(n => n, n => settings[n]);
        try
        {
            RunCore(check);
        }
        finally
        {
            foreach (var p in saved)
                settings[p.Key] = p.Value;
        }
    }

    static void RunCore(Action<bool, string> check)
    {
        foreach (var initiallyDirty in new[] { false, true })
        foreach (var externalEdit in new[] { false, true })
        {
            var e = Editor();
            var lane = Lane(e, -4, -4);
            var tap = Tap(e, lane);
            var other = new Tap { TGrid = TGrid.FromTotalGrid(1000), XGrid = new XGrid(2) };
            e.Fumen.AddObject(other);
            e.IsDirty = initiallyDirty;
            var action = e.InteractiveManager.GetInteractive(tap);
            var from = Point(e, lane, 173);
            action.OnDragStart(tap, from, e);
            action.OnDragMove(tap, Point(e, lane, 960), e);
            e.IsDirty = true;
            if (externalEdit)
                other.XGrid = new XGrid(3);
            e.AbortSelectionDrag();
            check(tap.TGrid.TotalGrid == 173 && !e.InteractiveManager.IsSelectionDragging,
                "取消恢复原位置并释放事务");
            check(e.IsDirty == (initiallyDirty || externalEdit), "取消恢复原脏状态且保留外来编辑");
            check(e.UndoRedoManager.UndoActionCount == 0, "取消不增加撤销动作");
            other.XGrid = new XGrid(4);
            e.IsDirty = true;
            e.AbortSelectionDrag();
            check(e.IsDirty, "无事务时取消不清除外来编辑脏状态");
        }

        foreach (var useGroup in new[] { false, true })
        {
            var e = Editor();
            var lane = Lane(e, 4, 4);
            var tap = new Tap { TGrid = TGrid.FromTotalGrid(173), XGrid = new XGrid(-4) };
            var hold = new Hold { TGrid = TGrid.FromTotalGrid(173), XGrid = new XGrid(-4) };
            hold.SetHoldEnd(new HoldEnd { TGrid = TGrid.FromTotalGrid(823) });
            e.Fumen.AddObject(tap);
            e.Fumen.AddObject(hold);
            if (useGroup)
            {
                var drag = Drag(new OngekiTimelineObjectBase[] { tap, hold }, new OngekiTimelineObjectBase[] { tap, hold, hold.HoldEnd });
                check(Move(drag, 0, 8 * XGrid.DEFAULT_RES_X, e), "未附轨群体可拖至邻轨");
                check(tap.ReferenceLaneStart == lane && hold.ReferenceLaneStart == lane, "未附轨群体自动附着目标轨");
            }
            else
            {
                foreach (var obj in new OngekiTimelineObjectBase[] { tap, hold })
                {
                    var action = e.InteractiveManager.GetInteractive(obj);
                    var from = new Point(XGridCalculator.ConvertXGridToX(new XGrid(-4), e), e.ConvertTGridToY_DesignMode(obj.TGrid));
                    action.OnDragStart(obj, from, e);
                    action.OnDragEnd(obj, Point(e, lane, 173), e);
                    check(((ILaneDockable)obj).ReferenceLaneStart == lane, "未附轨单拖自动附着目标轨");
                }
            }
            check(hold.HoldEnd.XGrid == lane.CalulateXGrid(hold.HoldEnd.TGrid), "初次附轨后Hold尾投影至目标轨");
        }
        {
            var e = Editor();
            var shortLane = Lane(e, 4, 4, 400);
            var hold = new Hold { TGrid = TGrid.FromTotalGrid(173), XGrid = new XGrid(-4) };
            hold.SetHoldEnd(new HoldEnd { TGrid = TGrid.FromTotalGrid(823) });
            e.Fumen.AddObject(hold);
            var action = e.InteractiveManager.GetInteractive(hold);
            action.OnDragStart(hold, new Point(XGridCalculator.ConvertXGridToX(hold.XGrid, e), e.ConvertTGridToY_DesignMode(hold.TGrid)), e);
            action.OnDragEnd(hold, Point(e, shortLane, 173), e);
            check(hold.ReferenceLaneStart == null && hold.HoldEnd.TGrid.TotalGrid == 823,
                "未附轨Hold不能附着未覆盖完整头尾的目标轨");
        }

        foreach (var distance in new[]
        {
            9d,
            7d
        })
        {
            var e = Editor();
            var a = Lane(e, -4, -4);
            var b = Lane(e, 4, 4);
            var tap = Tap(e, a);
            new DockableObjectInteractiveAction().OnMoveCanvas(tap, Point(e, b, 173, distance), e);
            check(tap.ReferenceLaneStart == (distance < 8 ? b : a), $"Tap 换线阈值 {distance} 像素");
            check(tap.TGrid.TotalGrid == 173, "纯横移不触发时间网格吸附");
        }

        {
            var e = Editor();
            var a = Lane(e, -4, -4);
            var b = Lane(e, 4, 8);
            var h = Hold(e, a);
            var action = e.InteractiveManager.GetInteractive(h);
            var from = Point(e, a, 173);
            var to = Point(e, b, 173);
            action.OnDragStart(h, from, e);
            check(e.InteractiveManager.IsSelectionDragging, "单个Hold复用管理器的选择拖动事务");
            action.OnDragMove(h, to, e);
            action.OnDragEnd(h, to, e);
            check(!e.InteractiveManager.IsSelectionDragging, "单拖结束释放管理器事务");
            check(h.ReferenceLaneStart == b && h.HoldEnd.ReferenceLaneStart == b, "只拖 Hold 头换整条所属轨");
            check(h.TGrid.TotalGrid == 173 && h.HoldEnd.TGrid.TotalGrid == 823, "只横拖头时头尾 TGrid 均不变");
            check(h.HoldEnd.XGrid == b.CalulateXGrid(h.HoldEnd.TGrid), "未选尾横向贴新轨");
            e.UndoRedoManager.Undo(1);
            check(h.ReferenceLaneStart == a && h.TGrid.TotalGrid == 173 && h.HoldEnd.TGrid.TotalGrid == 823 && h.HoldEnd.XGrid == a.CalulateXGrid(h.HoldEnd.TGrid), "真实 Undo 恢复头尾引用与坐标");
            e.UndoRedoManager.Redo(1);
            check(h.ReferenceLaneStart == b && h.HoldEnd.XGrid == b.CalulateXGrid(h.HoldEnd.TGrid), "真实 Redo 恢复换线结果");
        }

        {
            var e = Editor();
            var a = Lane(e, -4, -4);
            var b = Lane(e, 4, 4, 400);
            var h = Hold(e, a);
            new DockableObjectInteractiveAction().OnMoveCanvas(h, Point(e, b, 173), e);
            check(h.ReferenceLaneStart == a && h.HoldEnd.TGrid.TotalGrid == 823, "目标轨未覆盖 Hold 尾时拒绝换线");
        }

        {
            var e = Editor();
            var a = Lane(e, -4, -4);
            var b = Lane(e, 4, 4);
            var h = Hold(e, a);
            new HoldEndObjectInteractiveAction().OnMoveCanvas(h.HoldEnd, Point(e, b, 823), e);
            check(h.ReferenceLaneStart == a && h.HoldEnd.XGrid == a.CalulateXGrid(h.HoldEnd.TGrid), "单独拖 Hold 尾保持原轨");
        }

        foreach (var both in new[]
        {
            false,
            true
        })
        {
            var e = Editor();
            var a = Lane(e, -4, -4);
            var b = Lane(e, 4, 8);
            var h = Hold(e, a);
            e.Setting.DisableTGridMagneticDock = !both;
            var related = new OngekiTimelineObjectBase[]
            {
                h,
                h.HoldEnd
            };
            var drag = Drag(both ? related : new OngekiTimelineObjectBase[] { h }, related);
            var delta = (long)b.CalulateXGrid(TGrid.FromTotalGrid(173)).TotalGrid - h.XGrid.TotalGrid;
            check(Move(drag, both ? 500 : 0, delta, e), "多选换线操作合法");
            check(h.ReferenceLaneStart == b && h.HoldEnd.ReferenceLaneStart == b, "多选按 Hold 头选择轨道");
            check(both ? h.HoldEnd.TGrid.TotalGrid - h.TGrid.TotalGrid == 650 : h.HoldEnd.TGrid.TotalGrid == 823, "头尾同选保留 T 间距／未选尾保持 T");
            check(h.HoldEnd.XGrid == b.CalulateXGrid(h.HoldEnd.TGrid), "头尾统一投影目标曲线");
            var before = drag.GetType().GetField("Before", F).GetValue(drag);
            drag.GetType().GetMethod("Apply", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] { before, 0L, 0L });
            check(h.ReferenceLaneStart == a && h.HoldEnd.XGrid == a.CalulateXGrid(h.HoldEnd.TGrid), "多选快照恢复原轨与尾坐标");
        }

        {
            var e = Editor();
            var a = Lane(e, -8, -8);
            var b = Lane(e, 0, 0);
            var c = Lane(e, 8, 8);
            var x = Tap(e, a);
            var y = Tap(e, b);
            var objects = new OngekiTimelineObjectBase[]
            {
                x,
                y
            };
            check(Move(Drag(objects, objects), 0, 8L * 4096, e), "多轨 Tap 组移动成功");
            check(x.ReferenceLaneStart == b && y.ReferenceLaneStart == c, "多个物件各自换到邻近轨");
            check(x.TGrid.TotalGrid == 173 && y.TGrid.TotalGrid == 173, "多选纯横移保持时间");
        }

        {
            var e = Editor();
            var a = Lane(e, -4, -4);
            var b = Lane(e, 4, 4);
            for (var i = 0; i < 10000; i++)
                Tap(e, a, 10000 + i);
            var notes = Enumerable.Range(0, 200).Select(_ => Tap(e, a)).ToArray();
            var objects = notes.Cast<OngekiTimelineObjectBase>().ToArray();
            var drag = Drag(objects, objects);
            check(Move(drag, 0, 8L * 4096, e) && notes.All(n => n.ReferenceLaneStart == b), "200 个 Tap 群体换线");
            var notifications = 0;
            foreach (var n in notes)
                n.PropertyChanged += (_, args) =>
                {
                    if (args.PropertyName is "XGrid" or "TGrid" or "ReferenceLaneStart")
                        notifications++;
                };
            var watch = Stopwatch.StartNew();
            for (var i = 0; i < 100; i++)
                Move(drag, 0, 8L * 4096, e);
            watch.Stop();
            var resolver = drag.GetType().GetField("laneSwitchResolver", F)?.GetValue(drag);
            var candidates = resolver?.GetType().GetField("candidates", F)?.GetValue(resolver) as System.Collections.IDictionary;
            var queries = candidates?.Count ?? -1;
            check(queries == 1, "同时间群体拖动只查询一次候选轨道");
            check(notifications == 0, "重复相同换线位置没有坐标或引用通知");
            Console.WriteLine($"200 Tap／10000 背景物件／100 次重复位置：{watch.Elapsed.TotalMilliseconds:F2} ms，候选查询={queries}，坐标与引用通知={notifications}");
            check(Move(drag, 0, 0, e) && notes.All(n => n.ReferenceLaneStart == a), "拖回起始位置可换回原轨");
        }

        {
            var e = Editor();
            var a = Lane(e, -4, -4);
            var b = Lane(e, 4, 4);
            var h = Hold(e, a);
            var tap = Tap(e, a);
            var tailOnly = Drag(new OngekiTimelineObjectBase[] { h.HoldEnd, tap }, new OngekiTimelineObjectBase[] { h, h.HoldEnd, tap });
            check(Move(tailOnly, 0, 8L * 4096, e) && tap.ReferenceLaneStart == b && h.ReferenceLaneStart == a, "混选 Tap 和 Hold 尾时仅 Tap 换线");
            var moved = Drag(new OngekiTimelineObjectBase[] { a, h }, new OngekiTimelineObjectBase[] { a, a.Children.First(), h, h.HoldEnd });
            check(Move(moved, 0, 4096, e) && h.ReferenceLaneStart == a, "轨道参与拖动时附轨物件不换线");
        }

    }
}
