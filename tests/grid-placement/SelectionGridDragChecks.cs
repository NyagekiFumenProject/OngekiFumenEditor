using System;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.Collections;
using OngekiFumenEditor.Modules.FumenVisualEditor;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Base.OngekiObjects.Lane;
using System.Collections.Generic;
using System.Reflection;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;

public static class SelectionGridDragChecks
{
    private sealed class SelectionGridDrag
    {
        private static readonly Type DragType = typeof(FumenVisualEditorViewModel).GetNestedType("SelectionGridDrag", BindingFlags.NonPublic);
        private const BindingFlags Members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private readonly object instance;
        public SelectionGridDrag(IEnumerable<OngekiTimelineObjectBase> selected, IEnumerable<OngekiTimelineObjectBase> related)
            => instance = Activator.CreateInstance(DragType, Members, null, new object[] { selected, related }, null);
        public long AnchorT => (long)DragType.GetField("AnchorT", Members).GetValue(instance);
        public long AnchorX => (long)DragType.GetField("AnchorX", Members).GetValue(instance);
        public object Before => DragType.GetField("Before", Members).GetValue(instance);
        public bool TryMove(long dt, long dx) => (bool)DragType.GetMethod("TryMove", Members).Invoke(instance, new object[] { dt, dx });
        public object CaptureCurrent() => DragType.GetMethod("CaptureCurrent", Members).Invoke(instance, null);
        public static void Apply(object positions) => DragType.GetMethod("Apply", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { positions, 0L, 0L });
    }

