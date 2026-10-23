using Caliburn.Micro;
using Gemini.Framework;
using Gemini.Framework.Services;
using Gemini.Modules.UndoRedo;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Modules.FumenObjectPropertyBrowser;
using OngekiFumenEditor.Modules.FumenSoflanGroupListViewer.Models;
using OngekiFumenEditor.Modules.FumenVisualEditor.Base;
using OngekiFumenEditor.Modules.FumenVisualEditor.Kernel;
using OngekiFumenEditor.Modules.FumenVisualEditor.Models;
using static OngekiFumenEditor.Base.Collections.SoflanList;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using OngekiFumenEditor.Properties;
using OngekiFumenEditor.Utils;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace OngekiFumenEditor.Modules.FumenSoflanGroupListViewer.ViewModels
{
    [Export(typeof(IFumenSoflanGroupListViewer))]
    public class FumenSoflanGroupListViewerViewModel : Tool, IFumenSoflanGroupListViewer
    {
        private readonly ObservableCollection<SoflanGroupTreeNodeViewModel> rootNodes = new();

        public override PaneLocation PreferredLocation => PaneLocation.Bottom;
        private readonly List<SoflanGroupTreeNodeViewModel> allNodes = new();
        private readonly ConcurrentDictionary<int, GroupDisplayState> groupDisplayStates = new();
        private readonly HashSet<int> reservedDeletedGroupIds = new();

        private OngekiFumen prevFumen;
        private SoflanGroupTree subscribedTree;
        private SoflanGroupTree rebuiltTree;

        public FumenSoflanGroupListViewerViewModel()
        {
            DisplayName = Resources.SoflanGroupListViewer;

            IoC.Get<IEditorDocumentManager>().OnActivateEditorChanged += OnActivateEditorChanged;
            Editor = IoC.Get<IEditorDocumentManager>().CurrentActivatedEditor;
        }

        public ObservableCollection<SoflanGroupTreeNodeViewModel> RootNodes => rootNodes;

        private SoflanGroupTree GetTree() => Editor?.Fumen?.IndividualSoflanAreaMap.SoflanGroupTree;

        private void OnActivateEditorChanged(FumenVisualEditorViewModel @new, FumenVisualEditorViewModel old)
        {
            Editor = @new;
            this.RegisterOrUnregisterPropertyChangeEvent(old, @new, OnEditorPropertyChanged);
        }

        private void OnEditorPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(FumenVisualEditorViewModel.Fumen))
            {
                RebuildTree();
                RegisterFumenEvents();
                NotifyOfPropertyChange(nameof(IsEditorAvailable));
            }

            switch (e.PropertyName)
            {
                case nameof(FumenVisualEditorViewModel.IsIsaPlacementActive):
                case nameof(FumenVisualEditorViewModel.IsaPlacementPhase):
                case nameof(FumenVisualEditorViewModel.IsaPlacementTargetSoflanGroupId):
                    NotifyOfPropertyChange(nameof(IsIsaPlacementEnabled));
                    NotifyOfPropertyChange(nameof(IsaPlacementToolTip));
                    break;
            }
        }

        private void RegisterFumenEvents()
        {
            if (subscribedTree is not null)
            {
                subscribedTree.Changed -= OnGroupTreeChanged;
                subscribedTree = null;
            }

            if (prevFumen is not null)
            {
                prevFumen.SoflansMap.OnCollectionChangedEvent -= OnSoflansChanged;
                prevFumen = null;
            }

            if (Editor?.Fumen is OngekiFumen fumen)
            {
                subscribedTree = fumen.IndividualSoflanAreaMap.SoflanGroupTree;
                subscribedTree.Changed += OnGroupTreeChanged;
                fumen.SoflansMap.OnCollectionChangedEvent += OnSoflansChanged;
                prevFumen = fumen;
            }
        }

        private void OnGroupTreeChanged() => RebuildTreeOnDispatcher();

        private void OnSoflansChanged(CollectionChangeEventArgs e) => RebuildTreeOnDispatcher();

        private void RebuildTreeOnDispatcher()
        {
            var app = Application.Current;
            if (app is null || app.Dispatcher.CheckAccess())
                RebuildTree();
            else
                app.Dispatcher.BeginInvoke(new System.Action(RebuildTree));
        }

        public FumenVisualEditorViewModel Editor
        {
            get => field;
            set
            {
                if (!Set(ref field, value))
                    return;

                RebuildTree();
                RegisterFumenEvents();
                NotifyOfPropertyChange(nameof(IsEditorAvailable));
            }
        }

        public bool IsEditorAvailable => Editor?.Fumen is not null;

        private SoflanGroupTreeNodeViewModel selectedNode;
        public SoflanGroupTreeNodeViewModel SelectedNode
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

                NotifyOfPropertyChange(() => CurrentSelectedSoflanGroupId);
                NotifyOfPropertyChange(() => DisplaySoflanPointList);
                NotifyOfPropertyChange(() => SelectedTitle);
                NotifyOfPropertyChange(nameof(IsaPlacementToolTip));
            }
        }

        public int? CurrentSelectedSoflanGroupId => SelectedNode?.GroupId;

        public bool IsIsaPlacementEnabled
        {
            get => Editor?.IsIsaPlacementActive ?? false;
            set
            {
                if (value)
                {
                    if (Editor?.TryStartIsaPlacement(CurrentSelectedSoflanGroupId ?? 0) != true)
                        NotifyOfPropertyChange();
                }
                else
                {
                    Editor?.CancelIsaPlacement();
                }

                NotifyOfPropertyChange();
                NotifyOfPropertyChange(nameof(IsaPlacementToolTip));
            }
        }

        public string IsaPlacementToolTip
        {
            get
            {
                var groupName = SelectedGroupDisplayName;
                return Editor?.IsaPlacementPhase switch
                {
                    IsaPlacementPhase.WaitingFirstPoint => string.Format(Resources.IsaPlacementWaitingFirstPointTipText, groupName),
                    IsaPlacementPhase.WaitingSecondPoint => string.Format(Resources.IsaPlacementWaitingSecondPointTipText, groupName),
                    _ => string.Format(Resources.IsaPlacementTipText, groupName),
                };
            }
        }

        private string SelectedGroupDisplayName
        {
            get
            {
                var id = CurrentSelectedSoflanGroupId ?? 0;
                var group = Editor?.Fumen?.IndividualSoflanAreaMap.SoflanGroupTree.GetGroup(id);
                return group is null ? $"#{id}" : SoflanGroupDisplayName.FormatGroup(group);
            }
        }
        public string SelectedTitle => SelectedNode?.IsGroup == true
            ? string.Format(Resources.CurrentDisplaySelectedSoflanPointList, SelectedNode.DisplayNameWithId)
            : string.Empty;

        private int? drawnSoflanGroupId = 0;
        public int? DrawnSoflanGroupId => drawnSoflanGroupId;

        public bool IsGroupVisible(int soflanGroup, bool designMode)
        {
            if (!groupDisplayStates.TryGetValue(soflanGroup, out var state))
                return true;
            return designMode ? state.Design : state.Preview;
        }

        private bool isShowPreviewModeSoflanPositionList = true;
        public bool IsShowPreviewModeSoflanPositionList
        {
            get => isShowPreviewModeSoflanPositionList;
            set
            {
                if (Set(ref isShowPreviewModeSoflanPositionList, value))
                    NotifyOfPropertyChange(() => DisplaySoflanPointList);
            }
        }

        public IEnumerable<SoflanPoint> DisplaySoflanPointList
        {
            get
            {
                if (Editor?.Fumen is not OngekiFumen fumen
                    || SelectedNode?.IsGroup != true
                    || SelectedNode.GroupId is not int groupId)
                    return Array.Empty<SoflanPoint>();

                var soflanList = fumen.SoflansMap[groupId];
                return IsShowPreviewModeSoflanPositionList
                    ? soflanList.GetCachedSoflanPositionList_PreviewMode(fumen.BpmList)
                    : soflanList.GetCachedSoflanPositionList_DesignMode(fumen.BpmList);
            }
        }

        public void CreateNewGroup() => _ = CreateNewGroupAsync();

        private async Task CreateNewGroupAsync()
        {
            if (Editor?.Fumen is not OngekiFumen fumen)
                return;

            var dialog = new SoflanGroupNameDialogViewModel(Resources.NewItemGroup, string.Empty, null, allowEmpty: true);
            if (await IoC.Get<IWindowManager>().ShowDialogAsync(dialog) != true)
                return;

            var tree = fumen.IndividualSoflanAreaMap.SoflanGroupTree;
            var parent = GetTargetParent();
            var name = string.IsNullOrWhiteSpace(dialog.Name)
                ? Resources.SoflanGroupDefaultNewGroupName
                : dialog.Name.Trim();
            var id = GetNextSoflanGroupId();

            SoflanGroupNode node = null;
            var action = LambdaUndoAction.Create(Resources.NewItemGroup,
                () => node = tree.AddGroup(id, parent, name),
                () => { if (node is not null) { tree.Remove(node); node = null; } });

            ExecuteAction(action);
            RebuildTree();
            SelectGroup(id);
        }

        public int GetNextSoflanGroupId()
        {
            var tree = GetTree();
            if (tree is null)
                return 1;

            var fumen = Editor?.Fumen;
            if (fumen is null)
                return tree.GetNextGroupId(reservedDeletedGroupIds);

            return tree.GetNextGroupId(fumen.SoflansMap.Keys
                .Concat(fumen.IndividualSoflanAreaMap.Keys)
                .Concat(reservedDeletedGroupIds));
        }

        public SoflanGroupNode CreateGroupWithDefaultName(int id, SoflanGroupCategoryNode parent)
        {
            var tree = GetTree();
            if (tree is null || tree.ContainsGroup(id))
                return null;

            var name = Resources.SoflanGroupDefaultNewGroupName;
            SoflanGroupNode node = null;
            var action = LambdaUndoAction.Create(Resources.NewItemGroup,
                () => node = tree.AddGroup(id, parent ?? tree.Root, name),
                () =>
                {
                    if (node is not null)
                    {
                        tree.Remove(node);
                        node = null;
                    }
                });

            ExecuteAction(action);
            RebuildTree();
            SelectModel(node);
            return node;
        }

        public void CreateNewCategory() => _ = CreateNewCategoryAsync();

        private async Task CreateNewCategoryAsync()
        {
            if (Editor?.Fumen is not OngekiFumen fumen)
                return;

            var dialog = new SoflanGroupNameDialogViewModel(Resources.SoflanGroupNewCategory, string.Empty, null, allowEmpty: true);
            if (await IoC.Get<IWindowManager>().ShowDialogAsync(dialog) != true)
                return;

            var tree = fumen.IndividualSoflanAreaMap.SoflanGroupTree;
            // 组文件夹只能位于根级，不能嵌套。
            var parent = tree.Root;
            var name = string.IsNullOrWhiteSpace(dialog.Name)
                ? GetUniqueCategoryName(parent, Resources.SoflanGroupDefaultNewFolderName)
                : dialog.Name.Trim();

            if (parent.Children.OfType<SoflanGroupCategoryNode>().Any(x => x.Name == name))
            {
                MessageBox.Show(Resources.SoflanGroupNameExists, Resources.SoflanGroupNewCategory);
                return;
            }

            SoflanGroupCategoryNode category = null;
            var action = LambdaUndoAction.Create(Resources.SoflanGroupNewCategory,
                () => category = tree.AddCategory(name, parent),
                () => { if (category is not null) { tree.Remove(category); category = null; } });

            ExecuteAction(action);
            RebuildTree();
            SelectModel(category);
        }

        private static string GetUniqueCategoryName(SoflanGroupCategoryNode parent, string baseName)
        {
            var names = parent.Children.OfType<SoflanGroupCategoryNode>().Select(x => x.Name).ToHashSet();
            if (!names.Contains(baseName))
                return baseName;

            for (var i = 2; ; i++)
            {
                var candidate = $"{baseName} {i}";
                if (!names.Contains(candidate))
                    return candidate;
            }
        }

        public void RenameSelected() => _ = RenameSelectedAsync();

        private async Task RenameSelectedAsync()
        {
            var node = SelectedNode;
            if (node is null)
                return;

            var dialog = new SoflanGroupNameDialogViewModel(Resources.SoflanGroupRename, node.Model.Name, null, node.IsGroup);
            if (await IoC.Get<IWindowManager>().ShowDialogAsync(dialog) != true)
                return;

            var newName = dialog.Name?.Trim() ?? string.Empty;
            if (node.IsCategory && string.IsNullOrEmpty(newName))
            {
                MessageBox.Show(Resources.SoflanGroupNameRequired, Resources.SoflanGroupRename);
                return;
            }

            var oldName = node.Model.Name;
            if (oldName == newName)
                return;

            var tree = GetTree();
            if (tree is null)
                return;

            var action = LambdaUndoAction.Create(Resources.SoflanGroupRename,
                () => tree.Rename(node.Model, newName),
                () => tree.Rename(node.Model, oldName));
            ExecuteAction(action);
            RebuildTree();
            SelectModel(node.Model);
        }

        public void DeleteSelected() => _ = DeleteSelectedAsync();

        private async Task DeleteSelectedAsync()
        {
            var node = SelectedNode;
            if (node is null || Editor?.Fumen is not OngekiFumen fumen)
                return;

            if (node.IsDefaultGroup)
            {
                MessageBox.Show(Resources.SoflanGroupDefaultCannotDelete, Resources.SoflanGroupDelete);
                return;
            }

            if (node.IsCategory && node.Children.Count > 0)
            {
                MessageBox.Show(Resources.SoflanGroupDeleteHasChildren, Resources.SoflanGroupDelete);
                return;
            }

            if (node.IsGroup)
            {
                var groupId = node.GroupId!.Value;
                var references = SoflanGroupReferenceSnapshot.Capture(fumen, groupId);
                if (references.Count > 0)
                {
                    var alternatives = fumen.IndividualSoflanAreaMap.SoflanGroupTree
                        .EnumerateGroups()
                        .Where(x => x.Id != groupId);
                    var dialog = new SoflanGroupDeleteDialogViewModel(node.Group, references.Count, alternatives);
                    if (await IoC.Get<IWindowManager>().ShowDialogAsync(dialog) != true
                        || dialog.Choice == SoflanGroupDeleteChoice.Cancel)
                        return;

                    if (dialog.Choice == SoflanGroupDeleteChoice.Transfer)
                    {
                        if (dialog.SelectedAlternative is not { } alternative)
                            return;
                        DeleteGroupWithTransfer(fumen, node, references, alternative.Id);
                    }
                    else
                    {
                        DeleteGroupWithReferences(fumen, node, references);
                    }
                    return;
                }
            }

            DeleteEmptyNode(fumen, node);
        }

        private void DeleteEmptyNode(OngekiFumen fumen, SoflanGroupTreeNodeViewModel node)
        {
            var tree = fumen.IndividualSoflanAreaMap.SoflanGroupTree;
            var model = node.Model;
            var parent = model.Parent;
            var index = tree.IndexOf(model);
            var groupId = node.GroupId;

            var action = LambdaUndoAction.Create(Resources.SoflanGroupDelete,
                () =>
                {
                    tree.Remove(model);
                    if (groupId is int id)
                        reservedDeletedGroupIds.Add(id);
                    RebuildTree();
                    SelectModel(parent);
                },
                () =>
                {
                    tree.InsertExisting(model, parent, index);
                    if (groupId is int id)
                        reservedDeletedGroupIds.Remove(id);
                    RebuildTree();
                    SelectModel(model);
                });

            ExecuteAction(action);
            if (groupId is int removedGroupId)
                Editor?.CancelIsaPlacementIfTargetGroupRemoved(removedGroupId);
        }

        private void DeleteGroupWithTransfer(
            OngekiFumen fumen,
            SoflanGroupTreeNodeViewModel node,
            SoflanGroupReferenceSnapshot references,
            int alternativeGroupId)
        {
            var tree = fumen.IndividualSoflanAreaMap.SoflanGroupTree;
            var model = node.Model;
            var parent = model.Parent;
            var index = tree.IndexOf(model);
            var groupId = node.GroupId!.Value;

            var action = LambdaUndoAction.Create(Resources.SoflanGroupTransferReferencesAndDeleteAction,
                () =>
                {
                    references.ApplyGroup(alternativeGroupId);
                    tree.Remove(model);
                    reservedDeletedGroupIds.Add(groupId);
                    RebuildTree();
                    SelectModel(parent);
                },
                () =>
                {
                    tree.InsertExisting(model, parent, index);
                    references.RestoreGroup();
                    reservedDeletedGroupIds.Remove(groupId);
                    RebuildTree();
                    SelectModel(model);
                });

            ExecuteAction(action);
            Editor?.CancelIsaPlacementIfTargetGroupRemoved(groupId);
        }

        private void DeleteGroupWithReferences(
            OngekiFumen fumen,
            SoflanGroupTreeNodeViewModel node,
            SoflanGroupReferenceSnapshot references)
        {
            var tree = fumen.IndividualSoflanAreaMap.SoflanGroupTree;
            var model = node.Model;
            var parent = model.Parent;
            var index = tree.IndexOf(model);
            var groupId = node.GroupId!.Value;

            var action = LambdaUndoAction.Create(Resources.SoflanGroupDeleteReferencesAndGroupAction,
                () =>
                {
                    references.RemoveFrom(fumen);
                    tree.Remove(model);
                    reservedDeletedGroupIds.Add(groupId);
                    RebuildTree();
                    SelectModel(parent);
                    RefreshPropertyBrowser();
                },
                () =>
                {
                    tree.InsertExisting(model, parent, index);
                    references.AddTo(fumen);
                    references.RestoreSelection();
                    reservedDeletedGroupIds.Remove(groupId);
                    RebuildTree();
                    SelectModel(model);
                    RefreshPropertyBrowser();
                });

            ExecuteAction(action);
            Editor?.CancelIsaPlacementIfTargetGroupRemoved(groupId);
        }

        private void RefreshPropertyBrowser()
        {
            if (Editor is not null)
                IoC.Get<IFumenObjectPropertyBrowser>().RefreshSelected(Editor);
        }

        public void MoveSelectedToRoot()
        {
            if (SelectedNode is not null)
                MoveNodeTo(SelectedNode, null);
        }

        public bool MoveNodeTo(SoflanGroupTreeNodeViewModel dragged, SoflanGroupTreeNodeViewModel target)
        {
            if (dragged?.Model is null || dragged.IsDefaultGroup || ReferenceEquals(dragged, target) || Editor?.Fumen is not OngekiFumen fumen)
                return false;

            var tree = fumen.IndividualSoflanAreaMap.SoflanGroupTree;
            var newParent = target is null
                ? tree.Root
                : target.IsCategory
                    ? target.Category
                    : target.Group?.Parent ?? tree.Root;

            if (!tree.CanMove(dragged.Model, newParent))
                return false;

            var oldParent = dragged.Model.Parent;
            var oldIndex = tree.IndexOf(dragged.Model);
            var newIndex = newParent.Children.Count;
            if (ReferenceEquals(oldParent, newParent) && oldIndex == newIndex - 1)
                return false;

            var action = LambdaUndoAction.Create(Resources.SoflanGroupMove,
                () => tree.Move(dragged.Model, newParent, newIndex),
                () => tree.Move(dragged.Model, oldParent, oldIndex));
            ExecuteAction(action);
            RebuildTree();
            SelectModel(dragged.Model);
            return true;
        }

        public void OnSelectedNodeChanged(SoflanGroupTreeNodeViewModel node) => SelectedNode = node;

        public void OnItemDoubleClick(object item)
        {
            if (Editor is null || item is not SoflanPoint point)
                return;
            Editor.ScrollTo(point.TGrid);
        }

        private SoflanGroupCategoryNode GetTargetParent()
        {
            var tree = GetTree();
            if (tree is null)
                return null;

            return SelectedNode?.Model switch
            {
                SoflanGroupCategoryNode category => category,
                SoflanGroupNode group => group.Parent ?? tree.Root,
                _ => tree.Root
            };
        }

        private void SelectGroup(int id)
        {
            var node = allNodes.FirstOrDefault(x => x.GroupId == id);
            if (node is not null)
                SelectedNode = node;
        }

        private void SelectModel(SoflanGroupTreeNode model)
        {
            if (model is null)
                return;
            var node = allNodes.FirstOrDefault(x => ReferenceEquals(x.Model, model));
            if (node is not null)
                SelectedNode = node;
        }

        private void ExecuteAction(IUndoableAction action)
        {
            if (Editor is null)
                action.Execute();
            else
                Editor.UndoRedoManager.ExecuteAction(action);
        }

        public void SetGroupVisible(SoflanGroupTreeNodeViewModel node)
        {
            if (node.GroupId is not int id)
                return;

            var state = groupDisplayStates.TryGetValue(id, out var current) ? current : new GroupDisplayState(true, true, false);
            groupDisplayStates[id] = new GroupDisplayState(node.VisibleInDesign, node.VisibleInPreview, state.Draw);
        }

        public void SetDrawnGroup(SoflanGroupTreeNodeViewModel node)
        {
            if (node.GroupId is not int id)
                return;

            foreach (var item in allNodes.Where(x => x.IsGroup))
            {
                var draw = item.GroupId == id;
                item.SetDrawnSilent(draw);
                var state = groupDisplayStates.TryGetValue(item.GroupId!.Value, out var current)
                    ? current
                    : new GroupDisplayState(true, true, false);
                groupDisplayStates[item.GroupId.Value] = new GroupDisplayState(state.Design, state.Preview, draw);
            }

            drawnSoflanGroupId = id;
            NotifyOfPropertyChange(() => DrawnSoflanGroupId);
        }

        public void ClearDrawnGroup(SoflanGroupTreeNodeViewModel node)
        {
            if (node.GroupId is not int id || drawnSoflanGroupId != id)
                return;

            var state = groupDisplayStates.TryGetValue(id, out var current)
                ? current
                : new GroupDisplayState(true, true, false);
            groupDisplayStates[id] = new GroupDisplayState(state.Design, state.Preview, false);
            drawnSoflanGroupId = null;
            NotifyOfPropertyChange(() => DrawnSoflanGroupId);
        }

        private void RebuildTree()
        {
            var tree = GetTree();
            if (!ReferenceEquals(rebuiltTree, tree))
            {
                rebuiltTree = tree;
                reservedDeletedGroupIds.Clear();
                groupDisplayStates.Clear();
                drawnSoflanGroupId = 0;
            }

            var oldSelectedModel = SelectedNode?.Model;
            var oldSelectedId = SelectedNode?.GroupId;

            allNodes.Clear();
            rootNodes.Clear();
            SelectedNode = null;

            if (tree is null)
            {
                NotifyOfPropertyChange(() => DisplaySoflanPointList);
                return;
            }

            SoflanGroupTreeNodeViewModel Build(SoflanGroupTreeNode model, SoflanGroupTreeNodeViewModel parent)
            {
                var vm = new SoflanGroupTreeNodeViewModel(this, model)
                {
                    Parent = parent
                };
                allNodes.Add(vm);
                foreach (var child in model.Children)
                    vm.Children.Add(Build(child, vm));
                return vm;
            }

            foreach (var child in tree.Root.Children)
                rootNodes.Add(Build(child, null));

            var validGroupIds = allNodes.Where(x => x.GroupId is not null).Select(x => x.GroupId!.Value).ToHashSet();
            foreach (var staleId in groupDisplayStates.Keys.Where(x => !validGroupIds.Contains(x)).ToArray())
                groupDisplayStates.TryRemove(staleId, out _);

            if (drawnSoflanGroupId is int drawnId && !validGroupIds.Contains(drawnId))
                drawnSoflanGroupId = 0;

            foreach (var node in allNodes)
            {
                if (node.GroupId is not int id)
                    continue;

                if (!groupDisplayStates.TryGetValue(id, out var state))
                {
                    state = new GroupDisplayState(true, true, id == drawnSoflanGroupId);
                    groupDisplayStates[id] = state;
                }

                node.ApplyDisplayState(state.Design, state.Preview, id == drawnSoflanGroupId);
            }

            NotifyOfPropertyChange(() => DrawnSoflanGroupId);

            for (var i = allNodes.Count - 1; i >= 0; i--)
                if (allNodes[i].IsCategory)
                    allNodes[i].RefreshVisibilityFromChildren();

            var selected = oldSelectedModel is null
                ? null
                : allNodes.FirstOrDefault(x => ReferenceEquals(x.Model, oldSelectedModel));
            if (selected is null && oldSelectedId is int selectedId)
                selected = allNodes.FirstOrDefault(x => x.GroupId == selectedId);

            SelectedNode = selected ?? allNodes.FirstOrDefault(x => x.GroupId == 0);
            NotifyOfPropertyChange(() => DisplaySoflanPointList);
            NotifyOfPropertyChange(() => SelectedTitle);
            NotifyOfPropertyChange(nameof(IsaPlacementToolTip));
        }

        private readonly record struct GroupDisplayState(bool Design, bool Preview, bool Draw);
    }
}
