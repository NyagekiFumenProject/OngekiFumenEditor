using static OngekiFumenEditor.Kernel.Graphics.IPerfomenceMonitor;

namespace OngekiFumenEditor.Kernel.Graphics.Performence
{
	public struct RenderPerformenceStatisticsData : IRenderPerformenceStatisticsData
	{
		public double AveSpendTicks { get; set; }

		public double MostSpendTicks { get; set; }

		public int AveDrawCall { get; set; }

		public long MostUIRenderSpendTicks { get; set; }

		public double AveUIRenderSpendTicks { get; set; }
	}
}
