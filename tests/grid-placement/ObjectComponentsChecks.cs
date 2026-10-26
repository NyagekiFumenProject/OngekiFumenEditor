using System;
using System.Linq;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.EditorObjects.LaneCurve;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Base.OngekiObjects.Lane;

public static class ObjectComponentsChecks
{
    public static void Run(Action<bool, string> check)
    {
        var hold = new Hold
        {
            TGrid = TGrid.FromTotalGrid(100),
            XGrid = XGrid.FromTotalGrid(10)
        };
        var holdEnd = new HoldEnd
        {
            TGrid = TGrid.FromTotalGrid(500)
        };
        hold.SetHoldEnd(holdEnd);

        var holdComponents = hold.EnumerateSelfAndComponents().ToArray();
        check(holdComponents.Length == 2 && holdComponents.Contains(hold) && holdComponents.Contains(holdEnd),
            "Hold 组件枚举包含头与尾");
        check(ReferenceEquals(hold.GetTailObject(), holdEnd), "Hold 找到尾节点");
        check(ReferenceEquals(holdEnd.GetHeadObject(), hold), "HoldEnd 找回所属头");
        check(hold.GetHeadObject() == null, "Hold 头没有更上层的头");

        var emptyHold = new Hold
        {
            TGrid = TGrid.FromTotalGrid(100),
            XGrid = XGrid.FromTotalGrid(10)
        };
        var emptyComponents = emptyHold.EnumerateSelfAndComponents().ToArray();
        check(emptyComponents.Length == 1 && ReferenceEquals(emptyComponents[0], emptyHold),
            "无尾 Hold 只枚举自身");
        check(emptyHold.GetTailObject() == null, "无尾 Hold 返回空尾节点");

        var soflan = new Soflan
        {
            TGrid = TGrid.FromTotalGrid(200)
        };
        var soflanComponents = soflan.EnumerateSelfAndComponents().ToArray();
        check(soflanComponents.Length == 2 && soflanComponents.Contains(soflan.EndIndicator),
            "Soflan 组件枚举包含尾节点");
        check(ReferenceEquals(soflan.GetTailObject(), soflan.EndIndicator), "Soflan 找到尾节点");
        check(ReferenceEquals(soflan.EndIndicator.GetHeadObject(), soflan), "Soflan 尾节点找回所属头");

        var block = new LaneBlockArea
        {
            TGrid = TGrid.FromTotalGrid(300)
        };
        check(block.EnumerateSelfAndComponents().Contains(block.EndIndicator), "LaneBlockArea 组件枚举包含尾节点");
        check(ReferenceEquals(block.GetTailObject(), block.EndIndicator), "LaneBlockArea 找到尾节点");
        check(ReferenceEquals(block.EndIndicator.GetHeadObject(), block), "LaneBlockArea 尾节点找回所属头");

        var area = new IndividualSoflanArea
        {
            TGrid = TGrid.FromTotalGrid(400),
            XGrid = XGrid.FromTotalGrid(20)
        };
        check(area.EnumerateSelfAndComponents().Contains(area.EndIndicator), "ISA 组件枚举包含尾节点");
        check(ReferenceEquals(area.GetTailObject(), area.EndIndicator), "ISA 找到尾节点");
        check(ReferenceEquals(area.EndIndicator.GetHeadObject(), area), "ISA 尾节点找回所属头");

        var lane = new LaneLeftStart
        {
            TGrid = TGrid.FromTotalGrid(600),
            XGrid = XGrid.FromTotalGrid(30)
        };
        var child = new LaneLeftNext
        {
            TGrid = TGrid.FromTotalGrid(900),
            XGrid = XGrid.FromTotalGrid(60)
        };
        var control = new LaneCurvePathControlObject
        {
            TGrid = TGrid.FromTotalGrid(750),
            XGrid = XGrid.FromTotalGrid(45)
        };
        child.AddControlObject(control);
        lane.AddChildObject(child);

        var laneComponents = lane.EnumerateSelfAndComponents().ToArray();
        check(laneComponents.Length == 3 &&
              laneComponents.Contains(lane) &&
              laneComponents.Contains(child) &&
              laneComponents.Contains(control),
            "轨道组件枚举包含起点、子节点与控制点");
        check(ReferenceEquals(child.GetLaneStartObject(), lane), "轨道子节点找回起点");
        check(ReferenceEquals(control.GetLaneStartObject(), lane), "曲线控制点找回轨道起点");

        var childComponents = child.EnumerateSelfAndComponents().ToArray();
        check(childComponents.Length == 1 && ReferenceEquals(childComponents[0], child),
            "子节点单独作为根时只枚举自身");
        check(child.GetTailObject() == null, "普通子节点没有尾节点");
    }
}
