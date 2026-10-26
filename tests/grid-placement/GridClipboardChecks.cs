using System;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.Collections;
using OngekiFumenEditor.Modules.FumenVisualEditor;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Modules.FumenVisualEditor.Base;

public static class GridClipboardChecks
{
    public static int Run()
    {
        var checks = 0;
        void Check(bool condition, string name)
        {
            if (!condition)
                throw new Exception(name);
            checks++;
        }

        Tap Point(int t, int x) => new Tap
        {
            TGrid = TGrid.FromTotalGrid(t),
            XGrid = XGrid.FromTotalGrid(x)
        };

        Check(new TGrid(8739, 1).TotalGrid == 16778881, "长时间轴保留单个Grid");
        Check(TGrid.FromTotalGrid(int.MaxValue).TotalGrid == int.MaxValue, "最大时间坐标往返");
        Check(TGrid.MinValue != null, "最小时间哨兵仍可初始化");
        // 实际谱面在1920处由120 BPM/4拍变为240 BPM/3/8拍；BPM与拍号均非移动对象。
        var timingBpms = new BpmList
        {
            FirstBpm = 120
        };
        timingBpms.Add(new BPMChange
        {
            TGrid = TGrid.FromTotalGrid(1920),
            BPM = 240
        });
        var timingMeters = new MeterChangeList();
        timingMeters.Add(new MeterChange
        {
            TGrid = TGrid.FromTotalGrid(1920),
            BunShi = 3,
            Bunbo = 8
        });
        var timingSoflans = new SoflanList();
        foreach (var expectedTick in new[]
        {
            960,
            2640
        })
        {
            var expected = TGrid.FromTotalGrid(expectedTick);
            var lineY = TGridCalculator.ConvertTGridToY_DesignMode(expected, timingSoflans, timingBpms, 1);
            var snapped = TGridCalculator.TryPickMagneticBeatTime_DesignMode((float)(lineY + 1), 4, timingSoflans, timingBpms, timingMeters, 1, 1);
            Check(snapped.tGrid != null && snapped.tGrid.TotalGrid == expectedTick, "真实BPM拍号区域磁吸选中目标节奏线");
            var forced = TGridCalculator.TryPickClosestBeatTime_DesignMode((float)(lineY + 1), timingSoflans, timingBpms, timingMeters, 1, 1);
            Check(forced.tGrid != null && forced.tGrid.TotalGrid == expectedTick, "真实BPM拍号区域强制吸附选中目标节奏线");
            var snappedTarget = TGridCalculator.ConvertYToTGrid_DesignMode(snapped.y, timingSoflans, timingBpms, 1);
            Check(snappedTarget.TotalGrid == expectedTick, "吸附Y回转Grid仍精确落线");
            var snapA = Point(100, -100);
            var snapB = Point(1733, 300);
            Check(GridClipboardGeometry.Place(new[] { snapA, snapB }, XGrid.Zero, snappedTarget, PasteOption.None), "实际节奏线目标可整组粘贴");
            Check(snapA.TGrid.TotalGrid == expectedTick && snapB.TGrid.TotalGrid - snapA.TGrid.TotalGrid == 1633, "参考点落线且非节奏线原始Grid差不变");
            Check(snapB.XGrid.TotalGrid - snapA.XGrid.TotalGrid == 400, "跨变更区域横向Grid差不变");
        }

        var a = Point(100, -100);
        var b = Point(1700, 300);
        var bpm = new BPMChange
        {
            TGrid = TGrid.FromTotalGrid(500),
            BPM = 180
        };
        var meter = new MeterChange
        {
            TGrid = TGrid.FromTotalGrid(900),
            BunShi = 3,
            Bunbo = 8
        };
        var group = new OngekiObjectBase[]
        {
            a,
            bpm,
            meter,
            b
        };
        Check(GridClipboardGeometry.Place(group, XGrid.FromTotalGrid(-200000), TGrid.FromTotalGrid(20000), PasteOption.None), "跨BPM拍号平移成功");
        Check(b.TGrid.TotalGrid - a.TGrid.TotalGrid == 1600 && bpm.TGrid.TotalGrid - a.TGrid.TotalGrid == 400 && meter.TGrid.TotalGrid - a.TGrid.TotalGrid == 800, "跨BPM拍号保留原始时间偏移");
        Check(b.XGrid.TotalGrid - a.XGrid.TotalGrid == 400 && a.XGrid.TotalGrid < -24 * 4096, "负X跨显示边界保留横向偏移");

        var hold = new Hold
        {
            TGrid = TGrid.FromTotalGrid(100),
            XGrid = XGrid.FromTotalGrid(-400)
        };
        hold.SetHoldEnd(new HoldEnd
        {
            TGrid = TGrid.FromTotalGrid(16778881),
            XGrid = XGrid.FromTotalGrid(900)
        });
        var cloned = (Hold)GridClipboardGeometry.Clone(hold);
        Check(GridClipboardGeometry.Place(new[] { cloned }, XGrid.FromTotalGrid(250), TGrid.FromTotalGrid(500), PasteOption.None), "长Hold平移成功");
        Check(cloned.EndTGrid.TotalGrid - cloned.TGrid.TotalGrid == 16778781, "长Hold长度保留");
        Check(cloned.HoldEnd.XGrid.TotalGrid - cloned.XGrid.TotalGrid == 1300, "长Hold尾点偏移保留");
        Check(hold.TGrid.TotalGrid == 100 && hold.EndTGrid.TotalGrid == 16778881, "克隆平移不影响原Hold");

        a = Point(100, 10);
        b = Point(300, 20);
        Check(!GridClipboardGeometry.Place(new[] { a, b }, XGrid.Zero, TGrid.FromTotalGrid(-1), PasteOption.None), "整组拒绝负时间");
        Check(a.TGrid.TotalGrid == 100 && b.TGrid.TotalGrid == 300 && a.XGrid.TotalGrid == 10, "负时间拒绝无部分修改");
        Check(!GridClipboardGeometry.Place(new[] { a, b }, XGrid.Zero, TGrid.FromTotalGrid(int.MaxValue), PasteOption.None), "整组拒绝时间溢出");
        Check(a.TGrid.TotalGrid == 100 && b.TGrid.TotalGrid == 300, "时间溢出拒绝无部分修改");
        Check(GridClipboardGeometry.Place(new[] { a, b }, XGrid.Zero, TGrid.FromTotalGrid(1), PasteOption.None), "向前负偏移可成功");
        Check(a.TGrid.TotalGrid == 1 && b.TGrid.TotalGrid == 201, "向前移动保留时间间隔");

        a = Point(100, 0);
        b = Point(300, 1000);
        Check(!GridClipboardGeometry.Place(new[] { a, b }, XGrid.FromTotalGrid(int.MaxValue), TGrid.Zero, PasteOption.None), "整组拒绝X平移溢出");
        Check(a.TGrid.TotalGrid == 100 && b.XGrid.TotalGrid == 1000, "X平移溢出无部分修改");

        a = Point(100, int.MinValue);
        Check(!GridClipboardGeometry.Place(new[] { a }, XGrid.Zero, TGrid.Zero, PasteOption.XGridZeroMirror), "镜像int最小X拒绝溢出");
        Check(a.XGrid.TotalGrid == int.MinValue && a.TGrid.TotalGrid == 100, "X溢出拒绝无部分修改");
        foreach (var left in new[]
        {
            0,
            -3
        })
        {
            var oddLeft = Point(100, left);
            var oddRight = Point(300, left + 1);
            var oddPair = new[]
            {
                oddLeft,
                oddRight
            };
            var oddAnchor = GridClipboardGeometry.ReferencePoint(oddPair);
            Check(GridClipboardGeometry.Place(oddPair, oddAnchor.x, oddAnchor.t,
                PasteOption.SelectedRangeCenterXGridMirror), "奇数宽范围可原地镜像");
            Check(oddLeft.XGrid.TotalGrid == left + 1 && oddRight.XGrid.TotalGrid == left,
                "奇数宽正负X镜像精确交换边界，避免圆整中心偏移");
        }

        var speed = new Soflan
        {
            TGrid = TGrid.FromTotalGrid(100),
            SoflanGroup = 7,
            Speed = 2
        };
        speed.EndTGrid = TGrid.FromTotalGrid(400);
        var speedCopy = (Soflan)GridClipboardGeometry.Clone(speed);
        Check(speedCopy.SoflanGroup == 7 && speedCopy.EndTGrid.TotalGrid == 400, "Soflan克隆保留组号及尾点");
        return checks;
    }
}
