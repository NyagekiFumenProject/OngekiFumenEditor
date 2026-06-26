using OngekiFumenEditor.Kernel.Graphics.DrawCommands;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Text;
using static OngekiFumenEditor.Kernel.Graphics.IPerfomenceMonitor;
using static OngekiFumenEditor.Kernel.Graphics.IPerfomenceMonitor.ICategorizedPerformenceStatisticsData;

namespace OngekiFumenEditor.Kernel.Graphics.Performence
{
    [Export(typeof(IPerfomenceMonitor))]
    [PartCreationPolicy(CreationPolicy.NonShared)]
    public sealed class DefaultReleasePerfomenceMonitor : IPerfomenceMonitor
    {
        private const int RECORD_LENGTH = 60;

        private sealed class SampleWindow
        {
            private readonly long[] values = new long[RECORD_LENGTH];
            private int index;
            private int count;
            private long sum;

            public double Average => count == 0 ? 0 : (double)sum / count;

            public void Enqueue(long value)
            {
                if (count == values.Length)
                    sum -= values[index];
                else
                    count++;

                values[index] = value;
                sum += value;

                index = (index + 1) % values.Length;
            }

            public void Clear()
            {
                Array.Clear(values, 0, count);
                index = 0;
                count = 0;
                sum = 0;
            }
        }

        private sealed class EmptyCategorizedPerformenceStatisticsData : ICategorizedPerformenceStatisticsData
        {
            private static readonly PerformenceItem[] items = [];

            public IEnumerable<PerformenceItem> PerformenceRanks => items;

            public double AveSpendTicks => 0;

            public double MostSpendTicks => 0;
        }

        private static readonly ICategorizedPerformenceStatisticsData emptyCategorizedData = new EmptyCategorizedPerformenceStatisticsData();

        private readonly SampleWindow frameSpendTicks = new();
        private readonly SampleWindow onRenderSpendTicks = new();
        private readonly SampleWindow presentSpendTicks = new();
        private readonly SampleWindow drawCall = new();

        private long previousFrameTimestamp;
        private long renderBeginTimestamp;
        private long presentBeginTimestamp;
        private long currentFrameSpendTicks;
        private int currentDrawCall;
        private bool isRendering;
        private bool isPresenting;

        public void OnBeforeRender()
        {
            var currentTimestamp = Stopwatch.GetTimestamp();
            if (previousFrameTimestamp != 0)
            {
                currentFrameSpendTicks = Stopwatch.GetElapsedTime(previousFrameTimestamp, currentTimestamp).Ticks;
                frameSpendTicks.Enqueue(currentFrameSpendTicks);
            }
            else
            {
                currentFrameSpendTicks = 0;
            }

            previousFrameTimestamp = currentTimestamp;
            renderBeginTimestamp = currentTimestamp;
            isRendering = true;
        }

        public void OnAfterRender()
        {
            if (!isRendering)
                return;

            onRenderSpendTicks.Enqueue(Stopwatch.GetElapsedTime(renderBeginTimestamp).Ticks);
            isRendering = false;
        }

        public void OnBeforePresent()
        {
            currentDrawCall = 0;
            presentBeginTimestamp = Stopwatch.GetTimestamp();
            isPresenting = true;
        }

        public void OnAfterPresent()
        {
            if (!isPresenting)
                return;

            presentSpendTicks.Enqueue(Stopwatch.GetElapsedTime(presentBeginTimestamp).Ticks);
            drawCall.Enqueue(currentDrawCall);
            isPresenting = false;
        }

        public void CountDrawCall()
        {
            currentDrawCall++;
        }

        public void OnBeginDrawCommand(DrawCommand command)
        {
        }

        public void OnAfterDrawCommand(DrawCommand command)
        {
        }

        public void OnBeginTargetDrawing(IDrawingTarget target)
        {
        }

        public void OnAfterTargetDrawing(IDrawingTarget target)
        {
        }

        public ICategorizedPerformenceStatisticsData GetDrawCommandPerformenceData()
        {
            return emptyCategorizedData;
        }

        public ICategorizedPerformenceStatisticsData GetDrawingTargetPerformenceData()
        {
            return emptyCategorizedData;
        }

        public IRenderPerformenceStatisticsData GetRenderPerformenceData()
        {
            var aveFrameSpendTicks = frameSpendTicks.Average;
            var aveOnRenderSpendTicks = onRenderSpendTicks.Average;
            var avePresentSpendTicks = presentSpendTicks.Average;

            return new RenderPerformenceStatisticsData()
            {
                CurrentFrameSpendTicks = currentFrameSpendTicks,
                AveFrameSpendTicks = aveFrameSpendTicks,
                AveFrameFps = ToFps(aveFrameSpendTicks),
                AveOnRenderSpendTicks = aveOnRenderSpendTicks,
                AveOnRenderFps = ToFps(aveOnRenderSpendTicks),
                AvePresentSpendTicks = avePresentSpendTicks,
                AvePresentFps = ToFps(avePresentSpendTicks),
                AveDrawCall = drawCall.Average
            };
        }

        public void FormatStatistics(StringBuilder builder)
        {
            var render = GetRenderPerformenceData();

            string formatFPS(double fps) => $"{fps,7:0.00}";
            string formatMSec(double ticks) => $"{TimeSpan.FromTicks((long)Math.Max(0, ticks)).TotalMilliseconds:F2}";

            builder.AppendLine($"Frame FPS avg:{formatFPS(render.AveFrameFps)} ({formatMSec(render.AveFrameSpendTicks)}ms avg)");
            builder.AppendLine($"OnRender FPS avg:{formatFPS(render.AveOnRenderFps)} ({formatMSec(render.AveOnRenderSpendTicks)}ms avg)");
            builder.AppendLine($"Present  FPS avg:{formatFPS(render.AvePresentFps)} ({formatMSec(render.AvePresentSpendTicks)}ms avg)");
            builder.AppendLine($"DrawCall avg:{render.AveDrawCall,6:F1}");
        }

        public void Clear()
        {
            frameSpendTicks.Clear();
            onRenderSpendTicks.Clear();
            presentSpendTicks.Clear();
            drawCall.Clear();
            previousFrameTimestamp = 0;
            currentFrameSpendTicks = 0;
            currentDrawCall = 0;
            isRendering = false;
            isPresenting = false;
        }

        private static double ToFps(double ticks)
        {
            return ticks > 0 ? TimeSpan.TicksPerSecond / ticks : 0;
        }
    }
}
