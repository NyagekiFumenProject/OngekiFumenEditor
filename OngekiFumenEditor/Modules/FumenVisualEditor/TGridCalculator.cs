using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.Collections;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using OngekiFumenEditor.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Controls;

namespace OngekiFumenEditor.Modules.FumenVisualEditor
{
	public static class TGridCalculator
	{
		#region AudioTime -> TGrid

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static TGrid ConvertAudioTimeToTGrid(TimeSpan audioTime, FumenVisualEditorViewModel editor)
		   => ConvertAudioTimeToTGrid(audioTime, editor.Fumen.BpmList);
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static TGrid ConvertAudioTimeToTGrid(TimeSpan audioTime, BpmList bpmList)
		{
			var positionBpmList = GetAllBpmUniformPositionList(bpmList);

			//获取pickY对应的bpm和bpm起始位置
			(var pickStartY, var pickBpm) = positionBpmList.LastOrDefault(x => x.audioTime <= audioTime);
			if (pickBpm is null)
				return default;
			var relativeBpmLenOffset = pickBpm.LengthConvertToOffset((audioTime - pickStartY).TotalMilliseconds);

			var pickTGrid = pickBpm.TGrid + relativeBpmLenOffset;
			return pickTGrid;
		}

		#endregion

		#region TGrid -> AudioTime

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static TimeSpan ConvertTGridToAudioTime(TGrid tGrid, FumenVisualEditorViewModel editor)
			=> ConvertTGridToAudioTime(tGrid, editor.Fumen.BpmList);
		public static TimeSpan ConvertTGridToAudioTime(TGrid tGrid, BpmList bpmList)
		{
			var positionBpmList = GetAllBpmUniformPositionList(bpmList);

			//获取pickY对应的bpm和bpm起始位置
			(var audioTimeMsecBase, var pickBpm) = positionBpmList.LastOrDefault(x => x.bpm.TGrid <= tGrid);
			if (pickBpm is null)
				if (positionBpmList.FirstOrDefault().bpm?.TGrid is TGrid first && tGrid < first)
					return TimeSpan.FromMilliseconds(0);
				else
					return default;
			var relativeBpmLenOffset = TimeSpan.FromMilliseconds(MathUtils.CalculateBPMLength(pickBpm, tGrid));

			var audioTimeMsec = audioTimeMsecBase + relativeBpmLenOffset;
			return audioTimeMsec;
		}

		#endregion

		#region [DesignMode] Y -> TGrid

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static TGrid ConvertYToTGrid_DesignMode(double pickY, FumenVisualEditorViewModel editor)
			=> ConvertYToTGrid_DesignMode(pickY, editor.Fumen.Soflans, editor.Fumen.BpmList, editor.Setting.VerticalDisplayScale);
		private static TGrid ConvertYToTGrid_DesignMode(double pickY, SoflanList soflanList, BpmList bpmList, double scale)
		{
			pickY = pickY / scale;
			var list = soflanList.GetCachedSoflanPositionList_DesignMode(bpmList);

			//获取pickY对应的bpm和bpm起始位置
			var pos = list.LastOrDefault(x => x.Y <= pickY);
			if (pos.Bpm is null)
				return default;
			var absSpeed = Math.Abs(pos.Speed);
			var relativeBpmLenOffset = pos.Bpm.LengthConvertToOffset((pickY - pos.Y) / absSpeed);

			var pickTGrid = pos.TGrid + relativeBpmLenOffset;
			return pickTGrid;
		}

		#endregion

		#region [DesignMode] Y -> AudioTime

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static TimeSpan ConvertYToAudioTime_DesignMode(double pickY, FumenVisualEditorViewModel editor)
			=> ConvertYToAudioTime_DesignMode(pickY, editor.Fumen.Soflans, editor.Fumen.BpmList, editor.Setting.VerticalDisplayScale);
		private static TimeSpan ConvertYToAudioTime_DesignMode(double pickY, SoflanList soflanList, BpmList bpmList, double scale)
		{
			var tGrid = ConvertYToTGrid_DesignMode(pickY, soflanList, bpmList, scale);
			if (tGrid is null)
				return default;
			return ConvertTGridToAudioTime(tGrid, bpmList);
		}

		#endregion

