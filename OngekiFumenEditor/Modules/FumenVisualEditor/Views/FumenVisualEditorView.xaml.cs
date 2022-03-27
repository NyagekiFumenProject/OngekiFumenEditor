using Caliburn.Micro;
using Gemini.Framework;
using Gemini.Modules.Toolbox;
using Gemini.Modules.Toolbox.Models;
using OngekiFumenEditor.Modules.FumenVisualEditor.Base;
using OngekiFumenEditor.Modules.FumenVisualEditor.Controls;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.OngekiObjects;
using OngekiFumenEditor.Modules.FumenVisualEditorSettings.ViewModels;
using OngekiFumenEditor.Utils;
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
#pragma warning disable IDE0052 // 删除未读的私有成员
        private bool _dragging;
#pragma warning restore IDE0052 // 删除未读的私有成员
        private readonly Binding _scrollBinding;

        public FumenVisualEditorView()
        {
            InitializeComponent();
            _scrollBinding = new Binding
            {
                Source = myScrollViewer,
                Path = new PropertyPath("CurrentVerticalOffset"),
                Mode = BindingMode.OneWay
            };
        }

        private void Grid_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            (DataContext as FumenVisualEditorViewModel)?.OnLoaded(new ActionExecutionContext() { View = this, EventArgs = e });
        }

        private void Grid_Loaded(object sender, RoutedEventArgs e)
        {
            (DataContext as FumenVisualEditorViewModel)?.OnLoaded(new ActionExecutionContext() { View = this });
        }

        private void Thumb_DragStarted(object sender, DragStartedEventArgs e)
        {
            var scrollBar = (ScrollBar)sender;
            scrollBar.Value = scrollBar.Value;
            _dragging = true;
        }

        private void Thumb_DragDelta(object sender, DragDeltaEventArgs e)
        {
            var scrollBar = (ScrollBar)sender;
            myScrollViewer.ScrollToVerticalOffsetWithAnimation(scrollBar.Value);
        }

        private void Thumb_DragCompleted(object sender, DragCompletedEventArgs e)
        {
            var scrollBar = (ScrollBar)sender;
            myScrollViewer.ScrollToVerticalOffsetWithAnimation(scrollBar.Value);
            scrollBar.SetBinding(RangeBase.ValueProperty, _scrollBinding);
            _dragging = false;
        }

        private async void ButtonBase_OnClick(object sender, RoutedEventArgs e)
        {
            var scrollBar = (ScrollBar)sender;
            if (e.OriginalSource is not RepeatButton { Command: RoutedCommand rc }) return;
            if (rc.Name == "PageDown")
            {
                myScrollViewer.ScrollToVerticalOffsetWithAnimation(
                    myScrollViewer.CurrentVerticalOffset + myScrollViewer.ViewportHeight);
                // todo: fit the grid x 4 (4/4 Beat)
            }
            else if (rc.Name == "PageUp")
            {
                myScrollViewer.ScrollToVerticalOffsetWithAnimation(
                    myScrollViewer.CurrentVerticalOffset - myScrollViewer.ViewportHeight);
                // todo: fit the grid x 4 (4/4 Beat)
            }
            else if (rc.Name == "LineDown")
            {
                myScrollViewer.ScrollToVerticalOffsetWithAnimation(
                    myScrollViewer.CurrentVerticalOffset + myScrollViewer.VerticalScrollingDistance);
                // todo: fit the grid
            }
            else if (rc.Name == "LineUp")
            {
                myScrollViewer.ScrollToVerticalOffsetWithAnimation(
                    myScrollViewer.CurrentVerticalOffset - myScrollViewer.VerticalScrollingDistance);
                // todo: fit the grid
            }

            await Task.Delay(1);
            scrollBar.SetBinding(RangeBase.ValueProperty, _scrollBinding);
        }
    }
}
