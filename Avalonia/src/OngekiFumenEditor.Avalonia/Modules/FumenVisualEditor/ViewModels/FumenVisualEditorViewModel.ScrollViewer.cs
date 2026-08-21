using Gekimini.Avalonia.Framework;
using OngekiFumenEditor.Avalonia.Base;
using OngekiFumenEditor.Avalonia.Utils;
using System;
using System.Linq;
using System.Runtime.CompilerServices;

namespace OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.ViewModels
{
	public partial class FumenVisualEditorViewModel : DocumentViewModelBase
	{
		private double totalDurationHeight;
		public double TotalDurationHeight
		{
			get => totalDurationHeight;
			set
			{
				value = Math.Max(value, ViewHeight);
				//Log.LogDebug($"TotalDurationHeight {TotalDurationHeight} -> {value}");
				if (SetProperty(ref totalDurationHeight, value))
					OnPropertyChanged(nameof(IsVerticalScrollBarVisible));
			}
		}

		public bool IsVerticalScrollBarVisible => TotalDurationHeight > ViewHeight;

		private double scrollViewerVerticalOffset;
		public double ScrollViewerVerticalOffset
		{
			get => scrollViewerVerticalOffset;
			/*
            set
            {
                var val = Math.Min(TotalDurationHeight, Math.Max(0, value));
                Func<double, FumenVisualEditorViewModel, TGrid> convertToTGrid = IsDesignMode ? TGridCalculator.ConvertYToTGrid_DesignMode : TGridCalculator.ConvertYToTGrid_PreviewMode;

                SetProperty(ref scrollViewerVerticalOffset, val);
                OnPropertyChanged(nameof(ReverseScrollViewerVerticalOffset));
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
				if (IsDisposed || EditorContext?.Fumen is null)
					return;

				//ScrollViewerVerticalOffset = TotalDurationHeight - value;
				var val = TotalDurationHeight - value;
				if (IsDesignMode)
				{
					var audioTime = TGridCalculator.ConvertYToAudioTime_DesignMode(val, this);
					ScrollTo(audioTime);
				}
				else
				{
					var curTGrid = GetCurrentTGrid();
					var nextTGrid = TGridCalculator.ConvertYToTGrid_PreviewMode(val, this).OrderBy(x => Math.Abs(x.TotalGrid - curTGrid.TotalGrid)).FirstOrDefault();

					if (nextTGrid is not null)
					{
						var audioTime = TGridCalculator.ConvertTGridToAudioTime(nextTGrid, this);
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
			var audioTime = TGridCalculator.ConvertTGridToAudioTime(startTGrid, this);
			ScrollTo(audioTime);
		}

		public void ScrollTo(TimeSpan audioTime)
		{
			var fixedAudioTime = MathUtils.Max(TimeSpan.Zero, MathUtils.Min(audioTime, EditorContext.ProjectData.AudioDuration));
			CurrentPlayTime = fixedAudioTime;

			var val = IsDesignMode ?
				TGridCalculator.ConvertAudioTimeToY_DesignMode(fixedAudioTime, this) :
				TGridCalculator.ConvertAudioTimeToY_PreviewMode(fixedAudioTime, this);
			val = Math.Min(TotalDurationHeight, Math.Max(0, val));

			scrollViewerVerticalOffset = val;
			OnPropertyChanged(nameof(ReverseScrollViewerVerticalOffset));
		}

		#endregion

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public TGrid GetCurrentTGrid() => TGridCalculator.ConvertAudioTimeToTGrid(CurrentPlayTime, this);
	}
}




