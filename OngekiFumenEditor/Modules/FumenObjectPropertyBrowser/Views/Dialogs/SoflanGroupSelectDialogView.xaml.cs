using MahApps.Metro.Controls;
using OngekiFumenEditor.Modules.FumenObjectPropertyBrowser.ViewModels.Dialogs;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace OngekiFumenEditor.Modules.FumenObjectPropertyBrowser.Views.Dialogs
{
    public partial class SoflanGroupSelectDialogView : MetroWindow
    {
        public SoflanGroupSelectDialogView()
        {
            InitializeComponent();
        }

        private void OnCreateQuickGroup(object sender, RoutedEventArgs e)
        {
            if (DataContext is not SoflanGroupSelectDialogViewModel viewModel)
                return;

            viewModel.CreateQuickGroup();
            Dispatcher.BeginInvoke(new Action(ScrollToSelected), DispatcherPriority.ContextIdle);
        }

        private void OnSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (DataContext is SoflanGroupSelectDialogViewModel viewModel)
                viewModel.OnSelectedNodeChanged(e.NewValue as SoflanGroupSelectNodeViewModel);
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is not SoflanGroupSelectDialogViewModel { SelectedNode: not null })
                return;

            // 等树容器生成后再滚动到当前组；多选混合值没有 SelectedNode，因此不会滚动。
            Dispatcher.BeginInvoke(new Action(ScrollToSelected), DispatcherPriority.ContextIdle);
        }

        private void ScrollToSelected()
        {
            if (DataContext is not SoflanGroupSelectDialogViewModel viewModel
                || viewModel.SelectedNode is null)
                return;

            GroupTree.UpdateLayout();
            var container = FindContainer(GroupTree, viewModel.SelectedNode);
            if (container is null)
                return;

            container.BringIntoView();
            container.Focus();
        }

        private static TreeViewItem FindContainer(ItemsControl parent, object item)
        {
            if (parent.ItemContainerGenerator.ContainerFromItem(item) is TreeViewItem direct)
                return direct;

            foreach (var child in parent.Items)
            {
                if (parent.ItemContainerGenerator.ContainerFromItem(child) is not TreeViewItem childContainer)
                    continue;

                var found = FindContainer(childContainer, item);
                if (found is not null)
                    return found;
            }

            return null;
        }
    }
}
