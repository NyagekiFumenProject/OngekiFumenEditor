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
        /// </summary>
        public static void BuildCurve(List<LineVertex> points, RhythmEnvelope envelope, TimeSpan fromTime, TimeSpan toTime,
            float width, float height, float heightWeight, Vector4 color)
        {
            var columnCount = Math.Max(1, (int)MathF.Ceiling(width));
            var columns = ArrayPool<float>.Shared.Rent(columnCount);
            try
            {
                Array.Clear(columns, 0, columnCount);

                var durationMs = (toTime - fromTime).TotalMilliseconds;
                var frameRate = envelope.FrameRateHz;
                var firstFrame = Math.Max(0, (int)(fromTime.TotalSeconds * frameRate));
                var lastFrame = Math.Min(envelope.FrameCount - 1, (int)(toTime.TotalSeconds * frameRate) + 1);

                for (var frame = firstFrame; frame <= lastFrame; frame++)
                {
                    var x = WaveformGeometry.ProjectX(envelope.GetFrameTime(frame), fromTime, durationMs, width);
                    var column = Math.Clamp((int)(x + width / 2), 0, columnCount - 1);
                    var value = envelope.Total[frame];
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
            }
        }
    }
}
