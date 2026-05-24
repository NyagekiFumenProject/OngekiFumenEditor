using static OngekiFumenEditor.Kernel.Graphics.IPerfomenceMonitor;

namespace OngekiFumenEditor.Kernel.Graphics.Performence
{
	public struct RenderPerformenceStatisticsData : IRenderPerformenceStatisticsData
	{
		public long CurrentFrameSpendTicks { get; set; }

		public double AveFrameSpendTicks { get; set; }

		public double AveFrameFps { get; set; }

		public long CurrentOnRenderSpendTicks { get; set; }

		public double AveOnRenderSpendTicks { get; set; }

		public double AveOnRenderFps { get; set; }

		public long CurrentPresentSpendTicks { get; set; }

		public double AvePresentSpendTicks { get; set; }

		public double AvePresentFps { get; set; }

		public double AveDrawCall { get; set; }
	}
}
