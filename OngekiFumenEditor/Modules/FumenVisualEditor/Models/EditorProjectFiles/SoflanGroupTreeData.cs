using OngekiFumenEditor.Modules.FumenSoflanGroupListViewer.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Models.EditorProjectFiles
{
    /// <summary>项目文件中的变速组树布局（组名、组文件夹、顺序）。</summary>
    public sealed class SoflanGroupTreeData
    {
        public List<SoflanGroupCategoryData> Categories { get; set; } = new();
        public List<SoflanGroupData> Groups { get; set; } = new();
    }

    public sealed class SoflanGroupCategoryData
    {
        public string Name { get; set; } = string.Empty;
        public int Order { get; set; }
    }

    public sealed class SoflanGroupData
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;

        /// <summary>根级组为 null/空；组文件夹只允许一层。</summary>
        public string ParentCategory { get; set; }
        public int Order { get; set; }
    }

    public static class SoflanGroupTreeProjectData
    {
        public static SoflanGroupTreeData Capture(SoflanGroupTree tree)
        {
            var data = new SoflanGroupTreeData();
            if (tree is null)
                return data;

            var rootOrder = 0;
            foreach (var child in tree.Root.Children)
            {
                switch (child)
                {
                    case SoflanGroupCategoryNode category:
                        data.Categories.Add(new SoflanGroupCategoryData
                        {
                            Name = category.Name,
                            Order = rootOrder
                        });
                        break;
                    case SoflanGroupNode group:
                        data.Groups.Add(new SoflanGroupData
                        {
                            Id = group.Id,
                            Name = group.Name,
                            ParentCategory = null,
                            Order = rootOrder
                        });
                        break;
                }

                rootOrder++;
            }

            foreach (var category in tree.Root.Children.OfType<SoflanGroupCategoryNode>())
            {
                var order = 0;
                foreach (var group in category.Children.OfType<SoflanGroupNode>())
                {
                    data.Groups.Add(new SoflanGroupData
                    {
                        Id = group.Id,
                        Name = group.Name,
                        ParentCategory = category.Name,
                        Order = order++
                    });
                }
            }

            return data;
        }

        public static void Apply(SoflanGroupTree tree, SoflanGroupTreeData data)
        {
            if (tree is null || data is null)
                return;

            tree.EnsureDefaultGroup();

            // 项目文件中的布局是权威：先清掉谱面文件遗留的旧分类，
            // 但把其中不属于项目布局的组保留在根级，避免丢组。
            foreach (var legacyCategory in tree.Root.Children.OfType<SoflanGroupCategoryNode>().ToList())
            {
                foreach (var group in legacyCategory.Children.OfType<SoflanGroupNode>().ToList())
                    tree.Move(group, tree.Root);
                tree.Remove(legacyCategory);
            }

            var categories = new Dictionary<string, SoflanGroupCategoryNode>(StringComparer.Ordinal);
            foreach (var categoryData in data.Categories ?? Enumerable.Empty<SoflanGroupCategoryData>())
            {
                var name = categoryData?.Name ?? string.Empty;
                categories[name] = tree.GetOrCreateCategory(new[] { name });
            }

            foreach (var groupData in data.Groups ?? Enumerable.Empty<SoflanGroupData>())
            {
                if (groupData is null)
                    continue;

                var group = tree.GetGroup(groupData.Id) ?? tree.GetOrCreateGroup(groupData.Id);
                group.Name = groupData.Name ?? string.Empty;

                var parent = tree.Root;
                if (!string.IsNullOrEmpty(groupData.ParentCategory))
                {
                    if (!categories.TryGetValue(groupData.ParentCategory, out parent))
                    {
                        parent = tree.GetOrCreateCategory(new[] { groupData.ParentCategory });
                        categories[groupData.ParentCategory] = parent;
                    }
                }

                if (!group.IsDefault && !ReferenceEquals(group.Parent, parent))
                    tree.Move(group, parent);
            }

            foreach (var pair in categories)
            {
                var desired = (data.Groups ?? Enumerable.Empty<SoflanGroupData>())
                    .Where(x => x is not null && x.ParentCategory == pair.Key)
                    .OrderBy(x => x.Order)
                    .Select(x => tree.GetGroup(x.Id))
                    .Where(x => x is not null)
                    .Distinct()
                    .ToList();
                ReorderChildren(tree, pair.Value, desired);
            }

            var rootItems = new List<(int Order, SoflanGroupTreeNode Node)>();
            foreach (var categoryData in data.Categories ?? Enumerable.Empty<SoflanGroupCategoryData>())
            {
                if (categoryData is not null && categories.TryGetValue(categoryData.Name ?? string.Empty, out var category))
                    rootItems.Add((categoryData.Order, category));
            }
            foreach (var groupData in (data.Groups ?? Enumerable.Empty<SoflanGroupData>())
                .Where(x => x is not null && string.IsNullOrEmpty(x.ParentCategory)))
            {
                var group = tree.GetGroup(groupData.Id);
                if (group is not null)
                    rootItems.Add((groupData.Order, group));
            }

            var defaultGroup = tree.GetGroup(0);
            var orderedRoot = rootItems
                .OrderBy(x => x.Order)
                .Select(x => x.Node)
                .Where(x => !ReferenceEquals(x, defaultGroup))
                .Distinct()
                .ToList();
            orderedRoot.AddRange(tree.Root.Children
                .Where(x => !ReferenceEquals(x, defaultGroup) && !orderedRoot.Contains(x)));

            for (var i = 0; i < orderedRoot.Count; i++)
                tree.Move(orderedRoot[i], tree.Root, i + 1);
        }

        private static void ReorderChildren(SoflanGroupTree tree, SoflanGroupCategoryNode parent, List<SoflanGroupNode> desired)
        {
            var ordered = desired.Distinct().ToList();
            ordered.AddRange(parent.Children
                .OfType<SoflanGroupNode>()
                .Where(x => !ordered.Contains(x)));

            for (var i = 0; i < ordered.Count; i++)
                tree.Move(ordered[i], parent, i);
        }
    }
}
