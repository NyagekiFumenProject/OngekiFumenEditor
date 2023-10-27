using System;
using System.Collections.Generic;
using System.Text;

namespace OngekiFumenEditor.Kernel.Graphics
{
	public interface IPerfomenceMonitor
	{
		public interface IRenderPerformenceStatisticsData
		{
			public double AveSpendTicks { get; }
			public double MostSpendTicks { get; }
			public int AveDrawCall { get; }
			public long MostUIRenderSpendTicks { get; }
			public double AveUIRenderSpendTicks { get; }
		}

		public interface IDrawingPerformenceStatisticsData
		{
			public record PerformenceItem(string Name, double AveSpendTicks, int AveDrawCall);
			public IEnumerable<PerformenceItem> PerformenceRanks { get; }
			public double AveSpendTicks { get; }
			public double MostSpendTicks { get; }
		}

		void OnBeforeRender();
		void OnBeginDrawing(IDrawing drawing);
		void OnBeginTargetDrawing(IDrawingTarget drawing);

		void CountDrawCall(IDrawing drawing);

		void OnAfterTargetDrawing(IDrawingTarget drawing);
		void OnAfterDrawing(IDrawing drawing);
		void OnAfterRender();

		IDrawingPerformenceStatisticsData GetDrawingPerformenceData();
		IDrawingPerformenceStatisticsData GetDrawingTargetPerformenceData();
		IRenderPerformenceStatisticsData GetRenderPerformenceData();

		void FormatStatistics(StringBuilder builder);

		void Clear();
		void PostUIRenderTime(TimeSpan ts);
	}
}
