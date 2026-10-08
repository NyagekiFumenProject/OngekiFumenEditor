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
    /// <item>逐 bin 背景抑制：每个频点维护一条「最近 150ms 对数幅度均值」的参考值，只有相对它的抬升才计入，
    ///       于是持续音/长音被抵消、鼓点被留下（有限窗口让过去的事件滑过即被遗忘，指数平均会一直记得长尾）；</item>
    /// <item>分频段汇总成三条起音强度曲线（低/中/高），再做局部自适应归一化——每 3 秒的局部峰值拉到 1，
    ///       但尺度下限不低于全曲 P95 的一定比例，静音段不会被噪声抬满；</item>
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
        private const double BackgroundWindowSeconds = 0.15;  // 每 bin 背景窗口长度（滑过即遗忘）
        private const double SmoothSeconds = 0.05;          // 归一化前的平滑
        private const double LocalScaleSeconds = 3.0;       // 局部归一化窗口
        private const double GlobalScaleFloorRatio = 0.25;  // 局部尺度不低于全局 P95 的比例

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
            cancellationToken.ThrowIfCancellationRequested();

            var weightSum = 0f;
            foreach (var weight in BandWeights)
                weightSum += weight;

            var total = new float[frameCount];
            for (var band = 0; band < BandCount; band++)
            {
                var smoothed = MovingAverage(flux[band], Math.Max(1, (int)(SmoothSeconds * FramesPerSecond)));
                var localScale = SlidingMax(smoothed, Math.Max(1, (int)(LocalScaleSeconds * FramesPerSecond)));
                var scaleFloor = (float)(Percentile(flux[band], 0.95) * GlobalScaleFloorRatio);
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
        /// 逐帧做 STFT，计算每个 bin 相对自身近期背景的对数幅度抬升，并按频段汇总成谱通量。
        /// 背景取「最近一个有限窗口内对数幅度的均值」：有限窗口让过去的事件在窗口滑过之后彻底被遗忘，
        /// 军鼓这类宽带长尾不会把紧跟其后的踩镲压掉（指数平均会一直记得）。参考值在「用掉之后」才把当前帧计入，
        /// 当前帧的抬升因此不会被自己污染；首帧只初始化背景、不计通量，避免开头必然出现一次虚假的强抬升。
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
            var rise = new float[FftSize / 2 + 1];

            var binCount = FftSize / 2 + 1;
            var historyLength = Math.Max(1, (int)(BackgroundWindowSeconds * FramesPerSecond));
            var levels = new float[binCount];                   // 本帧各 bin 的对数幅度
            var history = new float[binCount * historyLength];  // 每 bin 的背景环形缓冲
            var historySum = new float[binCount];               // 每 bin 背景窗口内求和
            var historyCursor = 0;

            var magnitudeScale = 2.0f / (FftSize * windowGain);   // 单边幅度归一：满幅正弦 ≈ 1

            for (var frame = 0; frame < frameCount; frame++)
            {
                if ((frame & 255) == 0)
                    cancellationToken.ThrowIfCancellationRequested();

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

                if (frame == 0)
                {
                    // 用首帧填满背景窗口：既避免开头的虚假强起音，也省掉每 bin 的窗口计数分支
                    for (var k = 1; k < binCount; k++)
                    {
                        var first = levels[k];
                        historySum[k] = first * historyLength;
                        var baseIndex = k * historyLength;
                        for (var slot = 0; slot < historyLength; slot++)
                            history[baseIndex + slot] = first;
                    }
                }
                else
                {
                    for (var k = 1; k < binCount; k++)
                    {
                        var level = levels[k];
                        var delta = level - historySum[k] / historyLength;
                        rise[k] = delta > 0 ? delta : 0;

                        var slotIndex = k * historyLength + historyCursor;
                        historySum[k] += level - history[slotIndex];
                        history[slotIndex] = level;
                    }

                    for (var band = 0; band < bandBins.Length; band++)
                    {
                        var bins = bandBins[band];
                        var sum = 0f;
                        foreach (var k in bins)
                            sum += rise[k];
                        flux[band][frame] = sum / bins.Length;
                    }
                }

                historyCursor = historyCursor + 1 >= historyLength ? 0 : historyCursor + 1;
            }
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

        /// <summary>以第 i 个样本为中心的滑动平均（前缀和实现，O(n)）。</summary>
        private static float[] MovingAverage(float[] values, int window)
        {
            var length = values.Length;
            var result = new float[length];
            if (window <= 1)
            {
                Array.Copy(values, result, length);
                return result;
            }

            var half = window / 2;
            var prefix = new double[length + 1];
            for (var i = 0; i < length; i++)
                prefix[i + 1] = prefix[i] + values[i];

            for (var i = 0; i < length; i++)
            {
                var from = Math.Max(0, i - half);
                var to = Math.Min(length, i + half + 1);
                result[i] = (float)((prefix[to] - prefix[from]) / (to - from));
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
