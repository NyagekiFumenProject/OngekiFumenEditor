using Caliburn.Micro;
using Gemini.Framework;
using Gemini.Framework.Services;
using Gemini.Modules.UndoRedo;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Modules.FumenVisualEditor.Base;
using OngekiFumenEditor.Modules.FumenVisualEditor.Kernel;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using OngekiFumenEditor.Properties;
using OngekiFumenEditor.Utils;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.Composition;
using static OngekiFumenEditor.Modules.FumenEditorRenderControlViewer.ViewModels.FumenEditorRenderControlViewerViewModel;
using System.Windows;
using System.Windows.Controls;
using System.Collections.Specialized;
using System.Linq;
using static OngekiFumenEditor.Base.Collections.SoflanList;
using OngekiFumenEditor.Modules.FumenObjectPropertyBrowser;
using OngekiFumenEditor.Base.Collections;
using OngekiFumenEditor.Base.EditorObjects;
using OngekiFumenEditor.UI.ListViewDragDropManager;

namespace OngekiFumenEditor.Modules.FumenSoflanGroupListViewer.ViewModels
{
    [Export(typeof(IFumenSoflanGroupListViewer))]
    public class FumenSoflanGroupListViewerViewModel : Tool, IFumenSoflanGroupListViewer
    {
        private OngekiFumen prevFumen = default;

        public FumenSoflanGroupListViewerViewModel()
        {
            DisplayName = Resources.SoflanGroupListViewer;

            IoC.Get<IEditorDocumentManager>().OnActivateEditorChanged += OnActivateEditorChanged;
            Editor = IoC.Get<IEditorDocumentManager>().CurrentActivatedEditor;
        }

        private void OnActivateEditorChanged(FumenVisualEditorViewModel @new, FumenVisualEditorViewModel old)
        {
            Editor = @new;
            this.RegisterOrUnregisterPropertyChangeEvent(old, @new, OnEditorPropertyChanged);
        }

