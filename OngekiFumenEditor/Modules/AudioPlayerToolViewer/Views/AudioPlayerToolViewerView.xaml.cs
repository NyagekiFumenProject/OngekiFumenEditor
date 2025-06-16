using Caliburn.Micro;
using OngekiFumenEditor.Kernel.Graphics;
using System.Windows;
using System.Windows.Controls;

namespace OngekiFumenEditor.Modules.AudioPlayerToolViewer.Views
{
	/// <summary>
	/// FumenMetaInfoBrowserView.xaml 的交互逻辑
	/// </summary>
	public partial class AudioPlayerToolViewerView : UserControl
	{
		public AudioPlayerToolViewerView()
		{
			InitializeComponent();
			IoC.Get<IRenderManager>().InitializeRenderControl(glView);
		}

		private void GLWpfControl_Ready()
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

		private void GLWpfControl_SizeChanged(object sender, SizeChangedEventArgs e)
		{
			if (DataContext is IDrawingContext fumenPreviewer)
			{
				fumenPreviewer.OnRenderSizeChanged(glView, e);
			}
		}
	}
}
