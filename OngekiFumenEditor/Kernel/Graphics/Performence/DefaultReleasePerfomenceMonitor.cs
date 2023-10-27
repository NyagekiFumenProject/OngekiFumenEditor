using OngekiFumenEditor.Base.Collections;
using System;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using static OngekiFumenEditor.Kernel.Graphics.IPerfomenceMonitor;

namespace OngekiFumenEditor.Kernel.Graphics.Performence
{
#if !DEBUG
    [Export(typeof(IPerfomenceMonitor))]
#endif
	[PartCreationPolicy(CreationPolicy.NonShared)]
	public class DefaultReleasePerfomenceMonitor : IPerfomenceMonitor
	{
		const int RECORD_LENGTH = 10;
		private Stopwatch timer = new Stopwatch();

		private FixedSizeCycleCollection<long> RenderSpendTicks { get; } = new(RECORD_LENGTH);
		private FixedSizeCycleCollection<long> UIRenderSpendTicks { get; } = new(RECORD_LENGTH);
		private FixedSizeCycleCollection<long> TotalDrawCall { get; } = new(RECORD_LENGTH);

		private long currentDrawCall = 0;
		private long currentBeginRenderTick = 0;

		[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
		public void Clear() { }

		[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
		public void CountDrawCall(IDrawing drawing) => currentDrawCall++;

		[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
		public IDrawingPerformenceStatisticsData GetDrawingPerformenceData()
		{
			return default;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
		public IDrawingPerformenceStatisticsData GetDrawingTargetPerformenceData()
		{
			return default;
		}

		[MethodImpl(MethodImplOptions.AggressiveOptimization)]
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

		[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
		public void OnAfterDrawing(IDrawing drawing)
		{

		}

		[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
		public void OnAfterRender()
		{
			timer.Stop();
			RenderSpendTicks.Enqueue(timer.ElapsedTicks - currentBeginRenderTick);
			TotalDrawCall.Enqueue(currentDrawCall);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
		public void OnAfterTargetDrawing(IDrawingTarget drawing)
		{

		}

		[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
		public void OnBeforeRender()
		{
			timer.Restart();
			currentDrawCall = 0;
			currentBeginRenderTick = 0;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
		public void OnBeginDrawing(IDrawing drawing)
		{

		}

		[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
		public void OnBeginTargetDrawing(IDrawingTarget drawing)
		{

		}

		[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
		public void PostUIRenderTime(TimeSpan ts)
		{
			UIRenderSpendTicks.Enqueue(ts.Ticks);
		}

		public void FormatStatistics(StringBuilder builder)
		{
			var render = GetRenderPerformenceData();

			string formatFPS(double ticks) => $"{1.0 / TimeSpan.FromTicks((int)ticks).TotalSeconds,7:0.00}";

			builder.AppendLine($"UI.FPS:{formatFPS(render.AveUIRenderSpendTicks)}({formatFPS(render.MostUIRenderSpendTicks)}) / R.FPS {formatFPS(render.AveSpendTicks)}({formatFPS(render.MostSpendTicks)})");
			builder.AppendLine($"DC:{render.AveDrawCall,6}");
		}
	}
}