        private void OnEditorPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(FumenVisualEditorViewModel.Fumen))
            {
                RebuildItemGroupRoot();
                RegisterFumenSoflanListMapEvent();
            }
        }

        private void RegisterFumenSoflanListMapEvent()
        {
            if (prevFumen != null)
            {
                prevFumen.IndividualSoflanAreaMap.PropertyChanged -= IndividualSoflanAreaMap_PropertyChanged;
                prevFumen = default;
            }

            if (Editor?.Fumen is OngekiFumen curFumen)
            {
                curFumen.IndividualSoflanAreaMap.PropertyChanged += IndividualSoflanAreaMap_PropertyChanged; ;
                prevFumen = curFumen;
            }
        }

        private void IndividualSoflanAreaMap_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (nameof(IndividualSoflanAreaListMap.SoflanGroupWrapItemGroupRoot) == e.PropertyName)
            {
                RebuildItemGroupRoot();
            }
        }

        public override PaneLocation PreferredLocation => PaneLocation.Bottom;

        private bool isShowPreviewModeSoflanPositionList = true;
        public bool IsShowPreviewModeSoflanPositionList
        {
            get => isShowPreviewModeSoflanPositionList;
            set => Set(ref isShowPreviewModeSoflanPositionList, value);
        }

        private FumenVisualEditorViewModel editor;
        private ListViewDragDropManager<SoflanGroupDisplayItemListViewBase> listDragManager;

        private SoflanGroupWrapItemGroup displaySoflanGroupItemGroupRoot;

        public IEnumerable<SoflanPoint> DisplaySoflanPointList
        {
            get
            {
                if (CurrentSelectedSoflanGroupWrapItem is null)
                    return Enumerable.Empty<SoflanPoint>();
                if (Editor?.Fumen is not OngekiFumen fumen)
                    return Enumerable.Empty<SoflanPoint>();

                var soflanList = fumen.SoflansMap[CurrentSelectedSoflanGroupWrapItem.SoflanGroupId];
                var points = IsShowPreviewModeSoflanPositionList ? soflanList.GetCachedSoflanPositionList_PreviewMode(fumen.BpmList) : soflanList.GetCachedSoflanPositionList_DesignMode(fumen.BpmList);

                return points;
            }
        }

        private string createNewGroupName;
        public string CreateNewGroupName
        {
            get => createNewGroupName;
            set => Set(ref createNewGroupName, value);
        }

        public void CreateNewGroup()
        {
            if (string.IsNullOrWhiteSpace(CreateNewGroupName))
            {
                MessageBox.Show("Soflan group name cannot be empty.", Resources.SoflanGroupListViewer, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (DisplaySoflanGroupItemGroupRoot is null)
            {
                MessageBox.Show("Cannot create a Soflan group because no fumen is loaded.", Resources.SoflanGroupListViewer, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var group = new SoflanGroupWrapItemGroup()
            {
                DisplayName = CreateNewGroupName
            };

            var root = DisplaySoflanGroupItemGroupRoot;
            if (Editor is null)
            {
                root.Add(group);
            }
            else
            {
                Editor.UndoRedoManager.ExecuteAction(LambdaUndoAction.Create(Resources.NewItemGroup,
                    () => root.Add(group),
                    () => group.Parent?.Remove(group)));
            }

            CreateNewGroupName = string.Empty;
        }

        private SoflanGroupWrapItem currentSelectedSoflanGroupWrapItem;
        public SoflanGroupWrapItem CurrentSelectedSoflanGroupWrapItem
        {
            get => currentSelectedSoflanGroupWrapItem;
            set
            {
                Set(ref currentSelectedSoflanGroupWrapItem, value);
                NotifyOfPropertyChange(() => DisplaySoflanPointList);
            }
        }

        private SoflanGroupWrapItem currentSoflansDisplaySoflanGroupWrapItem;
        public SoflanGroupWrapItem CurrentSoflansDisplaySoflanGroupWrapItem
        {
            get => currentSoflansDisplaySoflanGroupWrapItem;
            set => Set(ref currentSoflansDisplaySoflanGroupWrapItem, value);
        }

        public SoflanGroupWrapItemGroup DisplaySoflanGroupItemGroupRoot
        {
            get => displaySoflanGroupItemGroupRoot;
            set => Set(ref displaySoflanGroupItemGroupRoot, value);
        }

        public FumenVisualEditorViewModel Editor
        {
            get
            {
                return editor;
            }
            set
            {
                Set(ref editor, value);
                RebuildItemGroupRoot();
                RegisterFumenSoflanListMapEvent();
            }
        }

        public void OnListLoaded(FrameworkElement list)
        {
            listDragManager = new(list as ListView);
            listDragManager.ShowDragAdorner = true;
            listDragManager.DragAdornerOpacity = 0.75f;

            listDragManager.ProcessDrop += ListDragManager_ProcessDrop;

            Log.LogDebug($"ListViewDragDropManager created.");
        }


        public void OnItemChecked_IsDisplaySoflanDesignMode(object dataContext)
        {
            if (dataContext is not SoflanGroupWrapItem item)
                return;
            if (!item.IsDisplaySoflanDesignMode)
                throw new Exception("IsDisplaySoflanDesignMode is false.");

            CurrentSoflansDisplaySoflanGroupWrapItem = item;
            Log.LogInfo($"CurrentSoflansDisplaySoflanGroupWrapItem changed: {CurrentSoflansDisplaySoflanGroupWrapItem}");
        }

        public void OnItemChecked(object dataContext)
        {
            if (dataContext is not SoflanGroupWrapItem item)
                return;
            if (!item.IsSelected)
                throw new Exception("IsSelected is false.");

            CurrentSelectedSoflanGroupWrapItem = item;
            Log.LogInfo($"CurrentSelectedSoflanGroupWrapItem changed: {CurrentSelectedSoflanGroupWrapItem}");
        }

        private void RebuildItemGroupRoot()
        {
            DisplaySoflanGroupItemGroupRoot = default;
            CurrentSelectedSoflanGroupWrapItem = default;
            CurrentSoflansDisplaySoflanGroupWrapItem = default;

            if (Editor?.Fumen is not OngekiFumen fumen)
                return;

            DisplaySoflanGroupItemGroupRoot = fumen.IndividualSoflanAreaMap.SoflanGroupWrapItemGroupRoot;
            IEnumerable<SoflanGroupDisplayItemListViewBase> visit(SoflanGroupDisplayItemListViewBase item)
            {
                yield return item;
                if (item is SoflanGroupWrapItemGroup group)
                {
                    foreach (var child in group.Children)
                    {
                        foreach (var subItem in visit(child))
                        {
                            yield return subItem;
                        }
                    }
                }
            }

            foreach (var item in visit(DisplaySoflanGroupItemGroupRoot).OfType<SoflanGroupWrapItem>())
            {
                if (item.IsSelected)
                    CurrentSelectedSoflanGroupWrapItem = item;
                if (item.IsDisplaySoflanDesignMode)
                    CurrentSoflansDisplaySoflanGroupWrapItem = item;
            }
        }

        public void OnItemDoubleClick(SoflanPoint item)
        {
            if (Editor is null)
                return;

            Editor.ScrollTo(item.TGrid);
        }

        private void ListDragManager_ProcessDrop(object sender, ProcessDropEventArgs<SoflanGroupDisplayItemListViewBase> e)
        {
            //only item can be moved.
            if (e.DataItem is not SoflanGroupWrapItem item)
                return;

            var placeTo = e.ItemsSource.ElementAtOrDefault(e.NewIndex);

            if (CreateMoveAction(item, placeTo) is not IUndoableAction action)
                return;

            if (Editor is null)
            {
                action.Execute();
                return;
            }

            Editor.UndoRedoManager.ExecuteAction(action);
        }

        /// <summary>
        /// 根据放置目标构造分组移动的 LambdaUndoAction；目标无效（自身/位置未变化）时返回 null。
        /// 位置语义：oldIndex / newIndex 都是"该项仍留在原父节点子列表里"时的下标。
        /// </summary>
        public static IUndoableAction CreateMoveAction(SoflanGroupWrapItem item, SoflanGroupDisplayItemListViewBase placeTo)
        {
            if (item?.Parent is not SoflanGroupWrapItemGroup oldParent)
                return null;

            var oldIndex = IndexOf(oldParent.Children, item);
            if (oldIndex < 0)
                return null;

            SoflanGroupWrapItemGroup newParent;
            int beforeIndex;

            switch (placeTo)
            {
                case SoflanGroupWrapItem placeItem when !ReferenceEquals(placeItem, item):
                    if (placeItem.Parent is not SoflanGroupWrapItemGroup placeParent)
                        return null;

                    newParent = placeParent;
                    beforeIndex = IndexOf(placeParent.Children, placeItem);
                    if (beforeIndex < 0)
                        return null;
                    break;
                case SoflanGroupWrapItemGroup placeGroup:
                    newParent = placeGroup;
                    beforeIndex = placeGroup.Children.Count;
                    break;
                default:
                    return null;
            }

            var sameParent = ReferenceEquals(oldParent, newParent);
            var newIndex = beforeIndex - (sameParent && oldIndex < beforeIndex ? 1 : 0);
            if (newIndex < 0)
                newIndex = 0;

            if (sameParent && newIndex == oldIndex)
                return null;

            return LambdaUndoAction.Create(Resources.SoflanGroupMove,
                () => MoveTo(item, newParent, newIndex),
                () => MoveTo(item, oldParent, oldIndex));
        }

        private static void MoveTo(SoflanGroupWrapItem item, SoflanGroupWrapItemGroup parent, int index)
        {
            item.Parent?.Remove(item);
            parent.Insert(index, item);
        }

        private static int IndexOf(IReadOnlyList<SoflanGroupDisplayItemListViewBase> list, SoflanGroupDisplayItemListViewBase item)
        {
            for (var i = 0; i < list.Count; i++)
            {
                if (ReferenceEquals(list[i], item))
                    return i;
            }

            return -1;
        }
    }
}
