using System;
using System.Linq;
using System.Numerics;
using System.Reflection;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.EditorObjects;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Kernel.Graphics.DrawCommands;
using OngekiFumenEditor.Modules.FumenSoflanGroupListViewer;
using OngekiFumenEditor.Modules.FumenVisualEditor.Base;
using OngekiFumenEditor.Modules.FumenVisualEditor.Graphics.Drawing;

internal static class Program
{
    private static int checks;
    private static void Check(bool condition, string name)
    {
        if (!condition)
            throw new InvalidOperationException(name);
        checks++;
    }

    [STAThread]
    private static int Main()
    {
        try
        {
            CheckTwoPointPlacement();
            CheckGroups();
            CheckTint();
            CheckCacheInvalidation();
            Console.WriteLine($"通过 {checks} 项 ISA 逻辑回归检查。");
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error);
            return 1;
        }
    }

    private static void CheckTwoPointPlacement()
    {
        var placement = new TwoPointPlacement<int>();
        Check(!placement.TryComplete(1, out _), "未激活时不能放置");
        placement.Toggle();
        Check(!placement.TryComplete(3, out _) && placement.HasFirstPoint, "第一点仅暂存");
        placement.Cancel();
        Check(!placement.IsActive && !placement.HasFirstPoint, "取消清空第一点");
        placement.Toggle();
        placement.TryComplete(7, out _);
        Check(placement.TryComplete(2, out var first) && first == 7, "完成返回本轮第一点");
        Check(!placement.IsActive && !placement.HasFirstPoint, "完成清空状态");
        placement.Toggle();
        placement.TryComplete(9, out _);
        placement.Toggle();
        Check(!placement.IsActive && !placement.HasFirstPoint, "再次切换取消放置");
    }

    private static void CheckGroups()
    {
        var fumen = new OngekiFumen();
        fumen.Setup();
        var map = fumen.IndividualSoflanAreaMap;
        var parent = new SoflanGroupWrapItemGroup
        {
            DisplayName = "测试组"
        };
        map.SoflanGroupWrapItemGroupRoot.Add(parent);
        var item = map.TryGetOrCreateSoflanGroupWrapItem(1, out _);
        parent.Add(item);
        var subgroup = new SoflanGroupWrapItemGroup
        {
            DisplayName = "子组"
        };
        parent.Add(subgroup);
        var replacement = map.GetSoflanGroups().Single(x => x.SoflanGroupId == 0);
        Check(item.SelectionDisplayName.Contains("1"), "选择器显示有效组编号");
        Check(map.GetNextSoflanGroupId(new[] { 2 }) == 3, "新组避开缓存及物件保留编号");
        var area = new IndividualSoflanArea
        {
            SoflanGroup = 1
        };
        var soflan = new Soflan
        {
            SoflanGroup = 1
        };
        fumen.AddObject(area);
        fumen.AddObject(soflan);
        var transfer = SoflanGroupOperations.CreateDeleteAction(fumen, item, replacement, false);
        transfer.Execute();
        Check(area.SoflanGroup == 0 && soflan.SoflanGroup == 0, "删除组同时转移 ISA 与变速引用");
        Check(!map.GetSoflanGroups().Contains(item), "删除组不可从下拉选择");
        Check(map.GetNextSoflanGroupId() == 2, "删除组仍保留编号供撤销历史使用");
        transfer.Undo();
        Check(parent.Children[0] == item && parent.Children[1] == subgroup, "撤销删除恢复位于子组之前的原始顺序");
        Check(area.SoflanGroup == 1 && soflan.SoflanGroup == 1 && map.GetSoflanGroups().Contains(item), "撤销恢复组及引用");
        var remove = SoflanGroupOperations.CreateDeleteAction(fumen, item, null, true);
        remove.Execute();
        Check(!map.Values.SelectMany(x => x).Contains(area) && !fumen.SoflansMap.Values.SelectMany(x => x).Contains(soflan), "删除全部引用");
        remove.Undo();
        Check(map.Values.SelectMany(x => x).Contains(area) && fumen.SoflansMap.Values.SelectMany(x => x).Contains(soflan), "撤销恢复全部引用");
        var rejected = false;
        try
        {
            SoflanGroupOperations.CreateDeleteAction(fumen, item, null, false);
        }
        catch (InvalidOperationException)
        {
            rejected = true;
        }

        Check(rejected, "有引用时拒绝无替代组的删除");
    }

    private static void CheckCacheInvalidation()
    {
        var fumen = new OngekiFumen();
        using var invalidation = new SoflanGroupCacheInvalidation();
        invalidation.Attach(fumen);
        var rebuilds = 0;
        void Refresh() => invalidation.RefreshIfNeeded(() => rebuilds++);
        Refresh();
        Refresh();
        Check(rebuilds == 1, "未修改的连续帧仅重建一次");
        var area = new IndividualSoflanArea();
        fumen.AddObject(area);
        Refresh();
        Check(rebuilds == 2, "新建 ISA 使缓存失效");
        area.XGrid = XGrid.FromTotalGrid(100);
        area.EndIndicator.XGrid = XGrid.FromTotalGrid(200);
        area.EndIndicator.TGrid = TGrid.FromTotalGrid(200);
        area.SoflanGroup = 4;
        Refresh();
        Check(rebuilds == 3, "移动端点与改组在同一帧合并重建");
        area.EndIndicator.TGrid.Grid += 1;
        area.EndIndicator.XGrid.Unit += 1;
        Refresh();
        Check(rebuilds == 4, "直接修改端点 Unit/Grid 使缓存失效");
        area.IsSelected = true;
        Refresh();
        Check(rebuilds == 4, "仅选择不重建组归属");
        fumen.RemoveObject(area);
        Refresh();
        Check(rebuilds == 5, "删除 ISA 使缓存失效");
        fumen.AddObject(area);
        Refresh();
        Check(rebuilds == 6, "撤销删除重新加入 ISA 使缓存失效");
        area.SoflanGroup = 0;
        Refresh();
        Check(rebuilds == 7, "撤销改组使缓存失效");
        invalidation.Attach(new OngekiFumen());
        Refresh();
        Check(rebuilds == 8, "切换谱面重建缓存");
        area.XGrid = XGrid.FromTotalGrid(101);
        Refresh();
        Check(rebuilds == 8, "旧谱面事件已解除订阅");
    }

    private static void CheckTint()
    {
        var inner = DispatchProxy.Create<IDrawCommandListBuilder, NoOpBuilder>();
        var tint = new Vector4(1, 0, 0, 1);
        var filter = new SoflanGroupTintBuilder(inner, tint);
        var original = new Vector4(0, 1, 1, 0.35f);
        var result = filter.TintColor(original);
        Check(Vector3.Distance(new(result.X, result.Y, result.Z), new(0.15f, 0.85f, 0.85f)) < 0.00001f, "仅混入百分之十五组色");
        Check(result.W == original.W, "组色不改变透明度");
        var nested = new SoflanGroupTintBuilder(filter, new Vector4(0, 0, 1, 1));
        nested.DrawCircle(Vector2.Zero, original, true, 1);
        Check(((NoOpBuilder)(object)inner).LastColor == nested.TintColor(original), "嵌套绘制不重复混色");
        filter.DrawHighlightBatchTexture(null, Array.Empty<OngekiFumenEditor.Kernel.Graphics.DrawCommands.DefaultDrawCommands.TextureInstance>());
    }
}

public class NoOpBuilder : DispatchProxy
{
    public Vector4 LastColor { get; private set; }

    protected override object Invoke(MethodInfo method, object[] args)
    {
        if (method.Name == nameof(IDrawCommandListBuilder.DrawCircle))
            LastColor = (Vector4)args[1];
        return method.ReturnType != typeof(void) && method.ReturnType.IsValueType
            ? Activator.CreateInstance(method.ReturnType) : null;
    }
}
