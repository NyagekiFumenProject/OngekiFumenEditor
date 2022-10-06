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
using System.Windows.Controls.Primitives;
using System.Windows.Media.Animation;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels
{
    public partial class FumenVisualEditorViewModel : PersistedDocument
    {
        private double totalDurationHeight;
        public double TotalDurationHeight
        {
            get => totalDurationHeight;
            set
            {
                value = Math.Max(value, ViewHeight);
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
                var val = Math.Min(TotalDurationHeight, Math.Max(0, value));

                Set(ref scrollViewerVerticalOffset, val);
                NotifyOfPropertyChange(() => ReverseScrollViewerVerticalOffset);
                RecalcViewProjectionMatrix();
            }
        }

        public double ReverseScrollViewerVerticalOffset
        {
            get => TotalDurationHeight - ScrollViewerVerticalOffset;
            set => ScrollViewerVerticalOffset = TotalDurationHeight - value;
        }

        private void RecalculateScrollBar()
        {
            Setting.NotifyOfPropertyChange(nameof(Setting.JudgeLineOffsetY));
        }

        #region ScrollTo

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
            ScrollViewerVerticalOffset = (float)y;
            //Log.LogInfo($"Scroll to AnimatedScrollViewer.CurrentVerticalOffset = {AnimatedScrollViewer.CurrentVerticalOffset:F2}, ScrollViewerVerticalOffset = {ScrollViewerVerticalOffset:F2}");
        }

        #endregion

        public TGrid GetCurrentJudgeLineTGrid()
        {
            var y = Setting.JudgeLineOffsetY + Rect.MinY;
            return TGridCalculator.ConvertYToTGrid(y, this);
        }
    }
}
