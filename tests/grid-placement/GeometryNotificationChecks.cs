using System;
using System.Collections.Generic;
using System.Linq;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects.Lane;

public static class GeometryNotificationChecks
{
    public static void Run(Action<bool, string> check)
    {
        var lane = new LaneLeftStart
        {
            TGrid = TGrid.FromTotalGrid(100),
            XGrid = XGrid.FromTotalGrid(10)
        };
        var child = new LaneLeftNext
        {
            TGrid = TGrid.FromTotalGrid(200),
            XGrid = XGrid.FromTotalGrid(20)
        };
        lane.AddChildObject(child);

        var laneNames = new List<string>();
        lane.PropertyChanged += (_, e) => laneNames.Add(e.PropertyName);

        lane.TGrid.Unit = 2;
        check(laneNames.Contains("TGrid"), "T 分量变化对外通知为 TGrid");
        check(!laneNames.Contains("Unit"), "T 分量变化不再泄漏 Unit 通知");

        laneNames.Clear();
        lane.XGrid.Grid = 100;
        check(laneNames.Contains("XGrid"), "X 分量变化对外通知为 XGrid");
        check(!laneNames.Contains("Grid"), "X 分量变化不再泄漏 Grid 通知");

        var childNames = new List<string>();
        child.PropertyChanged += (_, e) => childNames.Add(e.PropertyName);
        child.TGrid.Unit = 3;
        check(childNames.Contains("TGrid"), "子节点 T 分量变化通知为 TGrid");
        check(!childNames.Contains("Unit"), "子节点 T 分量变化不再泄漏 Unit 通知");
    }
}
