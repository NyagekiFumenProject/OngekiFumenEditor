using System;

namespace OngekiFumenEditor.Kernel.Audio.Rhythm
{
    /// <summary>
    /// 按固定帧率采样的节奏强度曲线：值域 0–1，已做局部自适应归一化。第 i 帧对应时刻 <c>i * FrameInterval</c>。
    /// <para>曲线是「相对变化量」而非绝对能量：持续音会被背景估计吸收，只有突然出现的声音才会抬升数值，
    /// 因此它看起来像鼓点轨，而不是频谱能量图。</para>
    /// </summary>
    public sealed record RhythmEnvelope(TimeSpan FrameInterval, float[] Total)
    {
        /// <summary>每秒帧数（绘制侧按时间轴重采样用）。</summary>
        public double FrameRateHz => 1000.0 / FrameInterval.TotalMilliseconds;

        public int FrameCount => Total.Length;

        /// <summary>第 <paramref name="index"/> 帧的起始时刻。</summary>
        public TimeSpan GetFrameTime(int index) => FrameInterval * index;
    }
}
