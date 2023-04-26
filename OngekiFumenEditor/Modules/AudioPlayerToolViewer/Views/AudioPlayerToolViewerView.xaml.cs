using OngekiFumenEditor.Kernel.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

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
            glView.Start(new()
            {
                MajorVersion = 4,
                MinorVersion = 5,
                GraphicsProfile = OpenTK.Windowing.Common.ContextProfile.Core
            });
        }

        private void GLWpfControl_Ready()
        {
            Dispatcher.InvokeAsync(() =>
            {
                if (DataContext is IDrawingContext fumenPreviewer)
                {
                    fumenPreviewer.PrepareOpenGLView(glView);
                }
            });
        }

        private void GLWpfControl_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (DataContext is IDrawingContext fumenPreviewer)
            {
                fumenPreviewer.OnOpenGLViewSizeChanged(glView, e);
            }
        }
    }
}
