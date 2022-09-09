using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Modules.FumenPreviewer.Graphics.Drawing.Performence
{
    public class DefaultPerfomenceMonitor : IPerfomenceMonitor
    {
        const int RECORD_LENGTH = 10;

        private class DrawingPerformenceData
        {
            public string Name { get; init; }
            public int DrawCallCount { get; set; }
            public long OnBeginDrawingTicks { get; set; }

            public FixedSizeCycleCollection<long> DrawingSpendTicks { get; } = new(RECORD_LENGTH);

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

        public void OnBeforeRender()
        {
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
        }

        public void OnAfterDrawing(IDrawing drawing)
        {
            var data = GetDrawingPerformenceData(drawing);
            var tickDiff = timer.ElapsedTicks - data.OnBeginDrawingTicks;
            data.DrawingSpendTicks.Enqueue(tickDiff);
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
        }

        public void Clear()
        {
            drawDataMap.Clear();
            drawTargetDataMap.Clear();
        }

        public IPerfomenceMonitor.IDrawingPerformenceData GetDrawingPerformenceData()
        {

        }

        public IPerfomenceMonitor.IDrawingPerformenceData GetDrawingTargetPerformenceData()
        {

        }

        public IPerfomenceMonitor.IPerformenceData GetRenderPerformenceData()
        {

        }
    }
}