		#region [DesignMode] AudioTime -> Y

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static double ConvertAudioTimeToY_DesignMode(TimeSpan audioTime, FumenVisualEditorViewModel editor)
			=> ConvertTGridToY_DesignMode(ConvertAudioTimeToTGrid(audioTime, editor), editor);

		#endregion

		#region [DesignMode] TGrid -> Y

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static double ConvertTGridToY_DesignMode(TGrid tGrid, FumenVisualEditorViewModel editor)
			=> ConvertTGridToY_DesignMode(tGrid, editor.Fumen.Soflans, editor.Fumen.BpmList, editor.Setting.VerticalDisplayScale);
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static double ConvertTGridToY_DesignMode(TGrid tGrid, SoflanList soflanList, BpmList bpmList, double scale)
			=> ConvertTGridUnitToY_DesignMode(tGrid.TotalUnit, soflanList, bpmList, scale);
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static double ConvertTGridUnitToY_DesignMode(double tGridUnit, FumenVisualEditorViewModel editor)
			=> ConvertTGridUnitToY_DesignMode(tGridUnit, editor.Fumen.Soflans, editor.Fumen.BpmList, editor.Setting.VerticalDisplayScale);
		public static double ConvertTGridUnitToY_DesignMode(double tGridUnit, SoflanList soflanList, BpmList bpmList, double scale)
		{
			var positionBpmList = soflanList.GetCachedSoflanPositionList_DesignMode(bpmList);

			//获取pickY对应的bpm和bpm起始位置
			var pos = positionBpmList.LastOrDefaultByBinarySearch(tGridUnit, x => x.TGrid.TotalUnit);

			if (pos.Bpm is null)
				return default;

			var relativeBpmLenOffset = MathUtils.CalculateBPMLength(pos.TGrid.TotalUnit, tGridUnit, pos.Bpm.BPM);

			var absSpeed = Math.Abs(pos.Speed);
			var y = (pos.Y + relativeBpmLenOffset * absSpeed) * scale;

			return y;
		}

		#endregion

		#region [PreviewMode] Y -> TGrid[]

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static IEnumerable<TGrid> ConvertYToTGrid_PreviewMode(double pickY, FumenVisualEditorViewModel editor)
			=> ConvertYToTGrid_PreviewMode(pickY, editor.Fumen.Soflans, editor.Fumen.BpmList, editor.Setting.VerticalDisplayScale);
		private static IEnumerable<TGrid> ConvertYToTGrid_PreviewMode(double pickY, SoflanList soflanList, BpmList bpmList, double scale)
		{
			var r = soflanList.GetVisibleRanges_PreviewMode(pickY, 0, 0, bpmList, scale);
			var result = r.OrderBy(x => x.minTGrid).Select(x => x.minTGrid);
			return result;
		}

		#endregion

		#region [PreviewMode] TGrid -> Y

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static double ConvertTGridToY_PreviewMode(TGrid tGrid, FumenVisualEditorViewModel editor)
			=> ConvertTGridToY_PreviewMode(tGrid, editor.Fumen.Soflans, editor.Fumen.BpmList, editor.Setting.VerticalDisplayScale);
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static double ConvertTGridToY_PreviewMode(TGrid tGrid, SoflanList soflanList, BpmList bpmList, double scale)
			=> ConvertTGridUnitToY_PreviewMode(tGrid.TotalUnit, soflanList, bpmList, scale);
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static double ConvertTGridUnitToY_PreviewMode(double tGridUnit, FumenVisualEditorViewModel editor)
			=> ConvertTGridUnitToY_PreviewMode(tGridUnit, editor.Fumen.Soflans, editor.Fumen.BpmList, editor.Setting.VerticalDisplayScale);
		public static double ConvertTGridUnitToY_PreviewMode(double tGridUnit, SoflanList soflanList, BpmList bpmList, double scale)
		{
			var positionBpmList = soflanList.GetCachedSoflanPositionList_PreviewMode(bpmList);

			//获取pickY对应的bpm和bpm起始位置
			var pos = positionBpmList.LastOrDefaultByBinarySearch(tGridUnit, x => x.TGrid.TotalUnit);
			if (pos.Bpm is null)
				return default;

			var relativeBpmLenOffset = MathUtils.CalculateBPMLength(pos.TGrid.TotalUnit, tGridUnit, pos.Bpm.BPM);

			var y = (pos.Y + relativeBpmLenOffset * pos.Speed) * scale;
			return y;
		}

		#endregion

