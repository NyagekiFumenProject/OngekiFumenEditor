using MahApps.Metro.Controls;
using OngekiFumenEditor.Modules.FumenPreviewer.ViewModels;
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

namespace OngekiFumenEditor.Modules.FumenPreviewer.Views
{
    /// <summary>
    /// FumenMetaInfoBrowserView.xaml 的交互逻辑
    /// </summary>
    public partial class FumenPreviewerView : UserControl
    {
        public FumenPreviewerView()
        {
            InitializeComponent();

            glView.Start(new()
            {
                MajorVersion = 4,
                MinorVersion = 5,
                GraphicsProfile = OpenTK.Windowing.Common.ContextProfile.Core
            });
        }

        private void glView_Ready()
        {
            Dispatcher.InvokeAsync(() =>
            {
                if (DataContext is IFumenPreviewer fumenPreviewer)
                {
                    fumenPreviewer.PrepareOpenGLView(glView);
                }
            });
        }

        private void glView_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (DataContext is IFumenPreviewer fumenPreviewer)
            {
                fumenPreviewer.OnOpenGLViewSizeChanged(glView,e);
            }
        }
    }
}
