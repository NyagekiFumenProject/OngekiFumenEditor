using System;
using System.Collections.Generic;
using System.Linq;

namespace OngekiFumenEditor.Modules.FumenSoflanGroupListViewer.Models
{
    /// <summary>
    /// 变速组树的节点。Category（组文件夹）是根级显示分组，只能包含 Group，不能嵌套；
    /// Group 是绑定谱面对象的逻辑组（叶节点）。
    /// 模型不持有展开/选中/可见性等 UI 状态，这些由查看器的节点 ViewModel 维护。
    /// </summary>
    public abstract class SoflanGroupTreeNode
    {
        protected SoflanGroupTreeNode(string name)
        {
            Name = name ?? string.Empty;
        }

        private string name = string.Empty;
        public string Name
        {
            get => name;
            set => name = value ?? string.Empty;
        }

        public SoflanGroupCategoryNode Parent { get; internal set; }

        internal readonly List<SoflanGroupTreeNode> ChildrenInternal = new();
        public IReadOnlyList<SoflanGroupTreeNode> Children => ChildrenInternal;

        public bool IsCategory => this is SoflanGroupCategoryNode;
        public bool IsGroup => this is SoflanGroupNode;

        public override string ToString() => IsGroup
            ? $"Group #{((SoflanGroupNode)this).Id} ({Name})"
            : $"Category ({Name})";
    }

    public sealed class SoflanGroupCategoryNode : SoflanGroupTreeNode
    {
        public SoflanGroupCategoryNode(string name = "") : base(name)
        {
        }
    }

    public sealed class SoflanGroupNode : SoflanGroupTreeNode
    {
        public SoflanGroupNode(int id, string name = "") : base(name)
        {
            Id = id;
        }

        public int Id { get; }
        public bool IsDefault => Id == 0;

        /// <summary>
        /// 是否由文件中的显式布局（SGF/SGC/SGWI）定义。用于旧 SGWI 兼容：
        /// 新格式已经定义过的组不会被后续 SGWI 行重新挂载。
        /// </summary>
        public bool HasExplicitLayout { get; internal set; }

        public override string ToString() => $"Group #{Id} ({Name})";
    }

    /// <summary>
    /// 可持久化的变速组树。Root 是不可见容器；默认组 0 固定为 Root 的第一个 Group。
    /// </summary>
    public sealed class SoflanGroupTree
    {
        private readonly Dictionary<int, SoflanGroupNode> groups = new();

        public SoflanGroupCategoryNode Root { get; } = new("root");

        public event Action Changed;

        public SoflanGroupTree()
        {
            EnsureDefaultGroup();
        }

        public IReadOnlyCollection<SoflanGroupNode> Groups => groups.Values;

        public bool ContainsGroup(int id) => groups.ContainsKey(id);

        public SoflanGroupNode GetGroup(int id) => groups.TryGetValue(id, out var node) ? node : null;

        public SoflanGroupNode EnsureDefaultGroup()
        {
            var node = GetOrCreateGroup(0);
            if (node.Parent != Root)
            {
                RemoveFromParent(node);
                InsertNode(node, Root, 0);
            }
            else
            {
                var list = Root.ChildrenInternal;
                var index = list.IndexOf(node);
                if (index > 0)
                {
                    list.RemoveAt(index);
                    list.Insert(0, node);
                    NotifyChanged();
                }
            }

            return node;
        }

        /// <summary>确保组节点存在；不存在时挂在 Root 下。</summary>
        public SoflanGroupNode GetOrCreateGroup(int id)
        {
            if (groups.TryGetValue(id, out var node))
                return node;

            node = new SoflanGroupNode(id);
            groups[id] = node;
            InsertNode(node, Root, id == 0 ? 0 : -1);
            return node;
        }

        /// <summary>新建组（不用于解析已有组）。</summary>
        public SoflanGroupNode AddGroup(int id, SoflanGroupCategoryNode parent, string name, int index = -1, bool layoutDefined = true)
        {
            if (groups.ContainsKey(id))
                throw new InvalidOperationException($"Soflan group #{id} already exists.");

            var node = new SoflanGroupNode(id, name)
            {
                HasExplicitLayout = layoutDefined
            };
            groups[id] = node;
            InsertNode(node, parent ?? Root, index);
            return node;
        }

        public SoflanGroupCategoryNode AddCategory(string name, SoflanGroupCategoryNode parent, int index = -1)
        {
            parent ??= Root;
            if (!ReferenceEquals(parent, Root))
                throw new InvalidOperationException("Group folders cannot be nested.");
            if (parent.ChildrenInternal.OfType<SoflanGroupCategoryNode>().Any(x => x.Name == name))
                throw new InvalidOperationException($"Category \"{name}\" already exists.");

            var node = new SoflanGroupCategoryNode(name);
            InsertNode(node, Root, index);
            return node;
        }

        /// <summary>
        /// 按名称路径查找/创建 Category。组文件夹不支持嵌套：多段路径会合并成一个根级名称，
        /// 这样旧版嵌套文件仍能保留可读名称，而不会在树中生成嵌套文件夹。
        /// </summary>
        public SoflanGroupCategoryNode GetOrCreateCategory(IEnumerable<string> path)
        {
            var name = string.Join("/", path ?? Enumerable.Empty<string>());
            var existing = Root.ChildrenInternal
                .OfType<SoflanGroupCategoryNode>()
                .FirstOrDefault(x => x.Name == name);
            if (existing is not null)
                return existing;

            var node = new SoflanGroupCategoryNode(name);
            InsertNode(node, Root, -1);
            return node;
        }