		#region [PreviewMode] AudioTime -> Y

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static double ConvertAudioTimeToY_PreviewMode(TimeSpan audioTime, FumenVisualEditorViewModel editor)
			=> ConvertTGridToY_PreviewMode(ConvertAudioTimeToTGrid(audioTime, editor), editor);

		#endregion

		#region [PreviewMode] VisbleTimelines

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static IEnumerable<(TGrid tGrid, double y, int beatIndex, MeterChange meter, BPMChange bpm)> GetVisbleTimelines_PreviewMode(FumenVisualEditorViewModel editor)
			=> GetVisbleTimelines_PreviewMode(editor.Fumen.Soflans, editor.Fumen.BpmList, editor.Fumen.MeterChanges, editor.Rect.MinY, editor.Rect.MaxY, editor.Setting.JudgeLineOffsetY, editor.Setting.BeatSplit, editor.Setting.VerticalDisplayScale);
		public static IEnumerable<(TGrid tGrid, double y, int beatIndex, MeterChange meter, BPMChange bpm)> GetVisbleTimelines_PreviewMode(SoflanList soflans, BpmList bpmList, MeterChangeList meterList, double currentY, double viewHeight, double judgeLineOffsetY, int beatSplit, double scale)
		{
			var tGridRanges = soflans.GetVisibleRanges_PreviewMode(currentY, viewHeight, judgeLineOffsetY, bpmList, scale);

			foreach (var range in tGridRanges)
			{
				var rMinY = ConvertTGridToY_DesignMode(range.minTGrid, soflans, bpmList, scale);
				var rMaxY = ConvertTGridToY_DesignMode(range.maxTGrid, soflans, bpmList, scale);

				var queryFromDesignMode = GetVisbleTimelines_DesignMode(soflans, bpmList, meterList, rMinY, rMaxY, judgeLineOffsetY, 1, scale);
				foreach (var item in queryFromDesignMode)
				{
					if (item.beatIndex != 0)
						continue;

					var cpItem = item;
					cpItem.y = ConvertTGridToY_PreviewMode(cpItem.tGrid, soflans, bpmList, scale);

					yield return cpItem;
				}
			}
		}

		#endregion

