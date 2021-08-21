using Gemini.Modules.Toolbox;
using Gemini.Modules.Toolbox.Models;
using OngekiFumenEditor.Modules.FumenVisualEditor.Controls;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.OngekiObjects;
using OngekiFumenEditor.Utils;
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
        }

        private FumenVisualEditorViewModel ViewModel => DataContext as FumenVisualEditorViewModel;

        private void Grid_DragEnter(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(ToolboxDragDrop.DataFormat))
                e.Effects = DragDropEffects.None;
        }

        private void Grid_Drop(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(ToolboxDragDrop.DataFormat))
                return;

            var mousePosition = e.GetPosition(VisualDisplayer);
            var toolboxItem = (ToolboxItem)e.Data.GetData(ToolboxDragDrop.DataFormat);
            var displayObject = Activator.CreateInstance(toolboxItem.ItemType) as DisplayObjectViewModelBase;

            displayObject.MoveCanvas(mousePosition);
            ViewModel.OnNewObjectAdd(displayObject);
        }
    }
}