        public SoflanGroupCategoryNode FindCategory(IEnumerable<string> path)
        {
            var name = string.Join("/", path ?? Enumerable.Empty<string>());
            return Root.ChildrenInternal
                .OfType<SoflanGroupCategoryNode>()
                .FirstOrDefault(x => x.Name == name);
        }

        public IReadOnlyList<string> GetPath(SoflanGroupCategoryNode category)
        {
            var path = new List<string>();
            for (var current = category; current is not null && current != Root; current = current.Parent)
                path.Add(current.Name);
            path.Reverse();
            return path;
        }

        public string GetPathText(SoflanGroupCategoryNode category) => string.Join("/", GetPath(category));

        /// <summary>深度优先枚举所有 Group（含默认组）。</summary>
        public IEnumerable<SoflanGroupNode> EnumerateGroups() => EnumerateGroups(Root);

        public IEnumerable<SoflanGroupNode> EnumerateGroups(SoflanGroupTreeNode node)
        {
            foreach (var child in node.ChildrenInternal)
            {
                if (child is SoflanGroupNode group)
                    yield return group;
                else
                    foreach (var sub in EnumerateGroups(child))
                        yield return sub;
            }
        }

        public int IndexOf(SoflanGroupTreeNode node) => node?.Parent?.ChildrenInternal.IndexOf(node) ?? -1;

        /// <summary>组文件夹只能放在根级；默认组不能移动。</summary>
        public bool CanMove(SoflanGroupTreeNode node, SoflanGroupCategoryNode newParent)
        {
            if (node is null || newParent is null)
                return false;
            if (node is SoflanGroupNode group && group.IsDefault)
                return false;
            if (node is SoflanGroupCategoryNode)
                return ReferenceEquals(newParent, Root);

            return true;
        }

        public bool Move(SoflanGroupTreeNode node, SoflanGroupCategoryNode newParent, int index = -1)
        {
            if (!CanMove(node, newParent))
                return false;

            RemoveFromParent(node);
            InsertNode(node, newParent, index);
            return true;
        }

        /// <summary>删除节点；Category 会连同其后代一起从字典移除。</summary>
        public bool Remove(SoflanGroupTreeNode node)
        {
            if (node is null || node.Parent is null)
                return false;
            if (node is SoflanGroupNode group && group.IsDefault)
                return false;

            RemoveFromParent(node);
            if (node is SoflanGroupNode single)
                groups.Remove(single.Id);
            else
                foreach (var sub in EnumerateGroups(node))
                    groups.Remove(sub.Id);

            NotifyChanged();
            return true;
        }

        /// <summary>撤销删除：把已存在的节点重新挂回树。</summary>
        public void InsertExisting(SoflanGroupTreeNode node, SoflanGroupCategoryNode parent, int index)
        {
            if (node is null)
                throw new ArgumentNullException(nameof(node));
            if (node.Parent is not null)
                throw new InvalidOperationException("Node already has a parent.");

            if (node is SoflanGroupNode group)
            {
                if (groups.ContainsKey(group.Id))
                    throw new InvalidOperationException($"Soflan group #{group.Id} already exists.");
                groups[group.Id] = group;
            }
            else
            {
                if (!ReferenceEquals(parent ?? Root, Root))
                    throw new InvalidOperationException("Group folders cannot be nested.");

                foreach (var sub in EnumerateGroups(node))
                {
                    if (groups.ContainsKey(sub.Id))
                        throw new InvalidOperationException($"Soflan group #{sub.Id} already exists.");
                    groups[sub.Id] = sub;
                }
            }

            InsertNode(node, parent ?? Root, index);
        }

        public int GetNextGroupId(IEnumerable<int> reserved = null)
        {
            var used = new HashSet<int>(groups.Keys);
            if (reserved is not null)
                used.UnionWith(reserved);

            var id = 1;
            while (used.Contains(id))
                id++;
            return id;
        }

        public bool Rename(SoflanGroupTreeNode node, string newName)
        {
            if (node is null)
                return false;

            newName ??= string.Empty;
            if (node is SoflanGroupCategoryNode)
            {
                var parent = node.Parent;
                if (parent is not null && parent.ChildrenInternal
                    .OfType<SoflanGroupCategoryNode>()
                    .Any(x => !ReferenceEquals(x, node) && x.Name == newName))
                    return false;
            }

            if (node.Name == newName)
                return false;

            node.Name = newName;
            NotifyChanged();
            return true;
        }

        public void MarkLayoutDefined(SoflanGroupNode node)
        {
            if (node is not null)
                node.HasExplicitLayout = true;
        }

        private void InsertNode(SoflanGroupTreeNode node, SoflanGroupCategoryNode parent, int index)
        {
            parent ??= Root;
            node.Parent = parent;
            var list = parent.ChildrenInternal;
            if (index < 0 || index > list.Count)
                list.Add(node);
            else
                list.Insert(index, node);

            NotifyChanged();
        }

        private void RemoveFromParent(SoflanGroupTreeNode node)
        {
            node.Parent?.ChildrenInternal.Remove(node);
            node.Parent = null;
        }

        public void NotifyChanged() => Changed?.Invoke();
    }
}
