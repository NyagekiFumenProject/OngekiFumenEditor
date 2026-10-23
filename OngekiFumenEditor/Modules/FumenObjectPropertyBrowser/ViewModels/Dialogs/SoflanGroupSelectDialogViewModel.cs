using Caliburn.Micro;
using OngekiFumenEditor.Modules.FumenSoflanGroupListViewer.Models;
using OngekiFumenEditor.Properties;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Modules.FumenObjectPropertyBrowser.ViewModels.Dialogs
{
    /// <summary>属性浏览器里点击 SoflanGroup 后弹出的组选择对话框（树状排列 + 快速新建）。</summary>
    public sealed class SoflanGroupSelectDialogViewModel : Screen
    {
        private readonly SoflanGroupTree tree;
        private readonly Func<int> nextGroupId;
        private readonly Func<int, SoflanGroupCategoryNode, SoflanGroupNode> createGroup;
        private readonly HashSet<int> pendingGroupIds = new();

        public SoflanGroupSelectDialogViewModel(
            SoflanGroupTree tree,
            int? currentGroupId,
            Func<int> nextGroupId = null,
            Func<int, SoflanGroupCategoryNode, SoflanGroupNode> createGroup = null)
        {
            DisplayName = Resources.SoflanGroupSelectDialogTitle;
            this.tree = tree;
            this.nextGroupId = nextGroupId ?? (() => tree?.GetNextGroupId() ?? 1);
            this.createGroup = createGroup ?? ((id, parent) =>
                tree?.AddGroup(id, parent ?? tree.Root, Resources.SoflanGroupDefaultNewGroupName));

            if (tree is not null)
            {
                foreach (var child in tree.Root.Children)
                    RootNodes.Add(BuildNode(child));
            }

            if (currentGroupId is int id)
                SelectedNode = FindGroupNode(id);
        }

        public ObservableCollection<SoflanGroupSelectNodeViewModel> RootNodes { get; } = new();

        private SoflanGroupSelectNodeViewModel selectedNode;
        public SoflanGroupSelectNodeViewModel SelectedNode
        {
            get => selectedNode;
            set
            {
                if (ReferenceEquals(selectedNode, value))
                    return;

                if (selectedNode is not null)
                    selectedNode.IsSelected = false;

                Set(ref selectedNode, value);

                if (selectedNode is not null)
                    selectedNode.IsSelected = true;

                NotifyOfPropertyChange(nameof(SelectedGroupId));
                NotifyOfPropertyChange(nameof(CanConfirm));
            }
        }

        public int? SelectedGroupId => SelectedNode?.GroupId;
        public bool CanConfirm => SelectedGroupId is not null;

        /// <summary>用默认组名快速新增一个组；确认时才真正写入组树。</summary>
        public void CreateQuickGroup()
        {
            if (tree is null)
                return;

            var parent = GetTargetParent();
            var id = nextGroupId();
            while ((tree.ContainsGroup(id) || pendingGroupIds.Contains(id)))
                id++;
            pendingGroupIds.Add(id);

            var model = new SoflanGroupNode(id, Resources.SoflanGroupDefaultNewGroupName);
            var node = new SoflanGroupSelectNodeViewModel(model)
            {
                IsPending = true,
                PendingParent = parent,
            };

            AddPendingNode(node, parent);
            SelectedNode = node;
        }

        public void OnSelectedNodeChanged(SoflanGroupSelectNodeViewModel node) => SelectedNode = node;

        public async void OnItemDoubleClick(SoflanGroupSelectNodeViewModel node)
        {
            if (node?.GroupId is null)
                return;

            SelectedNode = node;
            await ConfirmAndClose();
        }

        public async void Confirm() => await ConfirmAndClose();

        public async void Cancel() => await TryCloseAsync(false);

        /// <summary>校验当前选择；待创建组在此刻写入组树。返回 false 表示不应关闭对话框。</summary>
        public bool ConfirmSelection()
        {
            if (SelectedNode is null || SelectedGroupId is not int id)
                return false;

            if (SelectedNode.IsPending
                && createGroup(id, SelectedNode.PendingParent ?? tree?.Root) is null)
                return false;

            return true;
        }

        private async Task ConfirmAndClose()
        {
            if (!ConfirmSelection())
                return;

            await TryCloseAsync(true);
        }

        private SoflanGroupCategoryNode GetTargetParent()
        {
            if (SelectedNode is { IsCategory: true, Model: SoflanGroupCategoryNode category })
                return category;
            if (SelectedNode is { IsPending: true, PendingParent: { } pendingParent })
                return pendingParent;
            if (SelectedNode?.Model is SoflanGroupNode group)
                return group.Parent ?? tree?.Root;
            return tree?.Root;
        }

        private void AddPendingNode(SoflanGroupSelectNodeViewModel node, SoflanGroupCategoryNode parent)
        {
            var parentNode = FindCategoryNode(parent);
            if (parentNode is null || ReferenceEquals(parent, tree.Root))
            {
                RootNodes.Add(node);
                return;
            }

            parentNode.IsExpanded = true;
            parentNode.Children.Add(node);
        }

        private static SoflanGroupSelectNodeViewModel BuildNode(SoflanGroupTreeNode model)
        {
            var node = new SoflanGroupSelectNodeViewModel(model);
            foreach (var child in model.Children)
                node.Children.Add(BuildNode(child));
            return node;
        }

        private SoflanGroupSelectNodeViewModel FindGroupNode(int id)
        {
            return EnumerateNodes().FirstOrDefault(x => x.GroupId == id);
        }

        private SoflanGroupSelectNodeViewModel FindCategoryNode(SoflanGroupCategoryNode category)
        {
            if (category is null)
                return null;
            return EnumerateNodes().FirstOrDefault(x => ReferenceEquals(x.Model, category));
        }

        private System.Collections.Generic.IEnumerable<SoflanGroupSelectNodeViewModel> EnumerateNodes()
        {
            foreach (var root in RootNodes)
            {
                foreach (var node in Enumerate(root))
                    yield return node;
            }

            static System.Collections.Generic.IEnumerable<SoflanGroupSelectNodeViewModel> Enumerate(SoflanGroupSelectNodeViewModel node)
            {
                yield return node;
                foreach (var child in node.Children)
                    foreach (var sub in Enumerate(child))
                        yield return sub;
            }
        }
    }
}
