using Caliburn.Micro;
using OngekiFumenEditor.Kernel.Graphics;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using System.Windows;
using System.Windows.Controls;
using Gemini.Modules.Toolbox;
using System.Windows.Input;
using System.Windows.Controls.Primitives;
using OngekiFumenEditor.Modules.FumenVisualEditor.Kernel;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Views
{
    /// <summary>
    /// FumenVisualEditorView.xaml 的交互逻辑
    /// </summary>
    public partial class FumenVisualEditorView : UserControl
    {
        public FumenVisualEditorView()
        {
            InitializeComponent();
            Loaded += OnPlacementViewLoaded;
            Unloaded += OnPlacementViewUnloaded;
            DataContextChanged += (_, e) => (e.OldValue as FumenVisualEditorViewModel)?.CancelPlacementTools();
        }

        private Window placementWindow;
        private bool placementViewLoaded;

        private void OnPlacementViewLoaded(object sender, RoutedEventArgs e)
        {
            if (placementViewLoaded)
                return;
            placementViewLoaded = true;
            placementWindow = Window.GetWindow(this);
            if (placementWindow != null)
                placementWindow.Deactivated += OnPlacementWindowDeactivated;
            IoC.Get<IEditorDocumentManager>().OnActivateEditorChanged += OnPlacementEditorChanged;
            // 在窗口中首次收到工具箱拖放就取消，避免留下尚未完成的第一个点。
            placementWindow?.AddHandler(DragDrop.PreviewDragEnterEvent,
                new DragEventHandler(OnToolboxDragStarted), true);
        }

        private void OnPlacementViewUnloaded(object sender, RoutedEventArgs e)
        {
            if (!placementViewLoaded)
                return;
            placementViewLoaded = false;
            placementWindow?.RemoveHandler(DragDrop.PreviewDragEnterEvent,
                new DragEventHandler(OnToolboxDragStarted));
            if (placementWindow != null)
                placementWindow.Deactivated -= OnPlacementWindowDeactivated;
            IoC.Get<IEditorDocumentManager>().OnActivateEditorChanged -= OnPlacementEditorChanged;
            placementWindow = null;
            (DataContext as FumenVisualEditorViewModel)?.CancelPlacementTools();
        }

        private void OnPlacementEditorChanged(FumenVisualEditorViewModel next, FumenVisualEditorViewModel old)
        {
            if (old == DataContext)
                old?.CancelPlacementTools();
        }

        private void OnPlacementWindowDeactivated(object sender, System.EventArgs e)
        {
            (DataContext as FumenVisualEditorViewModel)?.CancelPlacementTools();
        }


        private void OnToolboxDragStarted(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(ToolboxDragDrop.DataFormat))
            {
                (DataContext as FumenVisualEditorViewModel)?.CancelPlacementTools();
            }
        }
    }
}
