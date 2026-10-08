using System;
using System.ComponentModel.Composition;
using System.Runtime.InteropServices;
using System.Threading;

namespace OngekiFumenEditor.Kernel.Audio.Rhythm
{
    /// <summary>
    /// 默认节奏频谱分析器。核心思路是把「频谱里突然出现的东西」单独拎出来，而不是画绝对能量：
    /// <list type="number">
    /// <item>短时傅里叶（1024 点 Hann，5ms 一跳，帧以窗口中心对齐时间轴）；</item>
    /// <item>对数压缩：把宽动态范围压到可比较的尺度，弱拍才不会被强拍吞掉；</item>
    /// <item>把 15ms 前的频谱沿频率轴取邻近 ±1 bin 的最大值，再保留当前幅度超过它至少 0.03 的抬升；
    ///       抑制持续音、泄漏和小幅颤音，同时避免长尾参考与宽平滑把密集击打合并；</item>
    /// <item>分频段汇总起音强度，以 σ=7.5ms 的高斯核平滑，再按每 3 秒的局部峰值归一化；
    ///       尺度下限取 0.1 与全曲原始通量 P95 的 25% 中较大者，避免放大微小残差；</item>
    /// <item>三条曲线按固定权重合成一条 0–1 的总曲线，交给绘制层。</item>
    /// </list>
    /// </summary>
    [Export(typeof(IRhythmAnalyzer))]
    internal class DefaultRhythmAnalyzer : IRhythmAnalyzer
    {
        private const int FftSize = 1024;
        private const int FramesPerSecond = 200;
        private const int BandCount = 3;

        // 频段边界（Hz）。高频上界再受采样率/奈奎斯特限制。
        private static readonly double[] BandLowerHz = { 20, 200, 2000 };
        private static readonly double[] BandUpperHz = { 200, 2000, 16000 };
        private static readonly float[] BandWeights = { 1.0f, 1.0f, 0.6f };

        private const float LogCompressGain = 300f;         // ln(1 + g*x)：典型音乐幅度压到 O(1)
        private const int ReferenceLagFrames = 3;          // 15ms 前的频谱参考
        private const float MinimumLogRise = 0.03f;        // 对数幅度域的残差下限
        private const double SmoothSigmaSeconds = 0.0075;  // 窄高斯平滑，保留密集峰之间的谷
        private const float MinimumScale = 0.1f;           // 局部尺度的绝对下限
        private const double LocalScaleSeconds = 3.0;       // 局部归一化窗口
        private const double GlobalScaleFloorRatio = 0.25;  // 局部尺度不低于全局 P95 的比例
        private static readonly float[] SmoothingKernel = BuildGaussianKernel(SmoothSigmaSeconds);

        public RhythmEnvelope Analyze(SampleData data, CancellationToken cancellationToken = default)
        {
            if (data is null)
                return null;

            var info = data.SampleInfo;
            // 播放链路统一输出 32bit 浮点 PCM；其他位深说明数据来源已变，宁可不画也不画错。
            if (info.SampleRate <= 0 || info.Channels <= 0 || info.BitsPerSample != 32)
                return null;

            var interleaved = MemoryMarshal.Cast<byte, float>(data.Samples.Span);
            var sampleCount = interleaved.Length / info.Channels;
            var hop = Math.Max(1, info.SampleRate / FramesPerSecond);
            if (sampleCount < FftSize)
                return null;

            // 帧以窗口中心对齐时间轴（第 f 帧的中心在 f*hop），起音时刻因此不会系统性偏移半个窗长
            var frameCount = sampleCount / hop + 1;
            if (frameCount < FramesPerSecond)
                return null;

            var bandBins = BuildBandBins(info.SampleRate);
            if (bandBins is null)
                return null;

            var flux = new float[BandCount][];
            for (var band = 0; band < BandCount; band++)
                flux[band] = new float[frameCount];

            ComputeBandFlux(interleaved, sampleCount, info.Channels, hop, frameCount, bandBins, flux, cancellationToken);
            if (cancellationToken.IsCancellationRequested)
                return null;

            var weightSum = 0f;
            foreach (var weight in BandWeights)
                weightSum += weight;

            var total = new float[frameCount];
            for (var band = 0; band < BandCount; band++)
            {
                var smoothed = GaussianSmooth(flux[band], SmoothingKernel);
                var localScale = SlidingMax(smoothed, Math.Max(1, (int)(LocalScaleSeconds * FramesPerSecond)));
                var scaleFloor = MathF.Max(MinimumScale,
                    (float)(Percentile(flux[band], 0.95) * GlobalScaleFloorRatio));
                var weight = BandWeights[band] / weightSum;

                for (var i = 0; i < frameCount; i++)
                {
                    var scale = MathF.Max(localScale[i], scaleFloor);
                    if (scale <= 0)
                        continue;

                    total[i] += Math.Clamp(smoothed[i] / scale, 0, 1) * weight;
                }

                cancellationToken.ThrowIfCancellationRequested();
            }

            return new RhythmEnvelope(TimeSpan.FromSeconds(1.0 / FramesPerSecond), total);
        }

