using System;

namespace OngekiFumenEditor.Modules.AudioPlayerToolViewer.Graphics.WaveformDrawing
{
    /// <summary>
    /// 节奏曲线的显示侧色调映射，两步都在曲线的**原生帧率分辨率**上做（像素列降采样之前，
    /// 否则核宽会随缩放变化，同一个峰在不同缩放下时宽不一致）：
    /// <list type="number">
    /// <item><b>局部均值强调</b>：<c>v' = clamp(v + λ·(v − blurσ(v)), 0, 1)</c>。
    ///       σ 固定与节拍包宽同量级（100ms），核用两次滑动均值近似（三角核），不做 O(n·k) 的卷积；
    ///       曲线本来就平坦时 <c>v − blurσ(v) ≈ 0</c>，因此**不会凭空造出峰**，只在有峰/谷的地方拉开对比。</item>
    /// <item><b>γ 压缩</b>：<c>v'' = v'^γ</c>。峰顶保持 1，中低段整体压低，基线跟着降下来。</item>
    /// </list>
    /// </summary>
    internal readonly record struct RhythmCurveTone(float Gamma, float Emphasis)
    {
        public const float MinGamma = 1f;
        public const float MaxGamma = 3f;
        public const float MaxEmphasis = 2f;

        /// <summary>强调核的时间宽度（秒）：与节拍包宽（约 100–300ms）同量级。</summary>
        public const float EmphasisScaleSeconds = 0.1f;

        public const float DefaultGamma = 1.5f;
        public const float DefaultEmphasis = 0.7f;

        /// <summary>默认：γ=1.5 压基线 + λ=0.7 轻度强调。</summary>
        public static RhythmCurveTone Default { get; } = new(DefaultGamma, DefaultEmphasis);

        /// <summary>不做任何映射（几何/单元用途）。</summary>
        public static RhythmCurveTone Identity { get; } = new(1f, 0f);

        /// <summary>
        /// 把 <paramref name="source"/> 映射到 <paramref name="destination"/>（两者必须不同，长度相同）。
        /// <paramref name="scratch"/> 为等长的临时缓冲，由调用方池化复用，因此这里不做任何分配。
        /// </summary>
        public void Apply(ReadOnlySpan<float> source, Span<float> destination, Span<float> scratch, double frameRateHz)
        {
            var count = source.Length;
            if (count == 0)
                return;

            var gamma = Math.Clamp(Gamma, MinGamma, MaxGamma);
            var emphasis = Math.Clamp(Emphasis, 0, MaxEmphasis);

            if (emphasis > 0 && frameRateHz > 0)
            {
                var halfWindow = Math.Max(1, (int)Math.Round(EmphasisScaleSeconds * frameRateHz / 2));
                BoxBlur(source, scratch, halfWindow);
                BoxBlur(scratch, destination, halfWindow);          // 两次盒式 → 三角核（高斯近似）

                for (var i = 0; i < count; i++)
                {
                    var value = source[i];
                    var emphasized = value + emphasis * (value - destination[i]);
                    destination[i] = Math.Clamp(emphasized, 0, 1);
                }
            }
            else
            {
                source.CopyTo(destination);
            }

            if (Math.Abs(gamma - 1f) > 1e-4f)
            {
                for (var i = 0; i < count; i++)
                {
                    var value = destination[i];
                    if (value > 0)
                        destination[i] = MathF.Pow(value, gamma);
                }
            }
        }

        /// <summary>盒式滑动均值（边界按最近样本填充），O(n)，用于近似高斯核。</summary>
        private static void BoxBlur(ReadOnlySpan<float> source, Span<float> destination, int halfWindow)
        {
            var count = source.Length;
            if (count == 0)
                return;

            if (halfWindow <= 0)
            {
                source.CopyTo(destination);
                return;
            }

            var width = halfWindow * 2 + 1;
            double sum = 0;
            for (var i = -halfWindow; i <= halfWindow; i++)
                sum += source[Math.Clamp(i, 0, count - 1)];

            for (var i = 0; i < count; i++)
            {
                destination[i] = (float)(sum / width);
                sum += source[Math.Clamp(i + halfWindow + 1, 0, count - 1)]
                     - source[Math.Clamp(i - halfWindow, 0, count - 1)];
            }
        }
    }
}
