using OngekiFumenEditor.Modules.FumenSoflanGroupListViewer.ViewModels;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace OngekiFumenEditor.Modules.FumenSoflanGroupListViewer.Views
{
    public partial class FumenSoflanGroupListViewerView : UserControl
    {
        private Point dragStart;
        private SoflanGroupTreeNodeViewModel dragCandidate;

        public FumenSoflanGroupListViewerView()
        {
            InitializeComponent();
        }

        private void OnGroupTreeSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (DataContext is FumenSoflanGroupListViewerViewModel vm)
                vm.OnSelectedNodeChanged(e.NewValue as SoflanGroupTreeNodeViewModel);
        }

        private void OnGroupTreePreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            dragStart = e.GetPosition(GroupTree);
            dragCandidate = null;

            if (FindTreeViewItem(e.OriginalSource as DependencyObject, out var interactive) is not { } item || interactive)
                return;

            if (item.DataContext is SoflanGroupTreeNodeViewModel node && !node.IsDefaultGroup)
                dragCandidate = node;
        }

        private void OnGroupTreePreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed || dragCandidate is null)
                return;

            var position = e.GetPosition(GroupTree);
            if (Math.Abs(position.X - dragStart.X) < SystemParameters.MinimumHorizontalDragDistance
                && Math.Abs(position.Y - dragStart.Y) < SystemParameters.MinimumVerticalDragDistance)
                return;

            var node = dragCandidate;
            dragCandidate = null;
            DragDrop.DoDragDrop(GroupTree, node, DragDropEffects.Move);
        }

        private void OnGroupTreeDragOver(object sender, DragEventArgs e)
        {
            var dragged = e.Data.GetData(typeof(SoflanGroupTreeNodeViewModel)) as SoflanGroupTreeNodeViewModel;
            e.Effects = dragged is not null && !dragged.IsDefaultGroup
                ? DragDropEffects.Move
                : DragDropEffects.None;
            e.Handled = true;
        }

        private void OnGroupTreeDrop(object sender, DragEventArgs e)
        {
            if (e.Data.GetData(typeof(SoflanGroupTreeNodeViewModel)) is not SoflanGroupTreeNodeViewModel dragged)
                return;

            var target = FindTreeViewItem(e.OriginalSource as DependencyObject, out _)?.DataContext as SoflanGroupTreeNodeViewModel;
            if (DataContext is FumenSoflanGroupListViewerViewModel vm)
                vm.MoveNodeTo(dragged, target);

            e.Handled = true;
        }

        private static TreeViewItem FindTreeViewItem(DependencyObject source, out bool interactive)
        {
            interactive = false;
            while (source is not null)
            {
                if (source is ButtonBase or TextBox)
                    interactive = true;

                if (source is TreeViewItem item)
                    return item;

                source = VisualTreeHelper.GetParent(source);
            }

            return null;
        }
    }
}
