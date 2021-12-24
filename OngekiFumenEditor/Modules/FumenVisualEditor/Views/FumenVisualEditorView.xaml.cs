using Gemini.Framework;
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
        public bool IsDragging
        {
            get { return (bool)GetValue(IsDraggingProperty); }
            set { SetValue(IsDraggingProperty, value); }
        }

        public static readonly DependencyProperty IsDraggingProperty =
            DependencyProperty.Register("IsDragging", typeof(bool), typeof(FumenVisualEditorView), new PropertyMetadata(false));

        public bool IsMouseDown
        {
            get { return (bool)GetValue(IsMouseDownProperty); }
            set { SetValue(IsMouseDownProperty, value); }
        }

        public static readonly DependencyProperty IsMouseDownProperty =
            DependencyProperty.Register("IsMouseDown", typeof(bool), typeof(OngekiObjectViewBase), new PropertyMetadata(false));

        public FumenVisualEditorView()
        {
            InitializeComponent();

            MouseDown += OnMouseDown;
            MouseMove += OnMouseMove;
            MouseUp += OnMouseUp;
            MouseLeave += OnMouseLeave;
        }

        private void OnMouseLeave(object sender, MouseEventArgs e)
        {
            IsMouseDown = false;
            IsDragging = false;
            e.Handled = true;
        }

        private void OnMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (!(IsMouseDown && Parent is IInputElement parent))
                return;
            var selectObjectViewModels = ViewModel.DisplayObjectList
                .OfType<OngekiObjectViewBase>()
                .Select(x=>x.ViewModel)
                .Where(x => x.IsSelected)
                .ToArray();

            var pos = e.GetPosition(parent);
            if (IsDragging)
                selectObjectViewModels.ForEach(x => x.OnDragEnd(pos));

            IsMouseDown = false;
            IsDragging = false;
            e.Handled = true;
        }

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            if (!(IsMouseDown && Parent is IInputElement parent))
                return;
            e.Handled = true;
            var r = IsDragging;
            Action<DisplayObjectViewModelBase,Point> dragCall = (vm,pos) =>
            {
                if (r)
                    vm.OnDragEnd(pos);
                else
                    vm.OnDragStart(pos);
            };
            IsDragging = true;

            var pos = e.GetPosition(parent);
            var selectObjectViewModels = ViewModel.DisplayObjectList
                .OfType<OngekiObjectViewBase>()
                .Select(x => x.ViewModel)
                .Where(x => x.IsSelected)
                .ToArray();
            if (VisualTreeUtility.FindParent<Visual>(this) is FrameworkElement uiElement)
            {
                var bound = new Rect(0, 0, uiElement.ActualWidth, uiElement.ActualHeight);
                if (bound.Contains(pos))
                {
                    selectObjectViewModels.ForEach(x=>dragCall(x,pos));
                }
            }
            else
            {
                selectObjectViewModels.ForEach(x => dragCall(x, pos));
            }
        }

        private void OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                IsMouseDown = true;
                IsDragging = false;
                e.Handled = true;
            }
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

            ViewModel.OnNewObjectAdd(displayObject);
            displayObject.MoveCanvas(mousePosition);
        }
    }
}
