using Caliburn.Micro;
using Gemini.Framework;
using OngekiFumenEditor.Modules.FumenVisualEditor.Base;
using OngekiFumenEditor.Modules.FumenVisualEditor.Views;
using OngekiFumenEditor.UI.Controls;
using OngekiFumenEditor.Utils;
using System;
using System.Security.Cryptography;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels
{
    public partial class FumenVisualEditorViewModel : PersistedDocument
    {
        public double MinVisibleCanvasY => ScrollViewerVerticalOffset;
        public double MaxVisibleCanvasY => ScrollViewerVerticalOffset + CanvasHeight;

        private double totalDurationHeight;
        public double TotalDurationHeight
        {
            get => totalDurationHeight;
            set
            {
                value = Math.Max(value, CanvasHeight);
                //Log.LogDebug($"TotalDurationHeight {TotalDurationHeight} -> {value}");
                Set(ref totalDurationHeight, value);
            }
        }

        private double scrollViewerVerticalOffset;
        public double ScrollViewerVerticalOffset
        {
            get => scrollViewerVerticalOffset;
            set
            {
                Set(ref scrollViewerVerticalOffset, value);
                NotifyOfPropertyChange(() => MaxVisibleCanvasY);
                NotifyOfPropertyChange(() => MinVisibleCanvasY);
                Redraw(RedrawTarget.TGridUnitLines);
                //Log.LogDebug($"current:{ScrollViewerVerticalOffset:F2}  min:{MinVisibleCanvasY:F2}  max:{MaxVisibleCanvasY:F2}");
            }
        }

        private void RecalculateScrollBar()
        {
            //todo 重新计算理论高度
        }

        public void ScrollViewer_OnScrollChanged(ActionExecutionContext e)
        {
            var arg = e.EventArgs as ScrollChangedEventArgs;
            var scrollViewer = e.Source as AnimatedScrollViewer;

            ScrollViewerVerticalOffset = scrollViewer.ScrollableHeight - arg.VerticalOffset;
            //Log.LogDebug($"ScrollViewerVerticalOffset = {ScrollViewerVerticalOffset}");
        }

        #region ScrollViewer Animations

        private AnimatedScrollViewer AnimatedScrollViewer => (GetView() as FumenVisualEditorView)?.myAnimatedScrollViewer;

        public FumenScrollViewerAnimationWrapper BeginScrollAnimation()
        {
            var animation = new DoubleAnimation(TotalDurationHeight, 0, TimeSpan.FromMilliseconds(TotalDurationHeight));
            Timeline.SetDesiredFrameRate(animation, 60);
            animation.FillBehavior = FillBehavior.HoldEnd;

            return new FumenScrollViewerAnimationWrapper(this, new AnimationWrapper(animation, AnimatedScrollViewer, AnimatedScrollViewer.CurrentVerticalOffsetProperty));
        }

        #endregion
    }
}
