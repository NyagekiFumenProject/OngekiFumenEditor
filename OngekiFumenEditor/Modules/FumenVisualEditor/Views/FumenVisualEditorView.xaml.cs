using Gemini.Modules.Toolbox;
using System;
using System.Windows;
using System.Windows.Controls;

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
            DataContextChanged += (_, e) => InteractionInterrupted?.Invoke(this, EventArgs.Empty);
        }

        public event EventHandler InteractionInterrupted;

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
            placementWindow = null;
            InteractionInterrupted?.Invoke(this, EventArgs.Empty);
        }

        private void OnPlacementWindowDeactivated(object sender, EventArgs e)
        {
            InteractionInterrupted?.Invoke(this, EventArgs.Empty);
        }

        private void OnToolboxDragStarted(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(ToolboxDragDrop.DataFormat))
            {
                InteractionInterrupted?.Invoke(this, EventArgs.Empty);
            }
        }
    }
}
