using OngekiFumenEditor.Kernel.Audio;
using System;
using System.Collections.Generic;
using System.Numerics;
using static OngekiFumenEditor.Kernel.Graphics.ILineDrawing;

namespace OngekiFumenEditor.Modules.AudioPlayerToolViewer.Graphics.WaveformDrawing
{
    /// <summary>
    /// 波形折线几何构造：纯函数、无状态，被实时绘制与离屏分块烘焙共用。
    /// 坐标以绘制目标中心为原点、y 向上，与编辑器其余绘制一致。
    /// <para>
    /// 顶点按「每两个顶点一段」被后端消费，且每段的颜色与虚线取自该段的<b>起始顶点</b>
    /// （颜色不同的两段会各自独立成段并做渐变），所以这里严格保持与改造前
    /// <c>DefaultWaveformDrawing</c> 相同的顶点配对与属性。
    /// </para>
    /// </summary>
    internal static class WaveformGeometry
    {
        private static readonly VertexDash InvailedLineDash = new(2, 2);

        private static readonly Vector4 WhiteColor = new(1, 1, 1, 1);
        private static readonly Vector4 WaveformFillColor = new(100 / 255.0f, 149 / 255.0f, 237 / 255.0f, 1);

        /// <summary>把时间投影为绘制目标的逻辑 x 坐标（原点在中心、y 轴向上）。</summary>
        public static float ProjectX(TimeSpan time, TimeSpan fromTime, double durationMs, float width)
            => (float)(width * ((time - fromTime).TotalMilliseconds / durationMs) - width / 2);

        /// <summary>
        /// 完整几何（改造前实时绘制的顶点序列）：左边界标记 → 波形本体 → 末端标记 → 右边界标记。
        /// 图块未就绪时整帧回退到实时绘制，走的就是这条路径。
        /// </summary>
        public static void Build(List<LineVertex> points, PeakPointCollection peakData, TimeSpan fromTime, TimeSpan toTime,
            float width, float height)
        {
            var durationMs = (toTime - fromTime).TotalMilliseconds;
            (var minIndex, var maxIndex) = peakData.BinaryFindRangeIndex(fromTime, toTime);

            points.Add(new(new(-width / 2, 0), WhiteColor, InvailedLineDash));

            AppendBody(points, peakData, fromTime, durationMs, width, height, minIndex, maxIndex);

            var prevX = maxIndex > minIndex ? ProjectX(peakData[maxIndex - 1].Time, fromTime, durationMs, width) : 0f;
            points.Add(new(new(prevX, 0), WaveformFillColor, InvailedLineDash));
            points.Add(new(new(width / 2, 0), WhiteColor, InvailedLineDash));
        }

        /// <summary>
        /// 只构造波形本体（分块烘焙用）。
        /// <paramref name="rangeMarginPoints"/> 让范围两端各多纳入若干个峰点，使折线跨过图块边界，消除块缝断线。
        /// </summary>
        public static void BuildBody(List<LineVertex> points, PeakPointCollection peakData, TimeSpan fromTime, TimeSpan toTime,
            float width, float height, int rangeMarginPoints)
        {
            var durationMs = (toTime - fromTime).TotalMilliseconds;
            (var minIndex, var maxIndex) = peakData.BinaryFindRangeIndex(fromTime, toTime);

            if (rangeMarginPoints > 0)
            {
                minIndex = Math.Max(0, minIndex - rangeMarginPoints);
                maxIndex = Math.Min(peakData.Count, maxIndex + rangeMarginPoints);
            }

            AppendBody(points, peakData, fromTime, durationMs, width, height, minIndex, maxIndex);
        }

        /// <summary>
        /// 分块贴回时补绘被图块裁掉的边界几何，顶点对与 <see cref="Build"/> 中的对应线段完全一致，
        /// 但分成两条链（后端每两个顶点成段，链之间不能相连，否则会画出实时绘制不存在的连线）：
        /// <list type="bullet">
        /// <item><paramref name="leftMarker"/>：左边界标记 → 第一个可见峰点；</item>
        /// <item><paramref name="trailingMarkers"/>：最后一个可见峰点 → 中轴点 → 右边界标记。</item>
        /// </list>
        /// 范围内没有峰点时，两条链退化到中轴原点，与实时绘制一致。
        /// </summary>
        public static void BuildEdgeMarkers(List<LineVertex> leftMarker, List<LineVertex> trailingMarkers,
            PeakPointCollection peakData, TimeSpan fromTime, TimeSpan toTime, float width, float height)
        {
            var durationMs = (toTime - fromTime).TotalMilliseconds;
            (var minIndex, var maxIndex) = peakData.BinaryFindRangeIndex(fromTime, toTime);

            leftMarker.Add(new(new(-width / 2, 0), WhiteColor, InvailedLineDash));

            if (maxIndex > minIndex)
            {
                var firstPeakPoint = peakData[minIndex];
                var firstX = ProjectX(firstPeakPoint.Time, fromTime, durationMs, width);
                leftMarker.Add(new(new(firstX, height / 2 * firstPeakPoint.Amplitudes[0]), WaveformFillColor, VertexDash.Solider));

                var lastPeakPoint = peakData[maxIndex - 1];
                var lastX = ProjectX(lastPeakPoint.Time, fromTime, durationMs, width);
                trailingMarkers.Add(new(new(lastX, -height / 2 * lastPeakPoint.Amplitudes[1]), WaveformFillColor, VertexDash.Solider));
                trailingMarkers.Add(new(new(lastX, 0), WaveformFillColor, InvailedLineDash));
                trailingMarkers.Add(new(new(width / 2, 0), WhiteColor, InvailedLineDash));
            }
            else
            {
                leftMarker.Add(new(new(0, 0), WaveformFillColor, InvailedLineDash));
                trailingMarkers.Add(new(new(0, 0), WaveformFillColor, InvailedLineDash));
                trailingMarkers.Add(new(new(width / 2, 0), WhiteColor, InvailedLineDash));
            }
        }

        private static void AppendBody(List<LineVertex> points, PeakPointCollection peakData, TimeSpan fromTime,
            double durationMs, float width, float height, int minIndex, int maxIndex)
        {
            for (var i = minIndex; i < maxIndex; i += 1)
            {
                var peakPoint = peakData[i];

                var x = ProjectX(peakPoint.Time, fromTime, durationMs, width);
                var yTop = height / 2 * peakPoint.Amplitudes[0];
                var yButtom = -height / 2 * peakPoint.Amplitudes[1];

                points.Add(new(new(x, yTop), WaveformFillColor, VertexDash.Solider));
                points.Add(new(new(x, yButtom), WaveformFillColor, VertexDash.Solider));
            }
        }
    }
}
