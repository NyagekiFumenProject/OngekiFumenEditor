using OngekiFumenEditor.Kernel.Graphics.DrawCommands;
using System.Collections.Generic;
using System.Text;
using static OngekiFumenEditor.Kernel.Graphics.IPerfomenceMonitor;
using static OngekiFumenEditor.Kernel.Graphics.IPerfomenceMonitor.ICategorizedPerformenceStatisticsData;

namespace OngekiFumenEditor.Kernel.Graphics.Performence
{
    public sealed class DummyPerformenceMonitor : IPerfomenceMonitor
    {
        private sealed class DummyCategorizedPerformenceStatisticsData : ICategorizedPerformenceStatisticsData
        {
            private static readonly PerformenceItem[] items = [];

            public IEnumerable<PerformenceItem> PerformenceRanks => items;

            public double AveSpendTicks => 0;

            public double MostSpendTicks => 0;
        }

        private sealed class DummyRenderPerformenceStatisticsData : IRenderPerformenceStatisticsData
        {
            public long CurrentOnRenderSpendTicks => 0;

            public double AveOnRenderSpendTicks => 0;

            public double AveOnRenderFps => 0;

            public long CurrentPresentSpendTicks => 0;

            public double AvePresentSpendTicks => 0;

            public double AvePresentFps => 0;

            public double AveDrawCall => 0;
        }

        private static readonly ICategorizedPerformenceStatisticsData statisticsData = new DummyCategorizedPerformenceStatisticsData();
        private static readonly IRenderPerformenceStatisticsData renderData = new DummyRenderPerformenceStatisticsData();

        public void Clear()
        {
        }

        public void CountDrawCall()
        {
        }

        public void FormatStatistics(StringBuilder builder)
        {
        }

        public ICategorizedPerformenceStatisticsData GetDrawCommandPerformenceData()
        {
            return statisticsData;
        }

        public ICategorizedPerformenceStatisticsData GetDrawingTargetPerformenceData()
        {
            return statisticsData;
        }

        public IRenderPerformenceStatisticsData GetRenderPerformenceData()
        {
            return renderData;
        }

        public void OnAfterDrawCommand(DrawCommand command)
        {
        }

        public void OnAfterPresent()
        {
        }

        public void OnAfterRender()
        {
        }

        public void OnAfterTargetDrawing(IDrawingTarget target)
        {
        }

        public void OnBeforePresent()
        {
        }

        public void OnBeforeRender()
        {
        }

        public void OnBeginDrawCommand(DrawCommand command)
        {
        }

        public void OnBeginTargetDrawing(IDrawingTarget target)
        {
        }

        public static DummyPerformenceMonitor Instance { get; } = new DummyPerformenceMonitor();
    }
}
