using Caliburn.Micro;
using OngekiFumenEditor.Kernel.Graphics;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
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
            IoC.Get<IDrawingManager>().InitializeRenderControl(glView);
        }

        private void glView_Ready()
        {
            Dispatcher.InvokeAsync(() =>
            {
                if (DataContext is IDrawingContext drawingContext)
                {
                    drawingContext.PrepareRenderLoop(glView);
                    //start render loop
                    glView.Render += (ts) => drawingContext.Render(ts);
                }
            });
        }

        private void glView_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (DataContext is IDrawingContext fumenPreviewer)
            {
                fumenPreviewer.OnRenderSizeChanged(glView, e);
            }
        }
    }
}
