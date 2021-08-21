using OngekiFumenEditor.Base;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.OngekiObjects;
using OngekiFumenEditor.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Views
{
    public class OngekiObjectViewBase : UserControl
    {
        public DisplayObjectViewModelBase ViewModel => DataContext as DisplayObjectViewModelBase;
        private static DropShadowEffect SelectEffect = new DropShadowEffect() { ShadowDepth = 0, Color = Colors.Yellow, BlurRadius = 25 };

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

        public static readonly DependencyProperty IsSelectedProperty =
            DependencyProperty.Register("IsSelected", typeof(bool), typeof(OngekiObjectViewBase), new PropertyMetadata(false));

        public bool IsSelected
        {
            get { return (bool)GetValue(IsSelectedProperty); }
            set
            {
                SetValue(IsSelectedProperty, value);
                Effect = value ? SelectEffect : null;
            }
        }

        public static readonly DependencyProperty IsMouseDownProperty =
            DependencyProperty.Register("IsMouseDown", typeof(bool), typeof(OngekiObjectViewBase), new PropertyMetadata(false));

        public bool IsPreventXAutoClose => ViewModel?.EditorViewModel?.IsPreventXAutoClose ?? false;

        public bool IsPreventTimelineAutoClose => ViewModel?.EditorViewModel?.IsPreventTimelineAutoClose ?? false;

        public OngekiObjectViewBase()
        {
            MouseDown += OnMouseDown;
            MouseMove += OnMouseMove;
            MouseUp += OnMouseUp;
            MouseLeave += OnMouseLeave;
        }

        protected virtual void OnDragStart()
        {

        }

        protected virtual void OnDragEnd()
        {

        }

        protected virtual void OnDragEnd(Point p) => ViewModel.MoveCanvas(p);

        protected virtual void OnMouseClick()
        {
            IsSelected = !IsSelected;
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

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            if (!(IsMouseDown && Parent is IInputElement parent))
                return;
            e.Handled = true;
            Action<Point> dragCall = IsDragging ? OnDragEnd : _ => OnDragStart();
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
            if (IsDragging)
                OnDragEnd();
            else if (IsMouseDown)
                OnMouseClick();

            IsMouseDown = false;
            IsDragging = false;
            e.Handled = true;
        }

        private void OnMouseLeave(object sender, MouseEventArgs e)
        {
            IsMouseDown = false;
            IsDragging = false;
            e.Handled = true;
        }
    }
}
