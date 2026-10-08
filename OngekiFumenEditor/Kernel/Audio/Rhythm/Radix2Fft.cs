using System;

namespace OngekiFumenEditor.Kernel.Audio.Rhythm
{
    /// <summary>
    /// 定长（2 的幂）基-2 迭代 FFT：位反转表与旋转因子在构造时预计算，<see cref="Forward"/> 全程无分配。
    /// 节奏分析每秒要跑 200 次变换，所以这里刻意不做泛化（不支持任意长度、不支持逆变换）。
    /// </summary>
    internal sealed class Radix2Fft
    {
        private readonly int size;
        private readonly int[] bitReversed;
        private readonly float[] cosTable;
        private readonly float[] sinTable;

        public int Size => size;

        public Radix2Fft(int size)
        {
            if (size < 2 || (size & (size - 1)) != 0)
                throw new ArgumentException($"FFT size must be a power of two, actual={size}", nameof(size));

            this.size = size;
            bitReversed = new int[size];
            cosTable = new float[size / 2];
            sinTable = new float[size / 2];

            var bits = 0;
            while ((1 << bits) < size)
                bits++;

            for (var i = 0; i < size; i++)
            {
                var reversed = 0;
                for (var b = 0; b < bits; b++)
                    reversed |= ((i >> b) & 1) << (bits - 1 - b);
                bitReversed[i] = reversed;
            }

            for (var i = 0; i < size / 2; i++)
            {
                var angle = -2.0 * Math.PI * i / size;
                cosTable[i] = (float)Math.Cos(angle);
                sinTable[i] = (float)Math.Sin(angle);
            }
        }

        /// <summary>原地复数正变换，<paramref name="real"/>/<paramref name="imaginary"/> 长度必须等于构造长度。</summary>
        public void Forward(float[] real, float[] imaginary)
        {
            for (var i = 0; i < size; i++)
            {
                var j = bitReversed[i];
                if (j <= i)
                    continue;

                (real[i], real[j]) = (real[j], real[i]);
                (imaginary[i], imaginary[j]) = (imaginary[j], imaginary[i]);
            }

            for (var span = 2; span <= size; span <<= 1)
            {
                var half = span >> 1;
                var twiddleStep = size / span;

                for (var start = 0; start < size; start += span)
                {
                    for (int k = 0, twiddle = 0; k < half; k++, twiddle += twiddleStep)
                    {
                        var other = start + k + half;
                        var wr = cosTable[twiddle];
                        var wi = sinTable[twiddle];
                        var tr = real[other] * wr - imaginary[other] * wi;
                        var ti = real[other] * wi + imaginary[other] * wr;

                        real[other] = real[start + k] - tr;
                        imaginary[other] = imaginary[start + k] - ti;
                        real[start + k] += tr;
                        imaginary[start + k] += ti;
                    }
                }
            }
        }
    }
}