        /// <summary>把频段边界换算成 FFT bin 下标；任何一段没有可用 bin 时返回 null。</summary>
        private static int[][] BuildBandBins(int sampleRate)
        {
            var binCount = FftSize / 2 + 1;
            var binHz = (double)sampleRate / FftSize;
            var result = new int[BandCount][];

            for (var band = 0; band < BandCount; band++)
            {
                var first = Math.Max(1, (int)Math.Ceiling(BandLowerHz[band] / binHz));
                var last = Math.Min(binCount - 1, (int)Math.Floor(BandUpperHz[band] / binHz));
                if (last < first)
                    return null;

                var bins = new int[last - first + 1];
                for (var i = 0; i < bins.Length; i++)
                    bins[i] = first + i;
                result[band] = bins;
            }

            return result;
        }

        /// <summary>
        /// 对当前频谱与 15ms 前的频率邻域最大值作正差分，再按频段求平均。
        /// 环形缓冲保存已取频率最大值的参考谱；前三帧用首帧作参考，首帧本身不计通量。
        /// 频率最大值在全部非 DC bin 上计算，可以跨过频段边界。
        /// </summary>
        private static void ComputeBandFlux(ReadOnlySpan<float> interleaved, int sampleCount, int channels, int hop,
            int frameCount, int[][] bandBins, float[][] flux, CancellationToken cancellationToken)
        {
            var fft = new Radix2Fft(FftSize);
            var window = new float[FftSize];
            var windowGain = 0f;
            for (var i = 0; i < FftSize; i++)
            {
                // 周期 Hann（0.5 - 0.5cos(2πi/N)），与 STFT 的帧长约定一致
                window[i] = 0.5f - 0.5f * MathF.Cos(2 * MathF.PI * i / FftSize);
                windowGain += window[i];
            }
            windowGain /= FftSize;

            var re = new float[FftSize];
            var im = new float[FftSize];

            var binCount = FftSize / 2 + 1;
            var levels = new float[binCount];
            var referenceHistory = new float[binCount * ReferenceLagFrames];
            var historyCursor = 0;

            var magnitudeScale = 2.0f / (FftSize * windowGain);   // 单边幅度归一：满幅正弦 ≈ 1

            for (var frame = 0; frame < frameCount; frame++)
            {
                if ((frame & 255) == 0 && cancellationToken.IsCancellationRequested)
                    return;

                var start = frame * hop - FftSize / 2;
                for (var i = 0; i < FftSize; i++)
                {
                    var sampleIndex = start + i;
                    var value = (uint)sampleIndex < (uint)sampleCount ? Downmix(interleaved, sampleIndex * channels, channels) : 0f;
                    re[i] = value * window[i];
                    im[i] = 0f;
                }

                fft.Forward(re, im);

                for (var k = 1; k < binCount; k++)
                    levels[k] = LogLevel(re[k], im[k], magnitudeScale);

                if (frame > 0)
                {
                    var referenceOffset = frame < ReferenceLagFrames ? 0 : historyCursor * binCount;
                    for (var band = 0; band < bandBins.Length; band++)
                    {
                        var bins = bandBins[band];
                        double sum = 0;
                        foreach (var k in bins)
                            sum += Math.Max(0d, (double)levels[k]
                                - referenceHistory[referenceOffset + k] - MinimumLogRise);
                        flux[band][frame] = (float)(sum / bins.Length);
                    }
                }

                // 比较完成后才写当前帧，不能污染正在使用的滞后参考。
                BuildFrequencyReference(levels, referenceHistory, historyCursor * binCount);
                if (++historyCursor == ReferenceLagFrames)
                    historyCursor = 0;
            }
        }

