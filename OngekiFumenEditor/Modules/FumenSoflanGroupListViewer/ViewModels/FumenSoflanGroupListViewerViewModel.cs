using Caliburn.Micro;
using Gemini.Framework;
using Gemini.Framework.Services;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Modules.FumenSoflanGroupListViewer.Models;
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
using WPF.JoshSmith.ServiceProviders.UI;
using System.Windows.Controls;
using System.Collections.Specialized;
using System.Linq;

namespace OngekiFumenEditor.Modules.FumenSoflanGroupListViewer.ViewModels
{
    [Export(typeof(IFumenSoflanGroupListViewer))]
    public class FumenSoflanGroupListViewerViewModel : Tool, IFumenSoflanGroupListViewer
    {
        public FumenSoflanGroupListViewerViewModel()
        {
            DisplayName = "变速分组查看器";

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
            }
        }

        public override PaneLocation PreferredLocation => PaneLocation.Bottom;

        private FumenVisualEditorViewModel editor;
        private ListViewDragDropManager<SoflanGroupDisplayItemListViewBase> listDragManager;

        private SoflanGroupWrapItemGroup displaySoflanGroupItemGroupRoot;

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
                //todo messagebox
                return;
            }
            if (DisplaySoflanGroupItemGroupRoot is null)
            {
                //todo messagebox
                return;
            }

            var group = new SoflanGroupWrapItemGroup()
            {
                DisplayName = CreateNewGroupName
            };
            DisplaySoflanGroupItemGroupRoot.Add(group);

            CreateNewGroupName = string.Empty;
        }

        private SoflanGroupWrapItem currentSelectedSoflanGroupWrapItem;
        public SoflanGroupWrapItem CurrentSelectedSoflanGroupWrapItem
        {
            get => currentSelectedSoflanGroupWrapItem;
            set => Set(ref currentSelectedSoflanGroupWrapItem, value);
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

            CurrentSelectedSoflanGroupWrapItem = visit(DisplaySoflanGroupItemGroupRoot).OfType<SoflanGroupWrapItem>().FirstOrDefault(x => x.IsSelected);
        }

        private void ListDragManager_ProcessDrop(object sender, ProcessDropEventArgs<SoflanGroupDisplayItemListViewBase> e)
        {
            var placeTo = e.ItemsSource.ElementAtOrDefault(e.NewIndex);

            var item = e.DataItem;
            //only item can be moved.
            if (item is not SoflanGroupWrapItem)
                return;
            var itemParent = item.Parent;
            item.Parent?.Remove(item);

            switch (placeTo)
            {
                case SoflanGroupWrapItem placeItem:
                    placeItem.Parent.InsertBefore(item, placeItem);
                    break;
                case SoflanGroupWrapItemGroup placeGroup:
                    placeGroup.Add(item);
                    break;
                default:
                    break;
            }
        }
    }
}