		#region [DesignMode] VisbleTimelines

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static IEnumerable<(TGrid tGrid, double y, int beatIndex, MeterChange meter, BPMChange bpm)> GetVisbleTimelines_DesignMode(FumenVisualEditorViewModel editor)
			=> GetVisbleTimelines_DesignMode(editor.Fumen.Soflans, editor.Fumen.BpmList, editor.Fumen.MeterChanges, editor.Rect.MinY, editor.Rect.MaxY, editor.Setting.JudgeLineOffsetY, editor.Setting.BeatSplit, editor.Setting.VerticalDisplayScale);
		public static IEnumerable<(TGrid tGrid, double y, int beatIndex, MeterChange meter, BPMChange bpm)> GetVisbleTimelines_DesignMode(SoflanList soflans, BpmList bpmList, MeterChangeList meterList, double minVisibleCanvasY, double maxVisibleCanvasY, double judgeLineOffsetY, int beatSplit, double scale)
		{
			minVisibleCanvasY = Math.Max(0, minVisibleCanvasY);
			var minVisibleCanvasTGrid = ConvertYToTGrid_DesignMode(minVisibleCanvasY, soflans, bpmList, scale);

			//划线的中止位置
			var endTGrid = ConvertYToTGrid_DesignMode(maxVisibleCanvasY, soflans, bpmList, scale);
			//可显示划线的起始位置 
			var currentTGridBaseOffset = ConvertYToTGrid_DesignMode(minVisibleCanvasY, soflans, bpmList, scale)
				?? ConvertYToTGrid_DesignMode(minVisibleCanvasY + judgeLineOffsetY, soflans, bpmList, 1);

			var timeSignatures = meterList.GetCachedAllTimeSignatureUniformPositionList(bpmList);
			//快速定位,尽量避免计算完全不用画的timesignature(
			var currentTimeSignatureIndex = timeSignatures.LastOrDefaultIndexByBinarySearch(minVisibleCanvasTGrid, x => x.startTGrid);

			//钦定好要画的起始timeSignatrue
			(TimeSpan audioTime, TGrid startTGrid, MeterChange meter, BPMChange bpm) currentTimeSignature = timeSignatures[currentTimeSignatureIndex];

			if (endTGrid is null)
				yield break;

			while (currentTGridBaseOffset is not null)
			{
				var nextTimeSignatureIndex = currentTimeSignatureIndex + 1;
				var nextTimeSignature = timeSignatures.Count > nextTimeSignatureIndex ? timeSignatures[nextTimeSignatureIndex] : default;

				//钦定好要画的相对于当前timeSignature的偏移Y，节拍信息，节奏速度
				(_, var currentTGridBase, var currentMeter, var currentBpm) = currentTimeSignature;
				//var currentStartY = ConvertTGridToY_DesignMode(currentTGridBase, soflans, bpmList, scale, tUnitLength);
				(_, var nextTGridBase, _, var nextBpm) = nextTimeSignature;

				//计算每一拍的(grid)长度
				var resT = currentTGridBase.ResT;
				var beatCount = currentMeter.BunShi * beatSplit;
				var lengthPerBeat = resT * 1.0d / beatCount;

				//这里也可以跳过添加完全看不到的线
				var diff = currentTGridBaseOffset - currentTGridBase;
				var totalGrid = diff.Unit * resT + diff.Grid;
				var i = (int)Math.Max(0, totalGrid / lengthPerBeat);

				//特殊处理beatCount=0的情况
				if (beatCount == 0)
				{
					var y = ConvertTGridToY_DesignMode(currentTGridBase, soflans, bpmList, 1);
					yield return (currentTGridBase, y * scale, 0, currentMeter, currentBpm);
				}
				else
				{
					while (true)
					{
						var tGrid = currentTGridBase + new GridOffset(0, (int)(lengthPerBeat * i));
						//因为是不存在跨bpm长度计算，可以直接CalculateBPMLength(...)计算而不是TGridCalculator.ConvertTGridToY(...);
						var y = ConvertTGridToY_DesignMode(tGrid, soflans, bpmList, 1);

						//超过当前timeSignature范围，切换到下一个timeSignature画新的线
						if (nextBpm is not null && tGrid >= nextTGridBase)
							break;
						//超过编辑器谱面范围，后面都不用画了
						if (tGrid > endTGrid)
							yield break;
						//节奏线在最低可见线的后面
						if (tGrid < currentTGridBaseOffset)
						{
							i++;
							continue;
						}

						yield return (tGrid, y * scale, i % beatCount, currentMeter, currentBpm);
						i++;
					}
				}

				currentTGridBaseOffset = nextTGridBase;
				currentTimeSignatureIndex = nextTimeSignatureIndex;
				currentTimeSignature = timeSignatures.Count > currentTimeSignatureIndex ? timeSignatures[currentTimeSignatureIndex] : default;
			}
		}

		#endregion

		public static (TimeSpan audioTime, TGrid startTGrid, MeterChange meter, BPMChange bpm) GetCurrentTimeSignature(TGrid tGrid, FumenVisualEditorViewModel editor)
		{
			return GetCurrentTimeSignature(tGrid, editor.Fumen.BpmList, editor.Fumen.MeterChanges);
		}

