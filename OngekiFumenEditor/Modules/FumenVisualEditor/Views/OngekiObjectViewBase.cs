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
using System.Windows.Input;
using System.Windows.Media;
using static OngekiFumenEditor.Utils.StatusNotifyHelper;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Views
{
    public class OngekiObjectViewBase : UserControl
    {
        public OngekiObjectViewModelBase ViewModel => DataContext as OngekiObjectViewModelBase;

        public bool IsDragging
        {
            get { return (bool)GetValue(IsDraggingProperty); }
            set { SetValue(IsDraggingProperty, value); }
        }

        public static readonly DependencyProperty IsDraggingProperty =
            DependencyProperty.Register("IsDragging", typeof(bool), typeof(OngekiObjectViewBase), new PropertyMetadata(false));

        public bool IsMouseDown
        {
            get { return (bool)GetValue(IsMouseDownProperty); }
            set { SetValue(IsMouseDownProperty, value); }
        }

        public static readonly DependencyProperty IsMouseDownProperty =
            DependencyProperty.Register("IsMouseDown", typeof(bool), typeof(OngekiObjectViewBase), new PropertyMetadata(false));

        public bool IsPreventXAutoClose
        {
            get { return (bool)GetValue(IsPreventXAutoCloseProperty); }
            set { SetValue(IsPreventXAutoCloseProperty, value); }
        }

        public static readonly DependencyProperty IsPreventXAutoCloseProperty =
            DependencyProperty.Register("IsPreventXAutoClose", typeof(bool), typeof(OngekiObjectViewBase), new PropertyMetadata(false));

        public bool IsPreventTimelineAutoClose
        {
            get { return (bool)GetValue(IsPreventTimelineAutoCloseProperty); }
            set { SetValue(IsPreventTimelineAutoCloseProperty, value); }
        }

        public static readonly DependencyProperty IsPreventTimelineAutoCloseProperty =
            DependencyProperty.Register("IsPreventTimelineAutoClose", typeof(bool), typeof(OngekiObjectViewBase), new PropertyMetadata(false));

        public OngekiObjectViewBase()
        {
            MouseDown += OnMouseDown;
            MouseMove += OnMouseMove;
            MouseUp += OnMouseUp;
            MouseLeave += OnMouseLeave;
        }

        Notify notify;

        protected virtual void OnDragStart()
        {
            notify = StatusNotifyHelper.BeginStatus("Hehehe");
        }

        protected virtual void OnDragMove(Point relativePoint)
        {
            ViewModel.Y = relativePoint.Y;

            if (ViewModel.CanMoveX)
            {
                ViewModel.X = relativePoint.X;
            }
        }

        protected virtual void OnDragEnd()
        {
            notify?.Dispose();
        }

        protected virtual void OnMouseClick()
        {

        }

        private void OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            IsMouseDown = true;
            IsDragging = false;
            e.Handled = true;
        }

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            if (!(IsMouseDown && Parent is IInputElement parent))
                return;
            e.Handled = true;
            Action<Point> dragCall = IsDragging ? OnDragMove : _ => OnDragStart();
            IsDragging = true;

            var pos = e.GetPosition(parent);
            if (parent is Visual uiElement)
            {
                if (VisualTreeHelper.GetContentBounds(uiElement).Contains(pos))
                {
                    dragCall(pos);
                }
            }
            else
            {
                dragCall(pos);
            }
        }

        private void OnMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (IsMouseDown && !IsDragging)
                OnMouseClick();
            IsMouseDown = false;
            IsDragging = false;
            e.Handled = true;
        }

        private void OnMouseLeave(object sender, MouseEventArgs e)
        {
            if (IsMouseDown && !IsDragging)
                OnDragEnd();
            IsMouseDown = false;
            IsDragging = false;
            e.Handled = true;
        }
    }
}
