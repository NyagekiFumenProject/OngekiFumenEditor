using OngekiFumenEditor.Base.Collections;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using static OngekiFumenEditor.Kernel.Graphics.IPerfomenceMonitor;
using static OngekiFumenEditor.Kernel.Graphics.IPerfomenceMonitor.IDrawingPerformenceStatisticsData;

namespace OngekiFumenEditor.Kernel.Graphics.Performence
{
#if DEBUG
	[Export(typeof(IPerfomenceMonitor))]
#endif
	[PartCreationPolicy(CreationPolicy.NonShared)]
	public class DefaultDebugPerfomenceMonitor : IPerfomenceMonitor
	{
		const int RECORD_LENGTH = 10;

		private class DrawingPerformenceData
		{
			public string Name { get; init; }
			public int DrawCallCount { get; set; }
			public long OnBeginDrawingTicks { get; set; }

			public FixedSizeCycleCollection<long> DrawingSpendTicks { get; } = new(RECORD_LENGTH);
			public FixedSizeCycleCollection<long> DrawCall { get; } = new(RECORD_LENGTH);

			public void ClearAll()
			{
				DrawingSpendTicks.Clear();
				OnBeginDrawingTicks = default;
				DrawCallCount = default;
			}
		}

		private class DrawingTargetPerformenceData : DrawingPerformenceData
		{

		}

		private Dictionary<IDrawing, DrawingPerformenceData> drawDataMap = new();
		private Dictionary<IDrawingTarget, DrawingTargetPerformenceData> drawTargetDataMap = new();
		private Stopwatch timer = new Stopwatch();

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private DrawingPerformenceData GetDrawingPerformenceData(IDrawing d) => drawDataMap.TryGetValue(d, out var data) ? data : (drawDataMap[d] = new DrawingPerformenceData() { Name = d.GetType().Name });

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private DrawingTargetPerformenceData GetDrawingTargetPerformenceData(IDrawingTarget d) => drawTargetDataMap.TryGetValue(d, out var data) ? data : (drawTargetDataMap[d] = new DrawingTargetPerformenceData() { Name = d.GetType().Name });

		private FixedSizeCycleCollection<long> RenderSpendTicks { get; } = new(RECORD_LENGTH);
		private FixedSizeCycleCollection<long> UIRenderSpendTicks { get; } = new(RECORD_LENGTH);
		private FixedSizeCycleCollection<long> TotalDrawCall { get; } = new(RECORD_LENGTH);
		private long currentDrawCall = 0;
		private long currentBeginRenderTick = 0;

		public void OnBeforeRender()
		{
			currentDrawCall = 0;
			timer.Restart();
			//currentBeginRenderTick = timer.ElapsedTicks;
		}

		public void OnBeginTargetDrawing(IDrawingTarget drawingTarget)
		{
			var data = GetDrawingTargetPerformenceData(drawingTarget);
			data.OnBeginDrawingTicks = timer.ElapsedTicks;
		}

		public void OnBeginDrawing(IDrawing drawing)
		{
			var data = GetDrawingPerformenceData(drawing);
			data.OnBeginDrawingTicks = timer.ElapsedTicks;
		}

		public void CountDrawCall(IDrawing drawing)
		{
			var data = GetDrawingPerformenceData(drawing);
			data.DrawCallCount++;
			currentDrawCall++;
		}

		public void OnAfterDrawing(IDrawing drawing)
		{
			var data = GetDrawingPerformenceData(drawing);
			var tickDiff = timer.ElapsedTicks - data.OnBeginDrawingTicks;
			data.DrawingSpendTicks.Enqueue(tickDiff);
			data.DrawCall.Enqueue(data.DrawCallCount);
		}

		public void OnAfterTargetDrawing(IDrawingTarget drawing)
		{
			var data = GetDrawingTargetPerformenceData(drawing);
			var tickDiff = timer.ElapsedTicks - data.OnBeginDrawingTicks;
			data.DrawingSpendTicks.Enqueue(tickDiff);
		}

		public void OnAfterRender()
		{
			timer.Stop();
			RenderSpendTicks.Enqueue(timer.ElapsedTicks - currentBeginRenderTick);
			TotalDrawCall.Enqueue(currentDrawCall);
			foreach (var data in drawDataMap.Values)
				data.DrawCallCount = 0;
		}

		public void Clear()
		{
			drawDataMap.Clear();
			drawTargetDataMap.Clear();
		}

		public struct DrawingPerformenceStatisticsData : IDrawingPerformenceStatisticsData
		{
			public List<PerformenceItem> PerformenceRankList { get; set; }

