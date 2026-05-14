using Gemini.Framework;
using OngekiFumenEditor.Core.Base;
using OngekiFumenEditor.Utils;
using System;
using System.Linq;
using System.Runtime.CompilerServices;

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
			/*
            set
            {
                var val = Math.Min(TotalDurationHeight, Math.Max(0, value));
                Func<double, FumenVisualEditorViewModel, TGrid> convertToTGrid = IsDesignMode ? ((y, editor) => editor.ConvertYToTGrid_DesignMode(y)) : ((y, editor) => editor.ConvertYToTGrid_PreviewMode(y).OrderBy(x => Math.Abs(x.TotalGrid - editor.GetCurrentTGrid().TotalGrid)).FirstOrDefault());

                Set(ref scrollViewerVerticalOffset, val);
                NotifyOfPropertyChange(() => ReverseScrollViewerVerticalOffset);
                RecalcViewProjectionMatrix();
                currentTGrid = convertToTGrid(scrollViewerVerticalOffset, this);
            }
            */
		}

		public double ReverseScrollViewerVerticalOffset
		{
			get => TotalDurationHeight - ScrollViewerVerticalOffset;
			set
			{
				//ScrollViewerVerticalOffset = TotalDurationHeight - value;
				var val = TotalDurationHeight - value;
				if (IsDesignMode)
				{
					var audioTime = ConvertYToAudioTime_DesignMode(val);
					ScrollTo(audioTime);
				}
				else
				{
					var curTGrid = GetCurrentTGrid();
					var nextTGrid = ConvertYToTGrid_PreviewMode(val).OrderBy(x => Math.Abs(x.TotalGrid - curTGrid.TotalGrid)).FirstOrDefault();

					if (nextTGrid is not null)
					{
						var audioTime = ConvertTGridToAudioTime(nextTGrid);
						ScrollTo(audioTime);
					}
				}
			}
		}

		#region ScrollTo

		public void ScrollTo(ITimelineObject timelineObject)
		{
			ScrollTo(timelineObject.TGrid);
		}

		public void ScrollTo(TGrid startTGrid)
		{
			if (startTGrid is null)
				return;
			var audioTime = ConvertTGridToAudioTime(startTGrid);
			ScrollTo(audioTime);
		}

		public void ScrollTo(TimeSpan audioTime)
		{
			var fixedAudioTime = MathUtils.Max(TimeSpan.Zero, MathUtils.Min(audioTime, EditorProjectData.AudioDuration));
			CurrentPlayTime = fixedAudioTime;

			var val = IsDesignMode ?
				ConvertAudioTimeToY_DesignMode(fixedAudioTime) :
				ConvertAudioTimeToY_PreviewMode(fixedAudioTime);
			val = Math.Min(TotalDurationHeight, Math.Max(0, val));

			scrollViewerVerticalOffset = val;
			NotifyOfPropertyChange(() => ReverseScrollViewerVerticalOffset);
		}

		#endregion

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public TGrid GetCurrentTGrid() => ConvertAudioTimeToTGrid(CurrentPlayTime);
	}
}
