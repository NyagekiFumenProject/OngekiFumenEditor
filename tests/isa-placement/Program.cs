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
            CheckCommonPropertySelection();
            CheckMixedGroupSelection();
            CheckGroups();
            CheckEmptyGroupsRoundTrip();
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

    private static void CheckCommonPropertySelection()
    {
        var type = typeof(OngekiFumenEditor.Modules.FumenObjectPropertyBrowser.ViewModels.FumenObjectPropertyBrowserViewModel);
        var browser = (OngekiFumenEditor.Modules.FumenObjectPropertyBrowser.ViewModels.FumenObjectPropertyBrowserViewModel)
            System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(type);
        var selected = new System.Collections.Generic.HashSet<ISelectableObject>();
        type.GetField("selectedObjects", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(browser, selected);
        type.GetField("supportTypes", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(browser,
            new System.Collections.Generic.HashSet<Type> { typeof(int), typeof(bool), typeof(string) });
        type.GetField("<PropertyInfoWrappers>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(browser,
            new System.Collections.ObjectModel.ObservableCollection<OngekiFumenEditor.UI.Controls.ObjectInspector.UIGenerator.IObjectPropertyAccessProxy>());
        var first = new Soflan { SoflanGroup = 3 };
        var unrelated = new NoGroupSelectionFixture();
        browser.RefreshSelected(new ISelectableObject[] { first, unrelated }, null);
        Check(!browser.PropertyInfoWrappers.Any(x => x.PropertyInfo.Name == nameof(Soflan.SoflanGroup)), "混选无组属性物件不显示部分共有的组属性");
        Check(first.SoflanGroup == 3 && !unrelated.IsSelected, "混选属性刷新不改写不具有该属性的物件");
        browser.RefreshSelected(new ISelectableObject[] { first, new Soflan { SoflanGroup = 4 } }, null);
        Check(browser.PropertyInfoWrappers.Any(x => x.PropertyInfo.Name == nameof(Soflan.SoflanGroup)), "共同具有组属性时保留批量组属性");
        foreach (var wrapper in browser.PropertyInfoWrappers)
            wrapper.Clear();
    }

    private static void CheckMixedGroupSelection()
    {
        var fumen = new OngekiFumen();
        var add = SoflanGroupOperations.CreateAddAction(fumen, "批量组", out var group);
        add.Execute();
        var first = new Soflan { SoflanGroup = 0 };
        var second = new Soflan { SoflanGroup = group.SoflanGroupId };
        Check(OngekiFumenEditor.Modules.FumenObjectPropertyBrowser.MultiObjectsPropertyInfoWrapper.TryCreate(
            nameof(Soflan.SoflanGroup), typeof(int), new object[] { first, second }, out var multi), "创建真实多物件属性代理");
        var editorType = typeof(OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.FumenVisualEditorViewModel);
        var editor = (OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.FumenVisualEditorViewModel)
            System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(editorType);
        editorType.GetField("editorProjectData", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(editor,
            new OngekiFumenEditor.Modules.FumenVisualEditor.Models.EditorProjectDataModel { Fumen = fumen });
        var managerProperty = editorType.GetProperty("UndoRedoManager");
        var manager = DispatchProxy.Create(managerProperty.PropertyType, typeof(UndoCaptureProxy));
        managerProperty.SetValue(editor, manager);
        var browser = DispatchProxy.Create<OngekiFumenEditor.Modules.FumenObjectPropertyBrowser.IFumenObjectPropertyBrowser, BrowserProxy>();
        ((BrowserProxy)(object)browser).Editor = editor;
        var previousGet = Caliburn.Micro.IoC.GetInstance;
        Caliburn.Micro.IoC.GetInstance = (type, key) => type == typeof(OngekiFumenEditor.Modules.FumenObjectPropertyBrowser.IFumenObjectPropertyBrowser) ? browser : previousGet(type, key);
        var proxy = new OngekiFumenEditor.Modules.FumenObjectPropertyBrowser.UIGenerator.UndoableMultiObjectPropertyInfoWrapper(multi, editor);
        var vm = new OngekiFumenEditor.UI.Controls.ObjectInspector.ViewModels.SoflanGroupTypeUIViewModel(proxy);
        try
        {
            Check(ReferenceEquals(vm.SelectedGroup, System.Windows.DependencyProperty.UnsetValue), "混合组值保留 UnsetValue 不抛转换异常");
            vm.SelectedGroup = System.Windows.DependencyProperty.UnsetValue;
            vm.SelectedGroup = null;
            Check(((UndoCaptureProxy)manager).Action == null, "空选择及混合哨兵不生成改组动作");
            vm.SelectedGroup = group.SoflanGroupId;
            Check(first.SoflanGroup == group.SoflanGroupId && second.SoflanGroup == group.SoflanGroupId, "选择有效组通过真实批量代理更新所有物件");
            var action = ((UndoCaptureProxy)manager).Action;
            Check(action != null && Equals(vm.SelectedGroup, group.SoflanGroupId), "批量改组记录撤销动作并显示单值");
            action.Undo();
            Check(first.SoflanGroup == 0 && second.SoflanGroup == group.SoflanGroupId && ReferenceEquals(vm.SelectedGroup, System.Windows.DependencyProperty.UnsetValue), "撤销恢复各自原组及混合选择状态");
            action.Execute();
            Check(first.SoflanGroup == group.SoflanGroupId && second.SoflanGroup == group.SoflanGroupId, "重做恢复批量改组");
            var before = ((UndoCaptureProxy)manager).Action;
            vm.SelectedGroup = group.SoflanGroupId;
            vm.SelectedGroup = int.MaxValue;
            Check(ReferenceEquals(before, ((UndoCaptureProxy)manager).Action), "相同组与无效组不添加撤销动作");
            var readOnly = new ReadOnlyGroupFixture();
            Check(OngekiFumenEditor.Modules.FumenObjectPropertyBrowser.MultiObjectsPropertyInfoWrapper.TryCreate(
                nameof(ReadOnlyGroupFixture.SoflanGroup), typeof(int), new object[] { readOnly }, out var readOnlyMulti), "创建只读组代理");
            var readOnlyVm = new OngekiFumenEditor.UI.Controls.ObjectInspector.ViewModels.SoflanGroupTypeUIViewModel(readOnlyMulti);
            readOnlyVm.SelectedGroup = group.SoflanGroupId;
            Check(Equals(readOnlyVm.SelectedGroup, 0), "只读组选择不写入代理");
            readOnlyMulti.Clear();
        }
        finally
        {
            proxy.Clear();
            Caliburn.Micro.IoC.GetInstance = previousGet;
        }
    }

    private static void CheckEmptyGroupsRoundTrip()
    {
        var fumen = new OngekiFumen();
        var dirtyNotifications = 0;
        fumen.ObjectModifiedChanged += (_, e) => { if (e.PropertyName == nameof(OngekiFumen.IndividualSoflanAreaMap)) dirtyNotifications++; };
        var add = SoflanGroupOperations.CreateAddAction(fumen, "空组测试", out var item);
        Check(!fumen.IndividualSoflanAreaMap.GetSoflanGroups().Contains(item) && fumen.IndividualSoflanAreaMap.GetNextSoflanGroupId() == item.SoflanGroupId, "构造新增动作不挂树或保留编号");
        add.Execute();
        Check(fumen.IndividualSoflanAreaMap.GetSoflanGroups().Contains(item), "新增空组挂入有效组树");
        Check(dirtyNotifications == 1, "新增空组发出文档修改通知");
        var formatter = new OngekiFumenEditor.Parser.Ogkr.DefaultOngekiFumenFormatter();
        Caliburn.Micro.IoC.GetAllInstances = _ => Array.Empty<object>();
        var parser = new OngekiFumenEditor.Parser.Ogkr.DefaultOngekiFumenParser(new OngekiFumenEditor.Parser.Ogkr.ICommandParser[]
        { new OngekiFumenEditor.Parser.Ogkr.CommandParserImpl.SoflanGroupWrapItemCommandParser() });
        OngekiFumen Reload()
        {
            var bytes = formatter.SerializeAsync(fumen).GetAwaiter().GetResult();
            using var stream = new System.IO.MemoryStream(bytes);
            return parser.DeserializeAsync(stream).GetAwaiter().GetResult();
        }
        Check(Reload().IndividualSoflanAreaMap.GetSoflanGroups().Any(x => x.SoflanGroupId == item.SoflanGroupId && x.Parent.DisplayName == "空组测试"), "空组 SGWI 序列化解析后保留编号及名称");
        add.Undo();
        Check(!Reload().IndividualSoflanAreaMap.GetSoflanGroups().Any(x => x.SoflanGroupId == item.SoflanGroupId), "撤销新增后保存重载不复活缓存组");
        add.Execute();
        var delete = SoflanGroupOperations.CreateDeleteAction(fumen, item, null, false);
        delete.Execute();
        Check(!Reload().IndividualSoflanAreaMap.GetSoflanGroups().Any(x => x.SoflanGroupId == item.SoflanGroupId), "删除空组保存重载后不存在");
        delete.Undo();
        Check(Reload().IndividualSoflanAreaMap.GetSoflanGroups().Any(x => x.SoflanGroupId == item.SoflanGroupId), "撤销空组删除恢复持久化");
        Check(dirtyNotifications == 5, "组增删执行撤销均发出修改通知");
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
        using var commands = new DrawCommandListBuilder();
        var reusable = new SoflanGroupTintBuilder(commands, tint);
        var vertices = new[] { new OngekiFumenEditor.Kernel.Graphics.ILineDrawing.LineVertex(Vector2.Zero, original, default) };
        reusable.DrawLines(vertices, 1);
        reusable.Reset(commands, new Vector4(0, 0, 1, 1));
        reusable.DrawLines(vertices, 1);
        using var snapshot = commands.GetDrawCommandList();
        var lineCommands = snapshot.Commands.OfType<OngekiFumenEditor.Kernel.Graphics.DrawCommands.DefaultDrawCommands.DrawLinesCommand>().ToArray();
        Check(lineCommands.Length == 2, "复用滤镜保留两个绘制批次");
        Check(lineCommands[0].Points[0].Color == result, "复用暂存集合不覆盖此前命令的颜色");
        Check(lineCommands[1].Points[0].Color == reusable.TintColor(original), "后续批次使用新组色");
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

public class UndoCaptureProxy : DispatchProxy
{
    public Gemini.Modules.UndoRedo.IUndoableAction Action { get; private set; }
    protected override object Invoke(MethodInfo method, object[] args)
    {
        if (method.Name == "ExecuteAction")
        {
            Action = (Gemini.Modules.UndoRedo.IUndoableAction)args[0];
            Action.Execute();
        }
        return null;
    }
}

public class BrowserProxy : DispatchProxy
{
    public OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.FumenVisualEditorViewModel Editor { get; set; }
    protected override object Invoke(MethodInfo method, object[] args) => method.Name == "get_Editor" ? Editor : null;
}

public class ReadOnlyGroupFixture
{
    [OngekiFumenEditor.Base.Attributes.ObjectPropertyBrowserReadOnly]
    public int SoflanGroup { get; set; }
}

public class NoGroupSelectionFixture : ISelectableObject
{
    public bool IsSelected { get; set; }
}