		public static (TimeSpan audioTime, TGrid startTGrid, MeterChange meter, BPMChange bpm) GetCurrentTimeSignature(TGrid tGrid, BpmList bpmList, MeterChangeList meterList)
		{
			var timeSignatures = meterList.GetCachedAllTimeSignatureUniformPositionList(bpmList);
			var idx = timeSignatures.BinarySearchBy(tGrid, x => x.startTGrid);
			idx = idx < 0 ? Math.Max(0, ((~idx) - 1)) : idx;
			return timeSignatures[idx];
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static List<(TimeSpan audioTime, BPMChange bpm)> GetAllBpmUniformPositionList(FumenVisualEditorViewModel editor)
			=> GetAllBpmUniformPositionList(editor.Fumen.BpmList);
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static List<(TimeSpan audioTime, BPMChange bpm)> GetAllBpmUniformPositionList(BpmList bpmList)
			=> bpmList.GetCachedAllBpmUniformPositionList();

		public static double CalculateOffsetYPerBeat(BPMChange bpm, MeterChange meter, int beatSplit, double scale)
		{
			//计算每一拍的(grid)长度
			var resT = bpm.TGrid.ResT;
			var beatCount = meter.BunShi * beatSplit;
			var lengthPerBeat = (resT * 1.0d / beatCount);

			return MathUtils.CalculateBPMLength(bpm, bpm.TGrid + new GridOffset(0, (int)lengthPerBeat)) * scale;
		}

		/// <summary>
		/// 计算在y±range内，最近的节奏线
		/// </summary>
		/// <param name="y"></param>
		/// <param name="range"></param>
		/// <param name="bpmList"></param>
		/// <param name="meterChanges"></param>
		/// <param name="beatSplit"></param>
		/// <param name="tUnitLength"></param>
		/// <returns></returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static (TGrid tGrid, double y, int beatIndex) TryPickMagneticBeatTime_DesignMode(float y, float range, SoflanList soflans, BpmList bpmList, MeterChangeList meterChanges, int beatSplit, double scale)
		{
			var result = GetVisbleTimelines_DesignMode(soflans, bpmList, meterChanges, y - range, y + range, 0, beatSplit, scale).MinByOrDefault(x => Math.Abs(x.y - y));
			return (result.tGrid, result.y, result.beatIndex);
		}
		/// <summary>
		/// 计算在y±range内，最近的节奏线
		/// </summary>
		/// <param name="y"></param>
		/// <param name="range"></param>
		/// <param name="editor"></param>
		/// <param name="tUnitLength"></param>
		/// <returns></returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static (TGrid tGrid, double y, int beatIndex) TryPickMagneticBeatTime(float y, float range, FumenVisualEditorViewModel editor)
			=> TryPickMagneticBeatTime_DesignMode(y, range, editor.Fumen.Soflans, editor.Fumen.BpmList, editor.Fumen.MeterChanges, editor.Setting.BeatSplit, editor.Setting.VerticalDisplayScale);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static (TGrid tGrid, double y, int beatIndex) TryPickClosestBeatTime(float y, FumenVisualEditorViewModel editor)
			=> TryPickClosestBeatTime_DesignMode(y, editor.Fumen.Soflans, editor.Fumen.BpmList, editor.Fumen.MeterChanges, editor.Setting.BeatSplit, editor.Setting.VerticalDisplayScale);

		/// <summary>
		/// 获取某个时间点上最近的节奏点
		/// </summary>
		/// <param name="y"></param>
		/// <param name="editor"></param>
		/// <param name="tUnitLength"></param>
		/// <returns></returns>
		public static (TGrid tGrid, double y, int beatIndex) TryPickClosestBeatTime_DesignMode(float y, SoflanList soflans, BpmList bpmList, MeterChangeList meterChanges, int beatSplit, double scale)
		{
			/**
             ...
              |
              |
            __|__ 
              |          downFirst
              |
            ------ prevY
             */
			var timeSignatures = meterChanges.GetCachedAllTimeSignatureUniformPositionList(bpmList);
			//var tGrid = ConvertAudioTimeToTGrid(audioTime, bpmList, tUnitLength);
			//var y = ConvertTGridToY_DesignMode(tGrid, soflans, bpmList, scale, tUnitLength);
			var tGrid = ConvertYToTGrid_DesignMode(y, soflans, bpmList, scale);
			if (tGrid is null)
				return default;
			var audioTime = ConvertTGridToAudioTime(tGrid, bpmList);

			(var prevAudioTime, _, var meter, var bpm) = timeSignatures.LastOrDefault(x => x.audioTime <= audioTime);
			var prevTGrid = ConvertAudioTimeToTGrid(prevAudioTime, bpmList);
			var prevY = ConvertTGridToY_DesignMode(prevTGrid, soflans, bpmList, scale);

			var downFirst = GetVisbleTimelines_DesignMode(soflans, bpmList, meterChanges, prevY, y, 0, beatSplit, scale)
				.LastOrDefault();
			var nextFirst = GetVisbleTimelines_DesignMode(soflans, bpmList, meterChanges, y, y + CalculateOffsetYPerBeat(bpm, meter, beatSplit, scale), 0, beatSplit, scale)
				.FirstOrDefault();

			if (Math.Abs(downFirst.y - y) < Math.Abs(nextFirst.y - y))
				return (downFirst.tGrid, downFirst.y, downFirst.beatIndex);
			return (nextFirst.tGrid, nextFirst.y, nextFirst.beatIndex);
		}

        internal static object ConvertTGridUnitToY_DesignMode(double v, SoflanList soflans, BpmList bpmList, object verticalScale)
        {
            throw new NotImplementedException();
        }
    }
}