			public IEnumerable<PerformenceItem> PerformenceRanks => PerformenceRankList ?? Enumerable.Empty<PerformenceItem>();

			public double AveSpendTicks { get; set; }

			public double MostSpendTicks { get; set; }
		}

		private IDrawingPerformenceStatisticsData StatisticsPerformenceData(IEnumerable<DrawingPerformenceData> dataList)
		{
			if (dataList.Count() == 0)
				return default;

			var ave = dataList.Select(x => x.DrawingSpendTicks.Average()).Average();
			var most = dataList.SelectMany(x => x.DrawingSpendTicks).GroupBy(x => (int)x).OrderByDescending(x => x.Key).SelectMany(x => x).Average();

			var list = dataList
				.Select(x => new { TotalCost = x.DrawingSpendTicks.Sum(), Obj = x })
				.OrderByDescending(x => x.TotalCost)
				.Select(x => new PerformenceItem(x.Obj.Name, x.Obj.DrawingSpendTicks.Average(), (int)x.Obj.DrawCall.Average()))
				.ToList();

			return new DrawingPerformenceStatisticsData()
			{
				AveSpendTicks = ave,
				MostSpendTicks = most,
				PerformenceRankList = list
			};
		}

		public IDrawingPerformenceStatisticsData GetDrawingPerformenceData()
		{
			return StatisticsPerformenceData(drawDataMap.Values);
		}

		public IDrawingPerformenceStatisticsData GetDrawingTargetPerformenceData()
		{
			return StatisticsPerformenceData(drawTargetDataMap.Values);
		}

		public IRenderPerformenceStatisticsData GetRenderPerformenceData()
		{
			return new RenderPerformenceStatisticsData()
			{
				AveSpendTicks = RenderSpendTicks.Average(),
				AveUIRenderSpendTicks = UIRenderSpendTicks.Average(),
				MostUIRenderSpendTicks = UIRenderSpendTicks.GroupBy(x => x).OrderByDescending(x => x.Key).FirstOrDefault().Key,
				MostSpendTicks = RenderSpendTicks.GroupBy(x => x).OrderByDescending(x => x.Key).FirstOrDefault().Key,
				AveDrawCall = (int)TotalDrawCall.Average()
			};
		}

		public void PostUIRenderTime(TimeSpan ts)
		{
			UIRenderSpendTicks.Enqueue(ts.Ticks);
		}

		public void FormatStatistics(StringBuilder builder)
		{
			var drawing = GetDrawingPerformenceData();
			var drawingTarget = GetDrawingTargetPerformenceData();

			if (drawing is null || drawingTarget is null)
				return;

			var drawingTop = drawing.PerformenceRanks.FirstOrDefault();
			var render = GetRenderPerformenceData();

			string formatFPS(double ticks) => $"{1.0 / TimeSpan.FromTicks((int)ticks).TotalSeconds,7:0.00}";
			string formatMSec(double ticks) => $"{TimeSpan.FromTicks((int)ticks).TotalMilliseconds:F2}";

			void dip(PerformenceItem p, int i)
			{
				if (p is null)
					return;
				builder.AppendLine($"D.TOP{i}:{p.Name} {p.AveDrawCall} dc ({formatMSec(p.AveSpendTicks)}ms) ");
			}

			void dipt(PerformenceItem p, int i)
			{
				if (p is null)
					return;
				builder.AppendLine($"DT.TOP{i}:{p.Name} {formatMSec(p.AveSpendTicks)}ms ");
			}

			builder.AppendLine($"UI.FPS:{formatFPS(render.AveUIRenderSpendTicks)}({formatFPS(render.MostUIRenderSpendTicks)}) / R.FPS {formatFPS(render.AveSpendTicks)}({formatFPS(render.MostSpendTicks)}) D.FPS:{formatFPS(drawing.AveSpendTicks)}({formatFPS(drawing.MostSpendTicks)})");
			builder.AppendLine($"DC:{render.AveDrawCall,6} D.Top.DC:{drawingTop.AveDrawCall,6}");
			builder.AppendLine();
			dip(drawing.PerformenceRanks.ElementAtOrDefault(0), 1);
			dip(drawing.PerformenceRanks.ElementAtOrDefault(1), 2);
			dip(drawing.PerformenceRanks.ElementAtOrDefault(2), 3);
			builder.AppendLine();
			dipt(drawingTarget.PerformenceRanks.ElementAtOrDefault(0), 1);
			dipt(drawingTarget.PerformenceRanks.ElementAtOrDefault(1), 2);
			dipt(drawingTarget.PerformenceRanks.ElementAtOrDefault(2), 3);
		}
	}
}