    public static void Run(Action<bool, string> check)
    {
        Tap Point(int t, int x) => new Tap { TGrid = TGrid.FromTotalGrid(t), XGrid = XGrid.FromTotalGrid(x) };
        // 实际谱面在1920处由120 BPM/4拍变为240 BPM/3/8拍；BPM与拍号均非移动对象。
        var timingBpms = new BpmList { FirstBpm = 120 };
        timingBpms.Add(new BPMChange { TGrid = TGrid.FromTotalGrid(1920), BPM = 240 });
        var timingMeters = new MeterChangeList();
        timingMeters.Add(new MeterChange { TGrid = TGrid.FromTotalGrid(1920), BunShi = 3, Bunbo = 8 });
        var timingSoflans = new SoflanList();
        foreach (var expectedTick in new[] { 960, 2640 })
        {
            var expected = TGrid.FromTotalGrid(expectedTick);
            var lineY = TGridCalculator.ConvertTGridToY_DesignMode(expected, timingSoflans, timingBpms, 1);
            var snapped = TGridCalculator.TryPickMagneticBeatTime_DesignMode((float)(lineY + 1), 4, timingSoflans, timingBpms, timingMeters, 1, 1);
            check(snapped.tGrid != null && snapped.tGrid.TotalGrid == expectedTick, "真实BPM拍号区域磁吸选中目标节奏线");
            var forced = TGridCalculator.TryPickClosestBeatTime_DesignMode((float)(lineY + 1), timingSoflans, timingBpms, timingMeters, 1, 1);
            check(forced.tGrid != null && forced.tGrid.TotalGrid == expectedTick, "真实BPM拍号区域强制吸附选中目标节奏线");
            var snappedTarget = TGridCalculator.ConvertYToTGrid_DesignMode(snapped.y, timingSoflans, timingBpms, 1);
            check(snappedTarget.TotalGrid == expectedTick, "吸附Y回转Grid仍精确落线");
            var snapA = Point(100, -100);
            var snapB = Point(1733, 300);
            var snapDrag = new SelectionGridDrag(new[] { snapA, snapB }, new[] { snapA, snapB });
            check(snapDrag.TryMove((long)snappedTarget.TotalGrid - snapDrag.AnchorT, 0), "实际节奏线目标可整组移动");
            check(snapA.TGrid.TotalGrid == expectedTick && snapB.TGrid.TotalGrid - snapA.TGrid.TotalGrid == 1633, "参考点落线且非节奏线原始Grid差不变");
            check(snapB.XGrid.TotalGrid - snapA.XGrid.TotalGrid == 400, "跨变更区域横向Grid差不变");
        }

        var a = Point(100, -300);
        var b = Point(1700, 700);
        var drag = new SelectionGridDrag(new[] { a, b }, new[] { a, b });
        check(drag.AnchorT == 100 && drag.AnchorX == 200, "整组锚点为最早时间与左右中心");
        check(drag.TryMove(9000, -200000), "跨BPM拍号的统一Grid平移");
        check(b.TGrid.TotalGrid - a.TGrid.TotalGrid == 1600 && b.XGrid.TotalGrid - a.XGrid.TotalGrid == 1000, "组内相对位置保持");
        check(a.XGrid.TotalGrid == -200300, "横向允许负值和越过显示范围");
        check(!drag.TryMove(-101, 0) && a.TGrid.TotalGrid == 9100, "负时间整组拒绝且保持上次有效位置");
        check(!drag.TryMove(int.MaxValue, 0) && !drag.TryMove(0, long.MaxValue - 1000), "时间横向溢出整组拒绝");
        var after = drag.CaptureCurrent();
        SelectionGridDrag.Apply(drag.Before);
        check(a.TGrid.TotalGrid == 100 && b.XGrid.TotalGrid == 700, "撤销恢复原Grid");
        SelectionGridDrag.Apply(after);
        check(a.TGrid.TotalGrid == 9100 && a.XGrid.TotalGrid == -200300, "重做恢复真实Grid");

        var hold = new Hold { TGrid = TGrid.FromTotalGrid(100) };
        hold.SetHoldEnd(new HoldEnd { TGrid = TGrid.FromTotalGrid(500), XGrid = XGrid.FromTotalGrid(900) });
        var other = Point(200, 100);
        var headOnly = new SelectionGridDrag(new OngekiTimelineObjectBase[] { hold, other }, new OngekiTimelineObjectBase[] { hold, hold.HoldEnd, other });
        check(headOnly.TryMove(200, 30) && hold.HoldEnd.TGrid.TotalGrid == 500 && hold.HoldEnd.XGrid.TotalGrid == 900, "未选Hold尾不跟随");
        check(!headOnly.TryMove(401, 0) && hold.TGrid.TotalGrid == 300, "头越过未选尾整组拒绝");
        var both = new SelectionGridDrag(new OngekiTimelineObjectBase[] { hold, hold.HoldEnd }, new OngekiTimelineObjectBase[] { hold, hold.HoldEnd });
        check(both.TryMove(700, 20) && hold.TGrid.TotalGrid == 1000 && hold.HoldEnd.TGrid.TotalGrid == 1200, "Hold头尾同选各移动一次");

        var soflan = new Soflan { TGrid = TGrid.FromTotalGrid(100) };
        soflan.EndTGrid = TGrid.FromTotalGrid(500);
        var duration = new SelectionGridDrag(new OngekiTimelineObjectBase[] { soflan, soflan.EndIndicator }, new OngekiTimelineObjectBase[] { soflan, soflan.EndIndicator });
        check(duration.TryMove(800, 0) && soflan.EndTGrid.TotalGrid == 1300, "持续头尾移动不受setter钳制影响");
        SelectionGridDrag.Apply(duration.Before);
        check(soflan.TGrid.TotalGrid == 100 && soflan.EndTGrid.TotalGrid == 500, "持续范围撤销恢复");
        var area = new IndividualSoflanArea { TGrid = TGrid.FromTotalGrid(100), XGrid = XGrid.FromTotalGrid(20) };
        area.EndIndicator.TGrid = TGrid.FromTotalGrid(500);
        area.EndIndicator.XGrid = XGrid.FromTotalGrid(70);
        var areaOther = Point(1000, 300);
        var areaHead = new SelectionGridDrag(new OngekiTimelineObjectBase[] { area, areaOther }, new OngekiTimelineObjectBase[] { area, area.EndIndicator, areaOther });
        check(areaHead.TryMove(100, 10) && area.EndIndicator.TGrid.TotalGrid == 500 && area.EndIndicator.XGrid.TotalGrid == 70, "ISA仅选头与普通物件混选不移动尾");
        check(!areaHead.TryMove(401, 20) && area.TGrid.TotalGrid == 200 && areaOther.TGrid.TotalGrid == 1100, "ISA头越过未选尾整组拒绝");
        SelectionGridDrag.Apply(areaHead.Before);
        var areaTail = new SelectionGridDrag(new OngekiTimelineObjectBase[] { area.EndIndicator, areaOther }, new OngekiTimelineObjectBase[] { area, area.EndIndicator, areaOther });
        check(!areaTail.TryMove(-401, 0) && area.EndIndicator.TGrid.TotalGrid == 500 && areaOther.TGrid.TotalGrid == 1000, "ISA仅选尾与普通物件混选越头整组拒绝");
        check(areaTail.TryMove(-50, -10) && area.TGrid.TotalGrid == 100 && area.EndIndicator.TGrid.TotalGrid == 450, "ISA仅选尾合法平移保留未选头");
        SelectionGridDrag.Apply(areaTail.Before);
        var areaBoth = new SelectionGridDrag(new OngekiTimelineObjectBase[] { area.EndIndicator, area, areaOther }, new OngekiTimelineObjectBase[] { area.EndIndicator, area, areaOther });
        check(areaBoth.TryMove(700, 25) && area.TGrid.TotalGrid == 800 && area.EndIndicator.TGrid.TotalGrid == 1200 && areaOther.TGrid.TotalGrid == 1700, "ISA两端同选按父先尾后移动且各一次");
        SelectionGridDrag.Apply(areaBoth.Before);
        check(area.TGrid.TotalGrid == 100 && area.EndIndicator.TGrid.TotalGrid == 500 && area.EndIndicator.XGrid.TotalGrid == 70, "ISA反序快照撤销恢复原头尾Grid");
        var start = new LaneLeftStart { TGrid = TGrid.FromTotalGrid(50), XGrid = XGrid.FromTotalGrid(40) };
        var laneSelection = new SelectionGridDrag(new OngekiTimelineObjectBase[] { start, other }, new OngekiTimelineObjectBase[] { start, other });
        check(laneSelection.TryMove(10, 20) && start.TGrid.TotalGrid == 60 && start.XGrid.TotalGrid == 60, "轨道起点只应用一次偏移");
        hold.ReferenceLaneStart = start;
        var originalTail = hold.HoldEnd;
        var refs = new SelectionGridDrag(new OngekiTimelineObjectBase[] { hold, other }, new OngekiTimelineObjectBase[] { hold, hold.HoldEnd, other });
        var originalTailX = hold.HoldEnd.XGrid.TotalGrid;
        hold.ReferenceLaneStart = new LaneRightStart();
        SelectionGridDrag.Apply(refs.Before);
        check(ReferenceEquals(hold.ReferenceLaneStart, start) && ReferenceEquals(hold.HoldEnd, originalTail) && ReferenceEquals(originalTail.RefHold, hold), "恢复原附轨引用且保持头尾对象引用");
        check(hold.HoldEnd.XGrid.TotalGrid == originalTailX, "引用恢复后的尾部横坐标最后还原");
    }
}
