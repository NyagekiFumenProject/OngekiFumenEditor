using OngekiFumenEditor.Kernel.Audio.Rhythm;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Numerics;
using static OngekiFumenEditor.Kernel.Graphics.ILineDrawing;

namespace OngekiFumenEditor.Modules.AudioPlayerToolViewer.Graphics.WaveformDrawing
{
    /// <summary>
    /// 节奏曲线几何构造：纯函数、无状态，与 <see cref="WaveformGeometry"/> 同一套坐标约定
    /// （原点在绘制目标中心、y 向上、顶点两两成段）。
    /// </summary>
    internal static class RhythmGeometry
    {
        /// <summary>
        /// 构造节奏强度曲线。时间轴按像素列降采样：每列只保留该列时间跨度内所有帧的最大值，
        /// 因此顶点数只与视图宽度有关，与歌曲长度、缩放级别无关。
        /// <paramref name="tone"/> 的强调/γ 映射在降采样之前、按原生帧率施加（核宽与缩放无关）。
        /// </summary>
        public static void BuildCurve(List<LineVertex> points, RhythmEnvelope envelope, TimeSpan fromTime, TimeSpan toTime,
            float width, float height, float heightWeight, Vector4 color, RhythmCurveTone tone)
        {
            var columnCount = Math.Max(1, (int)MathF.Ceiling(width));
            var durationMs = (toTime - fromTime).TotalMilliseconds;
            var frameRate = envelope.FrameRateHz;
            var firstFrame = Math.Max(0, (int)(fromTime.TotalSeconds * frameRate));
            var lastFrame = Math.Min(envelope.FrameCount - 1, (int)(toTime.TotalSeconds * frameRate) + 1);
            var frameCount = Math.Max(0, lastFrame - firstFrame + 1);
            if (frameCount <= 0)
                return;

            var columns = ArrayPool<float>.Shared.Rent(columnCount);
            // 强调项依赖邻域：读取范围向两侧外扩模糊核半径，落在曲线数据内的部分取真实值，
            // 只有超出曲线首尾的部分补 0（歌曲之外的真实延续是静音，而不是把边界值无限重复）。
            var margin = tone.GetBlurRadiusFrames(frameRate);
            var bufferCount = frameCount + margin * 2;
            var desiredFirst = firstFrame - margin;
            var source = ArrayPool<float>.Shared.Rent(bufferCount);
            var mapped = ArrayPool<float>.Shared.Rent(bufferCount);
            var scratch = ArrayPool<float>.Shared.Rent(bufferCount);
            try
            {
                Array.Clear(columns, 0, columnCount);
                if (margin > 0)
                {
                    Array.Clear(source, 0, bufferCount);
                    var readFirst = Math.Max(0, desiredFirst);
                    var readLast = Math.Min(envelope.FrameCount, desiredFirst + bufferCount);
                    if (readLast > readFirst)
                        envelope.Total.AsSpan(readFirst, readLast - readFirst)
                            .CopyTo(source.AsSpan(readFirst - desiredFirst, readLast - readFirst));
                }
                else
                {
                    envelope.Total.AsSpan(firstFrame, frameCount).CopyTo(source.AsSpan(0, frameCount));
                }

                var values = source.AsSpan(0, bufferCount);
                var mappedValues = mapped.AsSpan(0, bufferCount);
                var scratchValues = scratch.AsSpan(0, bufferCount);
                tone.Apply(values, mappedValues, scratchValues, frameRate);

                for (var i = 0; i < frameCount; i++)
                {
                    var frame = firstFrame + i;
                    var x = WaveformGeometry.ProjectX(envelope.GetFrameTime(frame), fromTime, durationMs, width);
                    var column = Math.Clamp((int)(x + width / 2), 0, columnCount - 1);
                    var value = mappedValues[margin + i];
                    if (value > columns[column])
                        columns[column] = value;
                }

                var halfHeight = height / 2 * heightWeight;
                for (var column = 0; column < columnCount; column++)
                {
                    var x = column - width / 2 + 0.5f;
                    var y = halfHeight * columns[column];
                    points.Add(new(new(x, y), color, VertexDash.Solider));
                    points.Add(new(new(x, -y), color, VertexDash.Solider));
                }
            }
            finally
            {
                ArrayPool<float>.Shared.Return(columns);
                ArrayPool<float>.Shared.Return(source);
                ArrayPool<float>.Shared.Return(mapped);
                ArrayPool<float>.Shared.Return(scratch);
            }
        }
    }
}
