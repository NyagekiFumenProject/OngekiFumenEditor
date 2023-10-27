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
			IoC.Get<IDrawingManager>().CreateGraphicsContext(glView);
		}

		private void GLWpfControl_Ready()
		{
			Dispatcher.InvokeAsync(() =>
			{
				if (DataContext is IDrawingContext fumenPreviewer)
				{
					fumenPreviewer.PrepareRender(glView);
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
