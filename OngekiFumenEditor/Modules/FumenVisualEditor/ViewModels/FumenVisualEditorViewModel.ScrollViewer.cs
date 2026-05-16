using Gemini.Framework;
using OngekiFumenEditor.Core.Base;
using OngekiFumenEditor.Core.Base.OngekiObjects;
using OngekiFumenEditor.Core.Base.OngekiObjects.ConnectableObject;
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

		private double verticalScrollMaximum;
		public double VerticalScrollMaximum
		{
			get => verticalScrollMaximum;
			private set => Set(ref verticalScrollMaximum, Math.Max(0, value));
		}

		private double previewScrollPositionMs;
		private double previewTailAllowanceMs;

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

		public double ReverseVerticalScrollValue
		{
			get => IsDesignMode
				? TotalDurationHeight - ScrollViewerVerticalOffset
				: VerticalScrollMaximum - previewScrollPositionMs;
			set
			{
				if (IsDesignMode)
				{
					var val = TotalDurationHeight - value;
					var audioTime = ConvertYToAudioTime_DesignMode(val);
					ScrollTo(audioTime);
					return;
				}

				var scrollPositionMs = Math.Min(VerticalScrollMaximum, Math.Max(0, VerticalScrollMaximum - value));
				SetPreviewScrollPosition(scrollPositionMs);
			}
		}

		public double ReverseScrollViewerVerticalOffset
		{
			get => ReverseVerticalScrollValue;
			set => ReverseVerticalScrollValue = value;
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
			var audioDuration = GetAudioDuration();
			var fixedAudioTime = MathUtils.Max(TimeSpan.Zero, MathUtils.Min(audioTime, audioDuration));

			if (IsPreviewMode)
			{
				SetPreviewScrollPosition(Math.Min(VerticalScrollMaximum, Math.Max(0, audioTime.TotalMilliseconds)));
				return;
			}

			CurrentPlayTime = fixedAudioTime;

			var val = ConvertAudioTimeToY_DesignMode(fixedAudioTime);
			val = Math.Min(TotalDurationHeight, Math.Max(0, val));

			scrollViewerVerticalOffset = val;
			NotifyOfPropertyChange(() => ReverseScrollViewerVerticalOffset);
			NotifyOfPropertyChange(() => ReverseVerticalScrollValue);
		}

		#endregion

		public void RecalculateScrollMetrics()
		{
			var audioDuration = GetAudioDuration();
			var audioDurationMs = Math.Max(0, audioDuration.TotalMilliseconds);

			previewTailAllowanceMs = 0;

			if (Fumen is not null && audioDuration > TimeSpan.Zero)
			{
				var audioEndTGrid = ConvertAudioTimeToTGrid(audioDuration);
				var maxDisplayTGrid = GetMaxDisplayEndTGrid();

				if (audioEndTGrid is not null && maxDisplayTGrid is not null && maxDisplayTGrid > audioEndTGrid)
				{
					var audioEndY = ConvertTGridToY_DesignMode(audioEndTGrid);
					var maxDisplayY = ConvertTGridToY_DesignMode(maxDisplayTGrid);
					if (maxDisplayY > audioEndY)
					{
						var tailAudioTime = ConvertYToAudioTime_DesignMode(audioEndY + (maxDisplayY - audioEndY));
						previewTailAllowanceMs = Math.Max(0, (tailAudioTime - audioDuration).TotalMilliseconds);
					}
				}
			}

			VerticalScrollMaximum = IsDesignMode ? TotalDurationHeight : audioDurationMs + previewTailAllowanceMs;
			previewScrollPositionMs = Math.Min(VerticalScrollMaximum, Math.Max(0, previewScrollPositionMs));

			NotifyOfPropertyChange(() => ReverseScrollViewerVerticalOffset);
			NotifyOfPropertyChange(() => ReverseVerticalScrollValue);
		}

		private TGrid GetMaxDisplayEndTGrid()
		{
			TGrid max = TGrid.Zero;

			foreach (var displayable in Fumen.GetAllDisplayableObjects())
			{
				var endTGrid = displayable switch
				{
					Hold hold => hold.EndTGrid,
					ISoflan soflan => soflan.EndTGrid,
					ConnectableStartObject connectableStart => connectableStart.MaxTGrid,
					OngekiTimelineObjectBase timelineObject => timelineObject.TGrid,
					_ => default
				};

				if (endTGrid is not null && endTGrid > max)
					max = endTGrid;
			}

			return max;
		}

		private void SetPreviewScrollPosition(double scrollPositionMs)
		{
			previewScrollPositionMs = Math.Min(VerticalScrollMaximum, Math.Max(0, scrollPositionMs));

			var fixedAudioTimeMs = Math.Min(previewScrollPositionMs, Math.Max(0, GetAudioDuration().TotalMilliseconds));
			CurrentPlayTime = TimeSpan.FromMilliseconds(fixedAudioTimeMs);

			var viewportTGrid = GetViewportTGrid();
			var val = ConvertTGridToY_PreviewMode(viewportTGrid);
			val = Math.Min(TotalDurationHeight, Math.Max(0, val));
			scrollViewerVerticalOffset = val;

			NotifyOfPropertyChange(() => ReverseScrollViewerVerticalOffset);
			NotifyOfPropertyChange(() => ReverseVerticalScrollValue);
		}

		private TimeSpan GetAudioDuration()
		{
			var projectDuration = EditorProjectData?.AudioDuration ?? TimeSpan.Zero;
			return projectDuration > TimeSpan.Zero ? projectDuration : AudioPlayer?.Duration ?? TimeSpan.Zero;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public TGrid GetCurrentTGrid() => ConvertAudioTimeToTGrid(CurrentPlayTime);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public TGrid GetViewportTGrid() => IsPreviewMode
			? ConvertAudioTimeToTGrid(TimeSpan.FromMilliseconds(previewScrollPositionMs))
			: GetCurrentTGrid();

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public TimeSpan GetViewportAudioTime() => IsPreviewMode
			? TimeSpan.FromMilliseconds(previewScrollPositionMs)
			: CurrentPlayTime;
	}
}
