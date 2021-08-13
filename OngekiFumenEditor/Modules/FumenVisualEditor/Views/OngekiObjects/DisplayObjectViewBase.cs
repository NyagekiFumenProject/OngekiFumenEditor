using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.OngekiObjects;
using OngekiFumenEditor.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Views.OngekiObjects
{
    public class DisplayObjectViewBase : UserControl
    {
        public DisplayObjectViewModelBase ViewModel => DataContext as DisplayObjectViewModelBase;

        public bool IsDragging
        {
            get { return (bool)GetValue(IsDraggingProperty); }
            set { SetValue(IsDraggingProperty, value); }
        }

        // Using a DependencyProperty as the backing store for IsDragging.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty IsDraggingProperty =
            DependencyProperty.Register("IsDragging", typeof(bool), typeof(DisplayObjectViewBase), new PropertyMetadata(false));

        public bool IsMouseDown
        {
            get { return (bool)GetValue(IsMouseDownProperty); }
            set { SetValue(IsMouseDownProperty, value); }
        }

        // Using a DependencyProperty as the backing store for IsMouseDown.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty IsMouseDownProperty =
            DependencyProperty.Register("IsMouseDown", typeof(bool), typeof(DisplayObjectViewBase), new PropertyMetadata(false));

        protected virtual void OnDragging(Point relativePoint)
        {
            ViewModel.X = relativePoint.X;
            ViewModel.Y = relativePoint.Y;
        }

        protected virtual void OnMouseClick()
        {
        }

        private void UserControl_MouseDown(object sender, MouseButtonEventArgs e)
        {
            IsMouseDown = true;
            IsDragging = false;
            e.Handled = true;
        }

        private void UserControl_MouseMove(object sender, MouseEventArgs e)
        {
            if (!(IsMouseDown && Parent is IInputElement parent))
                return;
            e.Handled = true;
            IsDragging = true;

            var pos = e.GetPosition(parent);
            if(parent is Visual uiElement)
            {
                if (VisualTreeHelper.GetContentBounds(uiElement).Contains(pos))
                {
                    OnDragging(pos);
                }
            }
            else
            {
                OnDragging(pos);
            }
        }

        private void UserControl_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (IsMouseDown && !IsDragging)
                OnMouseClick();
            IsMouseDown = false;
            IsDragging = false;
            e.Handled = true;
        }

        private void UserControl_MouseLeave(object sender, MouseEventArgs e)
        {
            IsMouseDown = false;
            IsDragging = false;
            e.Handled = true;
        }
    }
}
