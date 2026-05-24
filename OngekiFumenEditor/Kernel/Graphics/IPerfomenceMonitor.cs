using OngekiFumenEditor.Kernel.Graphics.DrawCommands;
using System.Collections.Generic;
using System.Text;

namespace OngekiFumenEditor.Kernel.Graphics
{
	public interface IPerfomenceMonitor
	{
		public interface IRenderPerformenceStatisticsData
		{
			public long CurrentOnRenderSpendTicks { get; }
			public double AveOnRenderSpendTicks { get; }
			public double AveOnRenderFps { get; }
			public long CurrentPresentSpendTicks { get; }
			public double AvePresentSpendTicks { get; }
			public double AvePresentFps { get; }
			public double AveDrawCall { get; }
		}

		public interface ICategorizedPerformenceStatisticsData
		{
			public record PerformenceItem(string Name, double AveSpendTicks);
			public IEnumerable<PerformenceItem> PerformenceRanks { get; }
			public double AveSpendTicks { get; }
			public double MostSpendTicks { get; }
		}

		void OnBeforeRender();
		void OnAfterRender();
		void OnBeforePresent();
		void OnAfterPresent();
		void OnBeginDrawCommand(DrawCommand command);
		void OnAfterDrawCommand(DrawCommand command);
		void OnBeginTargetDrawing(IDrawingTarget target);
		void OnAfterTargetDrawing(IDrawingTarget target);
		void CountDrawCall();

		ICategorizedPerformenceStatisticsData GetDrawCommandPerformenceData();
		ICategorizedPerformenceStatisticsData GetDrawingTargetPerformenceData();
		IRenderPerformenceStatisticsData GetRenderPerformenceData();

		void FormatStatistics(StringBuilder builder);

		void Clear();
	}
}
