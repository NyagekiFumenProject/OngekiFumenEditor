using Caliburn.Micro;
using OngekiFumenEditor.Kernel.Graphics;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Views
{
    /// <summary>
    /// FumenVisualEditorView.xaml 的交互逻辑
    /// </summary>
    public partial class FumenVisualEditorView : UserControl
    {
        private Window placementWindow;

        public FumenVisualEditorView()
        {
            InitializeComponent();
            Loaded += OnPlacementViewLoaded;
            Unloaded += OnPlacementViewUnloaded;
            DataContextChanged += OnPlacementDataContextChanged;
        }

        private void OnPlacementViewLoaded(object sender, RoutedEventArgs e)
        {
            DetachPlacementWindow();
            placementWindow = Window.GetWindow(this);
            if (placementWindow is null)
                return;

            placementWindow.Deactivated += OnPlacementWindowDeactivated;
            placementWindow.AddHandler(PreviewMouseDownEvent, new MouseButtonEventHandler(OnPlacementWindowMouseDown), true);
            placementWindow.AddHandler(PreviewMouseUpEvent, new MouseButtonEventHandler(OnPlacementWindowMouseUp), true);
            placementWindow.AddHandler(ContextMenuOpeningEvent, new ContextMenuEventHandler(OnPlacementContextMenuOpening), true);
            placementWindow.AddHandler(DragDrop.QueryContinueDragEvent, new QueryContinueDragEventHandler(OnOtherToolDrag), true);
        }

        private void OnPlacementViewUnloaded(object sender, RoutedEventArgs e)
        {
            (DataContext as FumenVisualEditorViewModel)?.CancelBatchProjectilePlacement();
            DetachPlacementWindow();
        }

        private void OnPlacementDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (!ReferenceEquals(e.OldValue, e.NewValue))
                (e.OldValue as FumenVisualEditorViewModel)?.CancelBatchProjectilePlacement();
        }

        private void DetachPlacementWindow()
        {
            if (placementWindow is null)
                return;
            placementWindow.Deactivated -= OnPlacementWindowDeactivated;
            placementWindow.RemoveHandler(PreviewMouseDownEvent, new MouseButtonEventHandler(OnPlacementWindowMouseDown));
            placementWindow.RemoveHandler(PreviewMouseUpEvent, new MouseButtonEventHandler(OnPlacementWindowMouseUp));
            placementWindow.RemoveHandler(ContextMenuOpeningEvent, new ContextMenuEventHandler(OnPlacementContextMenuOpening));
            placementWindow.RemoveHandler(DragDrop.QueryContinueDragEvent, new QueryContinueDragEventHandler(OnOtherToolDrag));
            placementWindow = null;
        }

        private void OnPlacementWindowDeactivated(object sender, System.EventArgs e)
            => (DataContext as FumenVisualEditorViewModel)?.CancelBatchProjectilePlacement();

        private void OnOtherToolDrag(object sender, QueryContinueDragEventArgs e)
            => (DataContext as FumenVisualEditorViewModel)?.CancelBatchProjectilePlacement();

        private void OnPlacementWindowMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is not FumenVisualEditorViewModel editor)
                return;
            if (e.ChangedButton == MouseButton.Left)
                editor.EndBatchPlacementGestureWithoutCommit();
            else if (e.ChangedButton == MouseButton.Right && editor.BeginBatchPlacementRightGesture())
                e.Handled = true;
        }

        private void OnPlacementWindowMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is not FumenVisualEditorViewModel editor)
                return;
            if (e.ChangedButton == MouseButton.Right && editor.ConsumeBatchPlacementRightMouseUp())
                e.Handled = true;
            else if (e.ChangedButton == MouseButton.Left &&
                !(e.OriginalSource is Visual source && (ReferenceEquals(source, renderControlHost) || renderControlHost.IsAncestorOf(source))) &&
                editor.EndBatchPlacementGestureWithoutCommit())
                e.Handled = true;
        }

        private void OnPlacementContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            if (e.CursorLeft >= 0 && DataContext is FumenVisualEditorViewModel editor && editor.ConsumeBatchPlacementContextMenu())
                e.Handled = true;
        }
    }
}
