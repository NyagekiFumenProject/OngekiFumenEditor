using Caliburn.Micro;
using Gemini.Framework;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Modules.FumenVisualEditor.Base;
using OngekiFumenEditor.Modules.FumenVisualEditor.Views;
using OngekiFumenEditor.UI.Controls;
using OngekiFumenEditor.Utils;
using System;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels
{
    public partial class FumenVisualEditorViewModel : PersistedDocument
    {
        public double MinVisibleCanvasY => ScrollViewerVerticalOffset - Setting.JudgeLineOffsetY;
        public double MaxVisibleCanvasY => ScrollViewerVerticalOffset + CanvasHeight - Setting.JudgeLineOffsetY;

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

                Redraw(RedrawTarget.OngekiObjects | RedrawTarget.TGridUnitLines);
            }
        }

        private void RecalculateScrollBar()
        {
            Setting.NotifyOfPropertyChange(nameof(Setting.JudgeLineOffsetY));
        }

        public void ScrollViewer_OnScrollChanged(ActionExecutionContext e)
        {
            var arg = e.EventArgs as ScrollChangedEventArgs;
            var scrollViewer = e.Source as AnimatedScrollViewer;

            ScrollViewerVerticalOffset = scrollViewer.ScrollableHeight - arg.VerticalOffset;
            //Log.LogDebug($"ScrollViewerVerticalOffset = {ScrollViewerVerticalOffset}");
        }

        #region ScrollTo

        public void ScrollTo(IEditorDisplayableViewModel objViewModel)
        {
            if ((objViewModel.DisplayableObject as ITimelineObject).TGrid is not TGrid tGrid)
                throw new Exception("ScrollTo.objViewModel is not a timeline object view model.");
            ScrollTo(tGrid);
        }

        public void ScrollTo(DisplayObjectViewModelBase objViewModel)
        {
            ScrollTo(objViewModel.CanvasY);
        }

        public void ScrollTo(ITimelineObject timelineObject)
        {
            ScrollTo(timelineObject.TGrid);
        }

        public void ScrollTo(TGrid startTGrid)
        {
            var y = TGridCalculator.ConvertTGridToY(startTGrid, this);
            ScrollTo(y);
        }

        public void ScrollTo(double y)
        {
            CurrentPlayTime = (float)(TotalDurationHeight - y - CanvasHeight);
            //Log.LogInfo($"Scroll to AnimatedScrollViewer.CurrentVerticalOffset = {AnimatedScrollViewer.CurrentVerticalOffset:F2}, ScrollViewerVerticalOffset = {ScrollViewerVerticalOffset:F2}");
        }

        #endregion

        public TGrid GetCurrentJudgeLineTGrid()
        {
            var y = Setting.JudgeLineOffsetY + MinVisibleCanvasY;
            return TGridCalculator.ConvertYToTGrid(y, this);
        }
    }
}
