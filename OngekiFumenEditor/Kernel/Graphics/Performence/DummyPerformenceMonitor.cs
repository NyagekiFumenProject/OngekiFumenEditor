using System;
using System.Collections.Generic;
using System.Text;
using static OngekiFumenEditor.Kernel.Graphics.IPerfomenceMonitor;
using static OngekiFumenEditor.Kernel.Graphics.IPerfomenceMonitor.IDrawingPerformenceStatisticsData;

namespace OngekiFumenEditor.Kernel.Graphics.Performence
{
	public class DummyPerformenceMonitor : IPerfomenceMonitor
	{
		private class DummyDrawingPerformenceStatisticsData : IDrawingPerformenceStatisticsData
		{
			private PerformenceItem[] items = new PerformenceItem[0];

			public IEnumerable<PerformenceItem> PerformenceRanks => items;

			public double AveSpendTicks => 0;

			public double MostSpendTicks => 0;
		}

		private class DummyRenderPerformenceStatisticsData : IRenderPerformenceStatisticsData
		{
			public double AveSpendTicks => 0;

			public double MostSpendTicks => 0;

			public int AveDrawCall => 0;

			public long MostUIRenderSpendTicks => 0;

			public double AveUIRenderSpendTicks => 0;
		}

		private IDrawingPerformenceStatisticsData statisticsData = new DummyDrawingPerformenceStatisticsData();
		private IRenderPerformenceStatisticsData renderData = new DummyRenderPerformenceStatisticsData();

		public void Clear()
		{
		}

		public void CountDrawCall(IDrawing drawing)
		{
		}

		public void FormatStatistics(StringBuilder builder)
		{
		}

		public IDrawingPerformenceStatisticsData GetDrawingPerformenceData()
		{
			return statisticsData;
		}

		public IDrawingPerformenceStatisticsData GetDrawingTargetPerformenceData()
		{
			return statisticsData;
		}

		public IRenderPerformenceStatisticsData GetRenderPerformenceData()
		{
			return renderData;
		}

		public void OnAfterDrawing(IDrawing drawing)
		{
		}

		public void OnAfterRender()
		{
		}

		public void OnAfterTargetDrawing(IDrawingTarget drawing)
		{
		}

		public void OnBeforeRender()
		{
		}

		public void OnBeginDrawing(IDrawing drawing)
		{
		}

		public void OnBeginTargetDrawing(IDrawingTarget drawing)
		{
		}

		public void PostUIRenderTime(TimeSpan ts)
		{
		}
	}
}