        private static void BuildFrequencyReference(float[] levels, float[] history, int offset)
        {
            history[offset + 1] = MathF.Max(levels[1], levels[2]);
            for (var k = 2; k < levels.Length - 1; k++)
                history[offset + k] = MathF.Max(levels[k - 1], MathF.Max(levels[k], levels[k + 1]));
            history[offset + levels.Length - 1] = MathF.Max(levels[^2], levels[^1]);
        }

        /// <summary>ln(1 + g·|X|)：小信号近似线性、大信号对数压缩，两种动态都能看清节奏。</summary>
        private static float LogLevel(float real, float imaginary, float magnitudeScale)
            => MathF.Log(1 + LogCompressGain * MathF.Sqrt(real * real + imaginary * imaginary) * magnitudeScale);

        private static float Downmix(ReadOnlySpan<float> interleaved, int offset, int channels)
        {
            if (channels == 1)
                return interleaved[offset];

            var sum = 0f;
            for (var c = 0; c < channels; c++)
                sum += interleaved[offset + c];
            return sum / channels;
        }

        private static float[] BuildGaussianKernel(double sigmaSeconds)
        {
            var sigmaFrames = sigmaSeconds * FramesPerSecond;
            var radius = Math.Max(1, (int)Math.Ceiling(3 * sigmaFrames));
            var kernel = new float[2 * radius + 1];
            var sum = 0f;
            for (var i = -radius; i <= radius; i++)
            {
                kernel[i + radius] = (float)Math.Exp(-i * i / (2 * sigmaFrames * sigmaFrames));
                sum += kernel[i + radius];
            }
            for (var i = 0; i < kernel.Length; i++)
                kernel[i] /= sum;
            return kernel;
        }

        /// <summary>居中高斯平滑；边界按最近样本填充，与曲线的时间轴保持对齐。</summary>
        private static float[] GaussianSmooth(float[] values, float[] kernel)
        {
            var result = new float[values.Length];
            var radius = kernel.Length / 2;
            for (var i = 0; i < values.Length; i++)
            {
                for (var k = 0; k < kernel.Length; k++)
                    result[i] += values[Math.Clamp(i + k - radius, 0, values.Length - 1)] * kernel[k];
            }
            return result;
        }

        /// <summary>以第 i 个样本为中心的滑动最大值（单调队列，O(n)）；边界按最近样本填充。</summary>
        private static float[] SlidingMax(float[] values, int window)
        {
            var length = values.Length;
            var result = new float[length];
            if (window <= 1)
            {
                Array.Copy(values, result, length);
                return result;
            }

            var half = window / 2;
            var deque = new int[length + 1];
            var head = 0;
            var tail = 0;

            for (var i = -half; i < length + half; i++)
            {
                var index = Math.Clamp(i, 0, length - 1);
                var value = values[index];
                while (tail > head && values[deque[tail - 1]] <= value)
                    tail--;
                deque[tail++] = index;

                var expired = i - window;
                while (tail > head && deque[head] <= expired)
                    head++;

                var center = i - half;
                if (center >= 0)
                    result[center] = values[deque[head]];
            }

            return result;
        }

        /// <summary>直方图法估计分位数（用于全曲尺度下限，不要求精确）。</summary>
        private static float Percentile(float[] values, double ratio)
        {
            const int bucketCount = 1024;

            var max = 0f;
            foreach (var value in values)
            {
                if (value > max)
                    max = value;
            }

            if (max <= 0)
                return 0;

            var histogram = new int[bucketCount];
            var scale = (bucketCount - 1) / max;
            foreach (var value in values)
            {
                if (value > 0)
                    histogram[(int)(value * scale)]++;
            }

            var target = (int)(values.Length * ratio);
            var accumulated = 0;
            for (var i = 0; i < bucketCount; i++)
            {
                accumulated += histogram[i];
                if (accumulated >= target)
                    return (i + 0.5f) / scale;
            }

            return max;
        }
    }
}
