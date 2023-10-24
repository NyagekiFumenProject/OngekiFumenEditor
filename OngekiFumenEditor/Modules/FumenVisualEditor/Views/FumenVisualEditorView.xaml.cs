using Caliburn.Micro;
using Gemini.Framework;
using Gemini.Modules.Toolbox;
using Gemini.Modules.Toolbox.Models;
using OngekiFumenEditor.Kernel.Graphics;
using OngekiFumenEditor.Modules.FumenVisualEditor.Base;
using OngekiFumenEditor.Modules.FumenVisualEditor.Controls;
using OngekiFumenEditor.Modules.FumenVisualEditor.Graphics;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.OngekiObjects;
using OngekiFumenEditor.Modules.FumenVisualEditorSettings.ViewModels;
using OngekiFumenEditor.Utils;
using OpenTK.Windowing.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

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
            IoC.Get<IDrawingManager>().CreateGraphicsContext(glView);
        }

        private void glView_Ready()
        {
            Dispatcher.InvokeAsync(() =>
            {
                if (DataContext is IDrawingContext fumenPreviewer)
                {
                    fumenPreviewer.PrepareRender(glView);
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

        private void Grid_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            (DataContext as FumenVisualEditorViewModel)?.OnLoaded(new ActionExecutionContext() { View = this, EventArgs = e });
        }

        private void Grid_Loaded(object sender, RoutedEventArgs e)
        {
            (DataContext as FumenVisualEditorViewModel)?.OnLoaded(new ActionExecutionContext() { View = this });
        }
    }
}
