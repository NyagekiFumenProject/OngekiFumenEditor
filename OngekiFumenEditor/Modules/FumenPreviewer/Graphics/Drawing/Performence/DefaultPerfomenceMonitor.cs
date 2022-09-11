using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using static OngekiFumenEditor.Modules.FumenPreviewer.Graphics.Drawing.IPerfomenceMonitor;
using static OngekiFumenEditor.Modules.FumenPreviewer.Graphics.Drawing.IPerfomenceMonitor.IDrawingPerformenceStatisticsData;

namespace OngekiFumenEditor.Modules.FumenPreviewer.Graphics.Drawing.Performence
{
    [Export(typeof(IPerfomenceMonitor))]
    public class DefaultPerfomenceMonitor : IPerfomenceMonitor
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
        private FixedSizeCycleCollection<long> TotalDrawCall { get; } = new(RECORD_LENGTH);
        private long currentDrawCall = 0;

        public void OnBeforeRender()
        {
            currentDrawCall = 0;
            timer.Start();
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
            RenderSpendTicks.Enqueue(timer.ElapsedTicks);
            RenderSpendTicks.Enqueue(currentDrawCall);
        }

        public void Clear()
        {
            drawDataMap.Clear();
            drawTargetDataMap.Clear();
        }

        public struct RenderPerformenceStatisticsData : IRenderPerformenceStatisticsData
        {
            public double AveSpendTicks { get; set; }

            public double MostSpendTicks { get; set; }

            public int AveDrawCall { get; set; }
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
                MostSpendTicks = RenderSpendTicks.GroupBy(x => x).OrderByDescending(x => x.Key).FirstOrDefault().Key,
                AveDrawCall = (int)TotalDrawCall.Average()
            };
        }
    }
}
