using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.ComponentModel;
using System.Linq;
using System.Numerics;
using System.Text;
using OngekiFumenEditor.UI.Controls.ObjectInspector.UIGenerator;
using OngekiFumenEditor.UI.Controls.ObjectInspector.ViewModels;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Modules.FumenSoflanGroupListViewer.Models;
using OngekiFumenEditor.Modules.FumenVisualEditor.Base;
using OngekiFumenEditor.Modules.FumenVisualEditor.Kernel.EditorProjectFile;
using OngekiFumenEditor.Modules.FumenVisualEditor.Models;
using OngekiFumenEditor.Modules.FumenVisualEditor.Models.EditorProjectFiles;
using OngekiFumenEditor.Parser;
using OngekiFumenEditor.Parser.Ogkr;
using OngekiFumenEditor.Parser.Ogkr.CommandParserImpl;
using OngekiFumenEditor.Properties;

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
            Caliburn.Micro.IoC.GetAllInstances = type => type == typeof(IArgValueConverter)
                ? new object[]
                {
                    new ArgStringValueConverter(),
                    new ArgSingleValueConverter(),
                    new ArgDoubleValueConverter(),
                    new ArgBoolValueConverter(),
                    new ArgIntValueConverter(),
                    new ArgLongValueConverter()
                }
                : Array.Empty<object>();

            CheckDefaultGroup();
            CheckHierarchyAndMove();
            CheckCycleGuard();
            CheckRenameAndNextId();
            CheckRemoveAndRestore();
            CheckDeletedGroupIdReservation();
            CheckOnlySoflanGroup();
            CheckIsaPlacementSession();
            CheckSoflanGroupReferenceSnapshot();
            CheckDeleteDialogOptions();
            CheckSoflanGroupSelectDialog();
            CheckGroupColors();
            CheckLegacySgwi();
            CheckProjectTreeRoundTrip();
            CheckProjectSaveCapture();
            CheckProjectLoadApply();
            CheckNumericEditorFormatting();
            Console.WriteLine($"通过 {checks} 项 SoflanGroupTree 模型检查。");
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error);
            return 1;
        }
    }

    private static void CheckDefaultGroup()
    {
        var tree = new SoflanGroupTree();
        var defaultGroup = tree.GetGroup(0);
        Check(defaultGroup is not null && defaultGroup.IsDefault, "默认组 0 自动创建");
        Check(ReferenceEquals(tree.Root.Children[0], defaultGroup), "默认组固定在根第一个");
        Check(!tree.Remove(defaultGroup), "默认组不能删除");
        Check(!tree.Move(defaultGroup, tree.GetOrCreateCategory(new[] { "A" })), "默认组不能移动");
        Check(tree.GetOrCreateGroup(0) == defaultGroup, "重复 Ensure 不创建新对象");
    }

    private static void CheckHierarchyAndMove()
    {
        var tree = new SoflanGroupTree();
        var categoryA = tree.AddCategory("A", tree.Root);
        var categoryB = tree.AddCategory("B", tree.Root);
        var group1 = tree.AddGroup(1, categoryA, "one");
        var group2 = tree.AddGroup(2, categoryA, "two");
        var group3 = tree.AddGroup(3, tree.Root, "three");

        Check(tree.GetPath(categoryA).SequenceEqual(new[] { "A" }), "一级路径");
        var flattened = tree.GetOrCreateCategory(new[] { "A", "C" });
        Check(ReferenceEquals(flattened.Parent, tree.Root) && flattened.Name == "A/C", "多段路径扁平为根级组文件夹");
        Check(!tree.CanMove(categoryA, categoryB), "组文件夹不能嵌套");
        Check(!tree.Move(categoryA, categoryB), "Move 拒绝组文件夹嵌套");
        Check(ReferenceEquals(categoryA.Parent, tree.Root), "拒绝嵌套后父级不变");
        Check(tree.EnumerateGroups().Select(x => x.Id).SequenceEqual(new[] { 0, 1, 2, 3 }), "深度优先组顺序");

        Check(tree.Move(group1, tree.Root), "组移动到根");
        Check(group1.Parent == tree.Root, "组父级更新");
        Check(tree.Move(group2, categoryB, 0), "组移动到另一个文件");
        Check(ReferenceEquals(categoryB.Children[0], group2), "插入到指定索引");
        Check(tree.IndexOf(group2) == 0, "索引查询");
        Check(tree.Move(categoryA, tree.Root), "组文件夹在根级重排");
        Check(tree.GetPath(categoryA).SequenceEqual(new[] { "A" }), "重排后路径不变");
    }

    private static void CheckCycleGuard()
    {
        var tree = new SoflanGroupTree();
        var a = tree.AddCategory("A", tree.Root);
        var b = tree.AddCategory("B", tree.Root);
        Check(!tree.CanMove(a, a), "组文件夹不能移动到自身");
        Check(!tree.CanMove(a, b), "组文件夹不能移动到另一个组文件夹");
        Check(!tree.Move(a, b), "Move 拒绝组文件夹嵌套");
        Check(ReferenceEquals(a.Parent, tree.Root), "拒绝嵌套后父级不变");

        var nestedRejected = false;
        try { tree.AddCategory("C", a); } catch (InvalidOperationException) { nestedRejected = true; }
        Check(nestedRejected, "AddCategory 拒绝嵌套组文件夹");

        var g = tree.AddGroup(1, a, "one");
        Check(tree.CanMove(g, b), "组可以移动到组文件夹");
        Check(tree.Move(g, b), "组移动成功");
        Check(ReferenceEquals(g.Parent, b), "组父级更新为组文件夹");
    }

    private static void CheckRenameAndNextId()
    {
        var tree = new SoflanGroupTree();
        var a = tree.AddCategory("A", tree.Root);
        var b = tree.AddCategory("B", tree.Root);
        Check(!tree.Rename(b, "A"), "同级分类重名被拒绝");
        Check(tree.Rename(b, "C"), "分类重命名");
        Check(b.Name == "C", "分类名称更新");
        var g = tree.AddGroup(4, tree.Root, "four");
        Check(tree.Rename(g, "renamed"), "组可重命名");
        Check(g.Name == "renamed", "组名称更新");
        Check(tree.GetNextGroupId() == 1, "组 ID 从 1 开始");
        tree.AddGroup(1, tree.Root, "");
        Check(tree.GetNextGroupId() == 2, "避开已有组 ID");
        Check(tree.GetNextGroupId(new[] { 2, 3 }) == 5, "避开外部保留 ID");
    }

    private static ICommandParser[] CreateParsers(bool includeNewFormat)
    {
        var parsers = new List<ICommandParser>
        {
            new SoflanCommandParser(),
            new SoflanGroupWrapItemCommandParser()
        };
        if (includeNewFormat)
        {
            parsers.Add(new SoflanGroupCategoryCommandParser());
            parsers.Add(new SoflanGroupCommandParser());
        }
        return parsers.ToArray();
    }

    private static OngekiFumen Parse(string text, bool includeNewFormat)
    {
        var parser = new DefaultOngekiFumenParser(CreateParsers(includeNewFormat));
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(text));
        return parser.DeserializeAsync(stream).GetAwaiter().GetResult();
    }

    private static string Format(OngekiFumen fumen)
        => Encoding.UTF8.GetString(new DefaultOngekiFumenFormatter().SerializeAsync(fumen).GetAwaiter().GetResult());

    private static void CheckOnlySoflanGroup()
    {
        var fumen = new OngekiFumen();
        fumen.AddObject(new Soflan
        {
            SoflanGroup = 7,
            TGrid = new TGrid { Grid = 0, Unit = 0 },
            Speed = 1.25f
        });
        fumen.Setup();

        var tree = fumen.IndividualSoflanAreaMap.SoflanGroupTree;
        Check(tree.GetGroup(7) is not null, "仅 Soflan 引用的组进入组树");
        Check(tree.GetGroup(7).Parent == tree.Root, "仅 Soflan 组默认挂在根");

        var data = SoflanGroupTreeProjectData.Capture(tree);
        Check(data.Groups.Any(x => x.Id == 7), "仅 Soflan 组写入项目树数据");

        var another = new OngekiFumen();
        another.AddObject(new Soflan
        {
            SoflanGroup = 7,
            TGrid = new TGrid { Grid = 0, Unit = 0 },
            Speed = 1.25f
        });
        another.Setup();
        SoflanGroupTreeProjectData.Apply(another.IndividualSoflanAreaMap.SoflanGroupTree, data);
        Check(another.IndividualSoflanAreaMap.SoflanGroupTree.GetGroup(7) is not null, "仅 Soflan 组从项目树恢复");

        var text = Format(another);
        Check(!text.Contains("[SGWI]") && !text.Contains("[SGF]") && !text.Contains("[SGC]"), "谱面文件不再写组布局");
    }

    private static void CheckIsaPlacementSession()
    {
        var session = new IsaPlacementSession();
        Check(!session.IsActive, "ISA 放置初始未激活");
        Check(!session.TryTakePoint(new TGrid { Unit = 1, Grid = 0 }, new XGrid { Unit = 1, Grid = 0 }, out _), "未激活时不取点");

        session.Begin(3);
        Check(session.IsActive && session.Phase == IsaPlacementPhase.WaitingFirstPoint, "ISA 放置等待第一点");
        Check(!session.TryTakePoint(new TGrid { Unit = 10, Grid = 0 }, new XGrid { Unit = 10, Grid = 0 }, out _), "第一点不产生 ISA");
        Check(session.Phase == IsaPlacementPhase.WaitingSecondPoint, "第一点后等待第二点");

        Check(session.TryTakePoint(new TGrid { Unit = 2, Grid = 0 }, new XGrid { Unit = 2, Grid = 0 }, out var result), "第二点完成 ISA");
        Check(Math.Abs(result.StartTGrid.TotalUnit - 2) < 1e-6 && Math.Abs(result.EndTGrid.TotalUnit - 10) < 1e-6, "ISA 时间边界规范化");
        Check(Math.Abs(result.StartXGrid.TotalUnit - 2) < 1e-6 && Math.Abs(result.EndXGrid.TotalUnit - 10) < 1e-6, "ISA 水平边界规范化");
        Check(!session.IsActive, "ISA 完成后退出放置");

        session.Begin(0);
        session.TryTakePoint(new TGrid { Unit = 1, Grid = 0 }, new XGrid { Unit = 9, Grid = 0 }, out _);
        Check(session.TryTakePoint(new TGrid { Unit = 8, Grid = 0 }, new XGrid { Unit = 3, Grid = 0 }, out var reversed), "反序两点完成 ISA");
        Check(Math.Abs(reversed.StartTGrid.TotalUnit - 1) < 1e-6 && Math.Abs(reversed.EndTGrid.TotalUnit - 8) < 1e-6, "反序时间规范化");
        Check(Math.Abs(reversed.StartXGrid.TotalUnit - 3) < 1e-6 && Math.Abs(reversed.EndXGrid.TotalUnit - 9) < 1e-6, "反序水平规范化");

        session.Begin(1);
        session.Cancel();
        Check(!session.IsActive && session.Phase == IsaPlacementPhase.Inactive, "取消清空 ISA 放置状态");

        session.Continuous = true;
        session.Begin(5);
        session.TryTakePoint(new TGrid { Unit = 1, Grid = 0 }, new XGrid { Unit = 1, Grid = 0 }, out _);
        Check(session.TryTakePoint(new TGrid { Unit = 4, Grid = 0 }, new XGrid { Unit = 4, Grid = 0 }, out _), "连续模式完成第一个 ISA");
        Check(session.IsActive && session.Phase == IsaPlacementPhase.WaitingFirstPoint, "连续模式完成后继续等待第一点");
        Check(session.TargetSoflanGroupId == 5, "连续模式保持目标组");

        session.TryTakePoint(new TGrid { Unit = 6, Grid = 0 }, new XGrid { Unit = 6, Grid = 0 }, out _);
        Check(session.TryTakePoint(new TGrid { Unit = 8, Grid = 0 }, new XGrid { Unit = 8, Grid = 0 }, out var next), "连续模式可继续完成第二个 ISA");
        Check(Math.Abs(next.StartTGrid.TotalUnit - 6) < 1e-6 && Math.Abs(next.EndTGrid.TotalUnit - 8) < 1e-6, "连续模式第二组边界正确");

        session.Continuous = false;
        session.Cancel();
        Check(!session.IsActive, "关闭连续模式后取消退出");
        Check(session.Continuous == false, "连续模式开关状态独立于取消");
    }

    private static void CheckSoflanGroupReferenceSnapshot()
    {
        var fumen = new OngekiFumen();
        var soflan = new Soflan
        {
            SoflanGroup = 1,
            TGrid = new TGrid { Unit = 0, Grid = 0 },
            Speed = 1.25f,
        };
        var area = new IndividualSoflanArea
        {
            SoflanGroup = 1,
            TGrid = new TGrid { Unit = 0, Grid = 0 },
            XGrid = new XGrid { Unit = -2, Grid = 0 },
        };
        area.EndIndicator.TGrid = new TGrid { Unit = 4, Grid = 0 };
        area.EndIndicator.XGrid = new XGrid { Unit = 2, Grid = 0 };

        fumen.AddObject(soflan);
        fumen.AddObject(area);
        fumen.Setup();

        var snapshot = SoflanGroupReferenceSnapshot.Capture(fumen, 1);
        Check(snapshot.Count == 2, "引用快照包含 Soflan 与 ISA");
        Check(snapshot.Soflans.Count == 1 && snapshot.Areas.Count == 1, "引用快照按类型拆分");

        snapshot.ApplyGroup(2);
        Check(soflan.SoflanGroup == 2 && area.SoflanGroup == 2, "引用转移到新组");
        Check(!fumen.SoflansMap[1].Contains(soflan) && fumen.SoflansMap[2].Contains(soflan), "Soflan 随组迁移");
        Check(!fumen.IndividualSoflanAreaMap[1].Contains(area) && fumen.IndividualSoflanAreaMap[2].Contains(area), "ISA 随组迁移");

        snapshot.RestoreGroup();
        Check(soflan.SoflanGroup == 1 && area.SoflanGroup == 1, "引用恢复原组");
        Check(fumen.SoflansMap[1].Contains(soflan) && fumen.IndividualSoflanAreaMap[1].Contains(area), "恢复后回到原组列表");

        soflan.IsSelected = true;
        area.IsSelected = true;
        snapshot = SoflanGroupReferenceSnapshot.Capture(fumen, 1);
        snapshot.RemoveFrom(fumen);
        Check(!fumen.SoflansMap[1].Contains(soflan) && !fumen.IndividualSoflanAreaMap[1].Contains(area), "连同引用删除");
        snapshot.AddTo(fumen);
        snapshot.RestoreSelection();
        Check(fumen.SoflansMap[1].Contains(soflan) && fumen.IndividualSoflanAreaMap[1].Contains(area), "撤销恢复引用对象");
        Check(soflan.IsSelected && area.IsSelected, "撤销恢复引用选中状态");
    }

    private static void CheckDeletedGroupIdReservation()
    {
        var tree = new SoflanGroupTree();
        var deleted = tree.AddGroup(1, tree.Root, "one");
        tree.Remove(deleted);

        var reserved = new HashSet<int> { deleted.Id };
        var newId = tree.GetNextGroupId(reserved);
        Check(newId == 2, "待撤销删除的组编号不被新组复用");

        tree.AddGroup(newId, tree.Root, "new");
        tree.InsertExisting(deleted, tree.Root, 0);
        Check(tree.GetGroup(1) == deleted && tree.GetGroup(2) is not null, "保留编号后撤销删除不冲突");
    }

    private static void CheckDeleteDialogOptions()
    {
        var tree = new SoflanGroupTree();
        tree.AddGroup(1, tree.Root, "A");
        tree.AddGroup(2, tree.Root, "B");
        var dialog = new OngekiFumenEditor.Modules.FumenSoflanGroupListViewer.ViewModels.SoflanGroupDeleteDialogViewModel(
            tree.GetGroup(2), 3, new[] { tree.GetGroup(0), tree.GetGroup(1) });

        Check(dialog.CanTransfer, "删除对话框提供替代组");
        Check(dialog.GroupOptions.Count == 2, "删除对话框列出其他有效组");
        Check(dialog.GroupOptions.Any(x => x.Id == 0) && dialog.GroupOptions.Any(x => x.Id == 1), "替代组包含默认组与普通组");
        Check(!dialog.GroupOptions.Any(x => x.Id == 2), "替代组排除被删组");
    }

    private static void CheckSoflanGroupSelectDialog()
    {
        var tree = new SoflanGroupTree();
        var category = tree.AddCategory("A", tree.Root);
        tree.AddGroup(1, category, "one");
        tree.AddGroup(2, category, "two");

        var dialog = new OngekiFumenEditor.Modules.FumenObjectPropertyBrowser.ViewModels.Dialogs.SoflanGroupSelectDialogViewModel(tree, 2);
        Check(dialog.RootNodes.Count == 2, "组选择对话框含默认组与组文件夹");
        Check(dialog.SelectedNode?.GroupId == 2, "组选择对话框默认选中当前组");
        Check(dialog.CanConfirm, "组选择对话框当前组可确认");

        var categoryNode = dialog.RootNodes.First(x => x.IsCategory);
        Check(categoryNode.Children.Count == 2, "组选择对话框保留组文件夹层级");
        Check(categoryNode.Children[0].IdLabel == "#1" && categoryNode.Children[0].ColorBrush is not null, "组选择节点带组号与颜色");

        var mixed = new OngekiFumenEditor.Modules.FumenObjectPropertyBrowser.ViewModels.Dialogs.SoflanGroupSelectDialogViewModel(tree, null);
        Check(mixed.SelectedNode is null, "多选混合值不默认选中任何项");
        Check(!mixed.CanConfirm, "多选混合值不可直接确认");

        mixed.CreateQuickGroup();
        mixed.CreateQuickGroup();
        var pending = mixed.RootNodes.Where(x => x.IsPending).ToList();
        Check(pending.Count == 2, "连续快速新建生成两个待创建节点");
        Check(pending.Select(x => x.GroupId).Distinct().Count() == 2, "待创建节点分配不同组号");
        Check(pending.All(x => x.DisplayName == Resources.SoflanGroupDefaultNewGroupName), "待创建节点沿用默认组名");
        Check(pending.All(x => tree.GetGroup(x.GroupId!.Value) is null), "确认前不写入组树");

        var pendingId = mixed.SelectedGroupId!.Value;
        mixed.OnSelectedNodeChanged(mixed.RootNodes.First(x => x.IsPending && x.GroupId == pendingId));
        Check(mixed.ConfirmSelection(), "待创建组可以确认");
        Check(tree.GetGroup(pendingId) is not null && tree.GetGroup(pendingId).Name == Resources.SoflanGroupDefaultNewGroupName, "确认后按默认名写入组树");
    }

    private static void CheckGroupColors()
    {
        var colors = Enumerable.Range(0, 16).Select(SoflanGroupColor.Get).ToArray();
        for (var i = 0; i < colors.Length; i++)
        {
            var color = colors[i];
            var max = Math.Max(color.X, Math.Max(color.Y, color.Z));
            var min = Math.Min(color.X, Math.Min(color.Y, color.Z));
            Check(max >= 0.99f, $"颜色 #{i} 足够明亮");
            Check((max - min) / max >= 0.85f, $"颜色 #{i} 足够鲜艳");
            Check(color.W == 1f, $"颜色 #{i} 不透明");
        }

        for (var i = 0; i < colors.Length; i++)
        {
            for (var j = i + 1; j < colors.Length; j++)
            {
                var distance = Math.Abs(Hue(colors[i]) - Hue(colors[j]));
                distance = Math.Min(distance, 360f - distance);
                Check(distance >= 11.5f, $"颜色 #{i} 与 #{j} 色相不撞色");
            }
        }
    }

    private static float Hue(Vector4 color)
    {
        var max = Math.Max(color.X, Math.Max(color.Y, color.Z));
        var min = Math.Min(color.X, Math.Min(color.Y, color.Z));
        var delta = max - min;
        if (delta <= 1e-6f)
            return 0;

        float hue;
        if (max == color.X)
            hue = (color.Y - color.Z) / delta % 6f;
        else if (max == color.Y)
            hue = (color.Z - color.X) / delta + 2f;
        else
            hue = (color.X - color.Y) / delta + 4f;

        hue *= 60f;
        if (hue < 0)
            hue += 360f;
        return hue;
    }

    private static void CheckLegacySgwi()
    {
        const string text =
            "SFL\t0\t0\t1\t1\t2\n" +
            "SFL\t0\t10\t1\t1\t3\n" +
            "[SGWI]\tA\t2\t3\n" +
            "[SGWI]\tdefault\t0\t1\n";

        var fumen = Parse(text, false);
        var tree = fumen.IndividualSoflanAreaMap.SoflanGroupTree;
        Check(tree.GetGroup(2)?.Parent?.Name == "A", "旧 SGWI 把组 2 放入分类 A");
        Check(tree.GetGroup(3)?.Parent?.Name == "A", "旧 SGWI 把组 3 放入分类 A");
        Check(tree.GetGroup(1)?.Parent == tree.Root, "旧 SGWI default 组挂根");
        Check(tree.GetGroup(0)?.Parent == tree.Root, "旧文件保留默认组 0");
    }

    private static void CheckProjectTreeRoundTrip()
    {
        var fumen = new OngekiFumen();
        fumen.AddObject(new Soflan
        {
            SoflanGroup = 2,
            TGrid = new TGrid { Grid = 0, Unit = 0 },
            Speed = 1f
        });
        fumen.AddObject(new Soflan
        {
            SoflanGroup = 3,
            TGrid = new TGrid { Grid = 10, Unit = 0 },
            Speed = 1f
        });
        fumen.Setup();

        var tree = fumen.IndividualSoflanAreaMap.SoflanGroupTree;
        var categoryA = tree.AddCategory("A", tree.Root);
        var categoryB = tree.AddCategory("Cat/1", tree.Root);
        var group2 = tree.GetGroup(2);
        var group3 = tree.GetGroup(3);
        tree.Move(group2, categoryA);
        tree.Move(group3, categoryB);
        tree.Rename(group2, "主变速");
        tree.Rename(group3, "Lead/A");
        tree.AddGroup(4, tree.Root, "Extra");

        var data = SoflanGroupTreeProjectData.Capture(tree);

        var anotherFumen = new OngekiFumen();
        anotherFumen.AddObject(new Soflan
        {
            SoflanGroup = 2,
            TGrid = new TGrid { Grid = 0, Unit = 0 },
            Speed = 1f
        });
        anotherFumen.AddObject(new Soflan
        {
            SoflanGroup = 3,
            TGrid = new TGrid { Grid = 10, Unit = 0 },
            Speed = 1f
        });
        anotherFumen.Setup();

        // 谱面文件遗留的旧分类应被项目布局清掉；不属于项目布局的组保留在根级。
        var legacyTree = anotherFumen.IndividualSoflanAreaMap.SoflanGroupTree;
        var legacyCategory = legacyTree.AddCategory("Old", legacyTree.Root);
        var legacyGroup = legacyTree.GetOrCreateGroup(9);
        legacyTree.Move(legacyGroup, legacyCategory);

        SoflanGroupTreeProjectData.Apply(legacyTree, data);
        var reparsed = anotherFumen.IndividualSoflanAreaMap.SoflanGroupTree;
        Check(reparsed.FindCategory(new[] { "Old" }) is null, "项目布局清除谱面遗留分类");
        Check(reparsed.GetGroup(9)?.Parent == reparsed.Root, "项目布局保留未列出的组于根级");
        Check(reparsed.GetGroup(2)?.Parent?.Name == "A", "项目树恢复分类 A");
        Check(reparsed.GetGroup(2)?.Name == "主变速", "项目树恢复中文组名");
        Check(reparsed.GetGroup(3)?.Parent?.Name == "Cat/1", "项目树恢复含 / 的分类名");
        Check(reparsed.GetGroup(3)?.Name == "Lead/A", "项目树恢复含 / 的组名");
        Check(reparsed.GetGroup(4)?.Name == "Extra" && reparsed.GetGroup(4).Parent == reparsed.Root, "项目树恢复根级空组");
        Check(reparsed.FindCategory(new[] { "Cat/1" }) is not null, "项目树分类名恢复");

        var text = Format(fumen);
        Check(!text.Contains("[SGWI]") && !text.Contains("[SGF]") && !text.Contains("[SGC]"), "谱面文件不再写组布局");

        var proj = new EditorProjectDataModel
        {
            SoflanGroupTree = data
        };
        var manager = new EditorProjectFileManager();
        var path = Path.Combine(Path.GetTempPath(), $"soflan-group-{Guid.NewGuid():N}.nyagekiProj");
        try
        {
            manager.Save(path, proj).GetAwaiter().GetResult();
            var loaded = manager.Load(path).GetAwaiter().GetResult();
            Check(loaded.SoflanGroupTree is not null, "项目文件保存组树");
            Check(loaded.SoflanGroupTree.Categories.Any(x => x.Name == "Cat/1"), "项目 JSON 保留分类名");
            Check(loaded.SoflanGroupTree.Groups.Any(x => x.Id == 2 && x.Name == "主变速" && x.ParentCategory == "A"), "项目 JSON 保留组名与归属");
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    private static void CheckProjectSaveCapture()
    {
        var fumen = new OngekiFumen();
        fumen.AddObject(new Soflan
        {
            SoflanGroup = 5,
            TGrid = new TGrid { Grid = 0, Unit = 0 },
            Speed = 1f
        });
        fumen.Setup();

        var tree = fumen.IndividualSoflanAreaMap.SoflanGroupTree;
        var category = tree.AddCategory("ProjCat", tree.Root);
        var group = tree.GetGroup(5);
        tree.Move(group, category);
        tree.Rename(group, "Captured");

        var project = new EditorProjectDataModel
        {
            Fumen = fumen
        };
        var path = Path.Combine(Path.GetTempPath(), $"soflan-proj-{Guid.NewGuid():N}.nyagekiProj");
        try
        {
            var result = EditorProjectDataUtils.TrySaveProjFileAsync(path, project).GetAwaiter().GetResult();
            Check(result.IsSuccess, "项目保存成功");
            var loaded = new EditorProjectFileManager().Load(path).GetAwaiter().GetResult();
            Check(loaded.SoflanGroupTree is not null, "项目保存写出组树属性");
            Check(loaded.SoflanGroupTree.Categories.Any(x => x.Name == "ProjCat"), "项目保存捕获分类");
            Check(loaded.SoflanGroupTree.Groups.Any(x => x.Id == 5 && x.Name == "Captured" && x.ParentCategory == "ProjCat"), "项目保存捕获组名与归属");
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    private static void CheckProjectLoadApply()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"soflan-load-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        var previousGetInstance = Caliburn.Micro.IoC.GetInstance;
        try
        {
            var fumenPath = Path.Combine(tempDir, "fumen.ogkr");
            File.WriteAllText(fumenPath, "SFL\t0\t0\t1\t1\t2\n");

            var project = new EditorProjectDataModel
            {
                FumenFilePath = "fumen.ogkr",
                AudioFilePath = "audio.wav",
                SoflanGroupTree = new SoflanGroupTreeData
                {
                    Categories = { new SoflanGroupCategoryData { Name = "ProjLoad", Order = 1 } },
                    Groups =
                    {
                        new SoflanGroupData { Id = 0, Name = "", ParentCategory = null, Order = 0 },
                        new SoflanGroupData { Id = 2, Name = "Loaded", ParentCategory = "ProjLoad", Order = 0 }
                    }
                }
            };
            var projectPath = Path.Combine(tempDir, "test.nyagekiProj");
            new EditorProjectFileManager().Save(projectPath, project).GetAwaiter().GetResult();

            Caliburn.Micro.IoC.GetInstance = (type, key) => type == typeof(IFumenParserManager)
                ? new TestParserManager()
                : type == typeof(OngekiFumenEditor.Utils.Log)
                    ? new OngekiFumenEditor.Utils.Log(Array.Empty<OngekiFumenEditor.Utils.Logs.ILogOutput>())
                    : previousGetInstance(type, key);

            var loaded = EditorProjectDataUtils.TryLoadFromFileAsync(projectPath).GetAwaiter().GetResult();
            var tree = loaded.Fumen.IndividualSoflanAreaMap.SoflanGroupTree;
            Check(tree.GetGroup(2)?.Parent?.Name == "ProjLoad", "项目加载应用组文件夹");
            Check(tree.GetGroup(2)?.Name == "Loaded", "项目加载应用组名");
        }
        finally
        {
            Caliburn.Micro.IoC.GetInstance = previousGetInstance;
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    private sealed class TestParserManager : IFumenParserManager
    {
        public IFumenSerializable GetSerializer(string saveFilePath) => null;

        public IFumenDeserializable GetDeserializer(string loadFilePath)
            => new DefaultOngekiFumenParser(CreateParsers(true));

        public IEnumerable<(string desc, string[] fileFormat)> GetSerializerDescriptions()
            => Array.Empty<(string desc, string[] fileFormat)>();

        public IEnumerable<(string desc, string[] fileFormat)> GetDeserializerDescriptions()
            => Array.Empty<(string desc, string[] fileFormat)>();
    }

    private static void CheckNumericEditorFormatting()
    {
        var target = new NumericEditorTarget { Value = 1.2000000476837158 };
        var property = typeof(NumericEditorTarget).GetProperty(nameof(NumericEditorTarget.Value));
        var proxy = new FakePropertyProxy(property, target.Value);
        var vm = new BaseValueTypeUIViewModel(proxy);

        Check(Math.Abs(double.Parse(vm.ProxyValueText, CultureInfo.CurrentCulture) - 1.2) < 1e-9, "属性编辑器格式化 double 噪声");
        vm.ProxyValueText = "2.345678";
        Check(proxy.ProxyValue is double parsed && Math.Abs(parsed - 2.345678) < 1e-12, "属性编辑器解析 double");

        Check(new TGrid(1.200000047f, 0).ToString() == $"T[{1.200000047f.ToString("0.###", CultureInfo.CurrentCulture)},0]", "TGrid 显示格式化");
        Check(new XGrid(1.200000047f, 0).ToString() == $"X[{1.200000047f.ToString("0.###", CultureInfo.CurrentCulture)},0]", "XGrid 显示格式化");
    }

    private sealed class NumericEditorTarget
    {
        public double Value { get; set; }
    }

    private sealed class FakePropertyProxy : IObjectPropertyAccessProxy
    {
        public FakePropertyProxy(System.Reflection.PropertyInfo propertyInfo, object value)
        {
            PropertyInfo = propertyInfo;
            ProxyValue = value;
        }

        public System.Reflection.PropertyInfo PropertyInfo { get; }

        private object proxyValue;
        public object ProxyValue
        {
            get => proxyValue;
            set
            {
                proxyValue = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ProxyValue)));
            }
        }
        public string DisplayPropertyName => PropertyInfo.Name;
        public string DisplayPropertyTipText => string.Empty;
        public bool IsAllowSetNull => false;
        public bool IsReadOnly => false;
        public event PropertyChangedEventHandler PropertyChanged;
        public void Clear() { }
    }

    private static void CheckRemoveAndRestore()
    {
        var tree = new SoflanGroupTree();
        var category = tree.AddCategory("A", tree.Root);
        var group = tree.AddGroup(1, category, "one");
        var index = tree.IndexOf(category);

        Check(tree.Remove(category), "删除文件");
        Check(tree.GetGroup(1) is null, "文件内组从字典移除");
        Check(category.Parent is null, "删除后父级为空");

        tree.InsertExisting(category, tree.Root, index);
        Check(tree.GetGroup(1) == group && group.Parent == category, "撤销删除恢复文件内组");
        Check(ReferenceEquals(tree.Root.Children[index], category), "撤销删除恢复顺序");

        Check(tree.Remove(group), "删除组");
        Check(tree.GetGroup(1) is null, "组从字典移除");
        tree.InsertExisting(group, category, 0);
        Check(tree.GetGroup(1) == group && ReferenceEquals(category.Children[0], group), "撤销删除恢复组");
    }
}
