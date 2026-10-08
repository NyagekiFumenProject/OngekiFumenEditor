using NAudio.Wave;
using OngekiFumenEditor.Kernel.Audio;
using OngekiFumenEditor.Kernel.Audio.Rhythm;
using OngekiFumenEditor.Modules.AudioPlayerToolViewer.Graphics.WaveformDrawing;
using System.Diagnostics;
using System.IO;
using System.Numerics;
using static OngekiFumenEditor.Kernel.Graphics.ILineDrawing;

namespace OngekiFumenEditor.RhythmAnalysisCheck;

/// <summary>
/// 节奏曲线校验：
/// <list type="bullet">
/// <item>默认跑合成用例（已知击打时刻的鼓组、纯踩镲、持续和弦、静音）与几何构造用例；</item>
/// <item>带 <c>--audio &lt;file&gt;</c> 时跑真实音频，打印曲线统计并可导出对照图（<c>--out &lt;png&gt;</c>）。</item>
/// </list>
/// 退出码非 0 表示有用例失败，可直接当作回归检查使用。
/// 这里断言的是「曲线像不像鼓点轨」：有击打时应当峰峦分明且峰落在击打上，
/// 只有持续音时应当是平的——而不是断言某帧的具体数值。
/// </summary>
internal static class Program
{
    private static int checkCount;
    private static int failureCount;

    public static int Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        var options = Options.Parse(args);
        try
        {
            if (options.AudioPath is { } audioPath)
                RunAudioFile(audioPath, options);
            else
                RunSyntheticChecks();
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            return 2;
        }

        Console.WriteLine($"checks: {checkCount - failureCount}/{checkCount} passed");
        return failureCount == 0 ? 0 : 1;
    }

    private static void RunSyntheticChecks()
    {
        var analyzer = new DefaultRhythmAnalyzer();

        // 1) 4/4 鼓组 + 持续和弦：曲线要有鼓点样，且峰值落在击打上
        var drums = SyntheticClickTrack.CreateDrumPattern(150, 30);
        var drumCurve = Measure(analyzer, drums);
        CheckPeaky(drumCurve, "drum-150", minP90OverMean: 2.0);
        CheckHitsVisible(drumCurve, drums.HitTimes, "drum-150", toleranceMs: 30, minRatio: 1.8);

        // 2) 只有反拍踩镲（高频瞬态）：同样要被曲线看见
        var hats = SyntheticClickTrack.CreateHatOnly(150, 30);
        var hatCurve = Measure(analyzer, hats);
        CheckHitsVisible(hatCurve, hats.HitTimes, "hat-150", toleranceMs: 30, minRatio: 2.0);

        // 3) 只有持续和弦：曲线必须是平的（不能被噪声抬出一堆假峰）
        var pad = SyntheticClickTrack.CreatePadOnly(10);
        var padCurve = Measure(analyzer, pad);
        CheckFlat(padCurve, "pad-only", maxP90OverMean: 1.5);

        // 4) 静音：曲线全零
        var silence = SyntheticClickTrack.CreateSilence(10);
        var silenceCurve = Measure(analyzer, silence);
        Check(silenceCurve is not null && silenceCurve.Total.All(x => x == 0f), "silence: 曲线全零",
            silenceCurve is null ? "分析返回 null" : $"max={silenceCurve.Total.Max():F3}");

        // 5) 采样格式不支持时安静地返回 null，而不是画出错误结果
        var wrongFormat = new SampleData(new byte[48000 * 4 * 2],
            new SampleInfo { SampleRate = 48000, Channels = 2, BitsPerSample = 16 });
        Check(analyzer.Analyze(wrongFormat) is null, "16bit 数据: 返回 null", "ok");

        CheckGeometry();
        CheckToneMapping(analyzer);
    }

    /// <summary>
    /// 显示侧色调映射（强调 + γ）：恒等、平坦不造假峰、峰谷对比确实被拉开，
    /// 以及真实合成曲线走一遍默认参数后的 p90/mean 变化（这就是「峰谷更明显」的量化口径）。
    /// </summary>
    private static void CheckToneMapping(DefaultRhythmAnalyzer analyzer)
    {
        var frameInterval = TimeSpan.FromSeconds(1.0 / 200);
        var frameCount = 4000;

        // 1) 恒等参数不改变任何数值
        var source = new float[frameCount];
        for (var i = 0; i < frameCount; i++)
            source[i] = 0.2f + 0.6f * (i % 37) / 37f;
        var mapped = new float[frameCount];
        var scratch = new float[frameCount];
        RhythmCurveTone.Identity.Apply(source, mapped, scratch, 200);
        Check(source.Zip(mapped).All(p => MathF.Abs(p.First - p.Second) < 1e-6f), "tone: 恒等参数不改变数值", "");

        // 2) 平坦曲线保持平坦：强调项不会凭空造峰（v - blur(v) == 0）
        var flat = new float[frameCount];
        Array.Fill(flat, 0.42f);
        RhythmCurveTone.Default.Apply(flat, mapped, scratch, 200);
        Check(mapped.Max() - mapped.Min() < 1e-3f, "tone: 平坦曲线保持平坦（不造假峰）",
            $"max-min={mapped.Max() - mapped.Min():F4}");

        // 3) 峰谷对比：0.25 基线 + 每 40 帧一个峰
        var spiky = new float[frameCount];
        for (var i = 0; i < frameCount; i++)
            spiky[i] = i % 40 == 0 ? 1f : 0.25f;
        RhythmCurveTone.Default.Apply(spiky, mapped, scratch, 200);
        var peakValue = Enumerable.Range(0, frameCount / 40).Min(k => mapped[k * 40]);
        var baselineBefore = spiky.Where((_, i) => i % 40 != 0).Average();
        var baselineAfter = mapped.Where((_, i) => i % 40 != 0).Average();
        var peakBefore = spiky.Where((_, i) => i % 40 == 0).Average();
        var peakAfter = mapped.Where((_, i) => i % 40 == 0).Average();
        var contrastBefore = baselineBefore > 0 ? peakBefore / baselineBefore : 0;
        var contrastAfter = baselineAfter > 0 ? peakAfter / baselineAfter : 0;
        Check(baselineAfter <= baselineBefore * 0.7f, "tone: 基线被压低",
            $"{baselineBefore:F3} -> {baselineAfter:F3}");
        Check(peakValue >= 0.9f, "tone: 峰顶仍保持满幅", $"min peak={peakValue:F3}");
        Check(contrastAfter >= contrastBefore * 1.8f, "tone: 峰谷比明显拉开",
            $"{contrastBefore:F2} -> {contrastAfter:F2}");

        // 4) 端到端：真实合成曲线（150BPM 鼓组）走默认映射后的 p90/mean
        var drums = SyntheticClickTrack.CreateDrumPattern(150, 30);
        var envelope = analyzer.Analyze(drums.ToSampleData());
        if (envelope is null)
        {
            Check(false, "tone: 端到端对比", "分析返回 null");
            return;
        }

        var raw = new float[envelope.FrameCount];
        Array.Copy(envelope.Total, raw, raw.Length);
        var toned = new float[envelope.FrameCount];
        var toneScratch = new float[envelope.FrameCount];
        RhythmCurveTone.Default.Apply(raw, toned, toneScratch, envelope.FrameRateHz);

        // 面板上的观感 = 峰高 / 基线：基线（p50）必须明显下降，峰顶必须保持
        var rawSorted = raw.OrderBy(x => x).ToArray();
        var tonedSorted = toned.OrderBy(x => x).ToArray();
        var rawMedian = rawSorted[rawSorted.Length / 2];
        var tonedMedian = tonedSorted[tonedSorted.Length / 2];
        Check(tonedMedian <= rawMedian * 0.7f, "tone: 鼓组曲线基线（p50）被压低",
            $"{rawMedian:F3} -> {tonedMedian:F3}");
        Check(tonedSorted[^1] >= 0.95f, "tone: 鼓组曲线峰顶保持", $"max={tonedSorted[^1]:F3}");
        Console.WriteLine($"      tone: 鼓组 p90/mean {Stats(raw).P90 / Stats(raw).Mean:F2} -> {Stats(toned).P90 / Stats(toned).Mean:F2}, " +
            $"占比≥0.5 {(raw.Count(x => x >= 0.5f) / (double)raw.Length):P1} -> {(toned.Count(x => x >= 0.5f) / (double)toned.Length):P1}");
    }

    private static (double Mean, double P90) Stats(float[] values)
    {
        var sorted = values.OrderBy(x => x).ToArray();
        return (sorted.Average(), sorted[(int)(sorted.Length * 0.9)]);
    }

    private static RhythmEnvelope Measure(IRhythmAnalyzer analyzer, SyntheticTrack track)
    {
        var stopwatch = Stopwatch.StartNew();
        var envelope = analyzer.Analyze(track.ToSampleData());
        stopwatch.Stop();
        Console.WriteLine($"[{track.Duration.TotalSeconds:F0}s] 分析耗时 {stopwatch.ElapsedMilliseconds}ms, 帧数 {envelope?.FrameCount.ToString() ?? "null"}");
        return envelope;
    }

    /// <summary>琴键分明：p90 明显高于均值（持续音的曲线两者接近）。</summary>
    private static void CheckPeaky(RhythmEnvelope envelope, string name, double minP90OverMean)
    {
        if (envelope is null)
        {
            Check(false, $"{name}: 曲线峰峦分明", "分析返回 null");
            return;
        }

        var (mean, p90) = Stats(envelope);
        var ratio = mean > 0 ? p90 / mean : 0;
        Check(ratio >= minP90OverMean, $"{name}: p90/mean {ratio:F2} ≥ {minP90OverMean}",
            $"mean={mean:F3} p90={p90:F3}");
    }

    /// <summary>曲线应当平坦：持续和弦不该被抬出假峰。</summary>
    private static void CheckFlat(RhythmEnvelope envelope, string name, double maxP90OverMean)
    {
        if (envelope is null)
        {
            Check(false, $"{name}: 曲线保持平坦", "分析返回 null");
            return;
        }

        var (mean, p90) = Stats(envelope);
        var ratio = mean > 0 ? p90 / mean : 0;
        Check(ratio <= maxP90OverMean, $"{name}: p90/mean {ratio:F2} ≤ {maxP90OverMean}",
            $"mean={mean:F3} p90={p90:F3}");
    }

    /// <summary>
    /// 击打可见：每个击打点都要是「相对紧邻两侧的凸起」（≥1.5 倍局部地板），
    /// 且击打处的平均强度明显高于整体平均（重音结构成立）。
    /// 判据不使用绝对高度——合奏里轻的反拍踩镲本来就比底鼓矮，画出来仍应是可见的小包。
    /// </summary>
    private static void CheckHitsVisible(RhythmEnvelope envelope, IReadOnlyList<TimeSpan> hits, string name,
        double toleranceMs, double minRatio)
    {
        if (envelope is null)
        {
            Check(false, $"{name}: 击打可见", "分析返回 null");
            return;
        }

        var core = Math.Max(1, (int)(toleranceMs / 1000.0 * envelope.FrameRateHz));
        var outer = core * 2;
        var visible = 0;
        double hitSum = 0;

        foreach (var hit in hits)
        {
            var index = (int)(hit.TotalSeconds * envelope.FrameRateHz);

            var peak = 0f;
            for (var i = Math.Max(0, index - core); i <= Math.Min(envelope.FrameCount - 1, index + core); i++)
                peak = Math.Max(peak, envelope.Total[i]);

            // 紧邻两侧（不含峰自身）的平均值作为局部地板
            double floorSum = 0;
            var floorCount = 0;
            for (var i = Math.Max(0, index - outer); i < index - core; i++)
            {
                floorSum += envelope.Total[i];
                floorCount++;
            }
            for (var i = index + core + 1; i <= Math.Min(envelope.FrameCount - 1, index + outer); i++)
            {
                floorSum += envelope.Total[i];
                floorCount++;
            }

            var floor = floorCount > 0 ? MathF.Max((float)(floorSum / floorCount), 1e-4f) : 1e-4f;
            hitSum += peak;
            if (peak >= MathF.Max(floor * 1.5f, 0.02f))
                visible++;
        }

        var (mean, _) = Stats(envelope);
        var hitMean = hitSum / Math.Max(1, hits.Count);
        var ratio = mean > 0 ? hitMean / mean : 0;

        Check(visible >= hits.Count * 0.9, $"{name}: 击打处是局部凸起 {visible}/{hits.Count} ≥ 90%", "");
        Check(ratio >= minRatio, $"{name}: 击打处均值/整体均值 {ratio:F2} ≥ {minRatio}",
            $"hitMean={hitMean:F3} mean={mean:F3}");
    }

    private static (double Mean, double P90) Stats(RhythmEnvelope envelope)
    {
        var sorted = envelope.Total.OrderBy(x => x).ToArray();
        return (sorted.Average(), sorted[(int)(sorted.Length * 0.9)]);
    }

    private static void CheckGeometry()
    {
        // 30 秒、200fps 的曲线：一个尖峰位于 15s；100 像素宽的窗口 → 100 列
        var frameInterval = TimeSpan.FromSeconds(1.0 / 200);
        var frameCount = 6000;
        var total = new float[frameCount];
        total[3000] = 1f;
        var envelope = new RhythmEnvelope(frameInterval, total);

        var points = new List<LineVertex>();
        RhythmGeometry.BuildCurve(points, envelope, TimeSpan.Zero, TimeSpan.FromSeconds(30), 100, 200, 0.8f, Vector4.One, RhythmCurveTone.Identity);
        Check(points.Count == 200, "geometry: 曲线顶点数 = 2×列数", $"actual={points.Count}");

        var monotonic = true;
        var symmetric = true;
        var maxY = 0f;
        var spikeY = 0f;
        for (var column = 0; column < points.Count / 2; column++)
        {
            var top = points[column * 2];
            var bottom = points[column * 2 + 1];
            if (top.Point.X != bottom.Point.X)
                symmetric = false;
            if (MathF.Abs(top.Point.Y + bottom.Point.Y) > 1e-4f)
                symmetric = false;
            if (column > 0 && top.Point.X <= points[(column - 1) * 2].Point.X)
                monotonic = false;

            maxY = MathF.Max(maxY, top.Point.Y);
            if (column == 50)
                spikeY = top.Point.Y;
        }

        Check(symmetric, "geometry: 曲线上下对称", "");
        Check(monotonic, "geometry: 曲线 x 单调递增", "");
        Check(MathF.Abs(spikeY - 200f / 2 * 0.8f) < 1e-3f, "geometry: 尖峰落在满幅高度",
            $"actual={spikeY:F3} expected={200f / 2 * 0.8f:F3}");
        Check(maxY <= 200f / 2 * 0.8f + 1e-3f, "geometry: 曲线不超界", $"maxY={maxY:F3}");

        // 只看 5 秒窗口时，窗口之外的尖峰不能出现
        points.Clear();
        RhythmGeometry.BuildCurve(points, envelope, TimeSpan.Zero, TimeSpan.FromSeconds(5), 100, 200, 0.8f, Vector4.One, RhythmCurveTone.Identity);
        Check(points.Count == 200 && points.All(x => MathF.Abs(x.Point.Y) < 1e-4f),
            "geometry: 窗口外的尖峰不参与绘制", $"vertices={points.Count}");
    }

    private static void RunAudioFile(string audioPath, Options options)
    {
        if (!File.Exists(audioPath))
        {
            Console.WriteLine($"audio file not found: {audioPath}");
            Environment.Exit(2);
            return;
        }

        var samples = LoadAudio(audioPath, out var sampleRate, out var channels);
        var duration = TimeSpan.FromSeconds((double)samples.Length / channels / sampleRate);
        Console.WriteLine($"loaded {audioPath}: {duration.TotalSeconds:F2}s {sampleRate}Hz {channels}ch");

        var sampleBytes = new byte[samples.Length * sizeof(float)];
        Buffer.BlockCopy(samples, 0, sampleBytes, 0, sampleBytes.Length);
        var data = new SampleData(sampleBytes,
            new SampleInfo { SampleRate = sampleRate, Channels = channels, BitsPerSample = 32 });

        var analyzer = new DefaultRhythmAnalyzer();
        var stopwatch = Stopwatch.StartNew();
        var envelope = analyzer.Analyze(data);
        stopwatch.Stop();

        if (envelope is null)
        {
            Console.WriteLine("analysis returned null");
            Environment.Exit(2);
            return;
        }

        var (mean, p90) = Stats(envelope);
        var above = envelope.Total.Count(x => x >= 0.5f) / (double)envelope.FrameCount;
        Console.WriteLine($"curve: {stopwatch.ElapsedMilliseconds}ms, frames={envelope.FrameCount} ({envelope.FrameRateHz:F0}fps), " +
            $"mean={mean:F3} p90={p90:F3} max={envelope.Total.Max():F3} 占比≥0.5 {above:P1} " +
            $"p90/mean={(mean > 0 ? p90 / mean : 0):F2}");

        // 面板画的是映射之后的曲线，这里把同一套默认参数也跑一遍，便于用数字对照
        var toned = new float[envelope.FrameCount];
        var toneScratch = new float[envelope.FrameCount];
        RhythmCurveTone.Default.Apply(envelope.Total, toned, toneScratch, envelope.FrameRateHz);
        var tonedStats = Stats(toned);
        Console.WriteLine($"curve(默认映射 γ={RhythmCurveTone.DefaultGamma} λ={RhythmCurveTone.DefaultEmphasis}): " +
            $"mean={tonedStats.Mean:F3} p90={tonedStats.P90:F3} max={toned.Max():F3} " +
            $"p90/mean={(tonedStats.Mean > 0 ? tonedStats.P90 / tonedStats.Mean : 0):F2}");

        if (options.OutputPath is { } outputPath)
        {
            var from = options.StartSeconds is { } start ? TimeSpan.FromSeconds(start) : TimeSpan.Zero;
            var to = options.WindowSeconds is { } window ? from + TimeSpan.FromSeconds(window) : duration;
            if (to > duration)
                to = duration;

            SpectrumImageWriter.Write(outputPath, envelope, from, to, options.ExpectedBpm, Path.GetFileName(audioPath));
        }
    }

    private static float[] LoadAudio(string path, out int sampleRate, out int channels)
    {
        using var reader = new AudioFileReader(path);
        sampleRate = reader.WaveFormat.SampleRate;
        channels = reader.WaveFormat.Channels;

        var provider = reader.ToSampleProvider();
        var buffer = new float[reader.WaveFormat.SampleRate * reader.WaveFormat.Channels];
        var samples = new List<float>(buffer.Length);
        int read;
        while ((read = provider.Read(buffer, 0, buffer.Length)) > 0)
            samples.AddRange(buffer.AsSpan(0, read));

        return samples.ToArray();
    }

    private static void Check(bool condition, string name, string detail)
    {
        checkCount++;
        if (condition)
        {
            Console.WriteLine($"PASS  {name}" + (detail.Length > 0 ? $"  [{detail}]" : ""));
        }
        else
        {
            failureCount++;
            Console.WriteLine($"FAIL  {name}  [{detail}]");
        }
    }

    private sealed record Options(string? AudioPath, string? OutputPath, double? ExpectedBpm, double? StartSeconds, double? WindowSeconds)
    {
        public static Options Parse(string[] args)
        {
            string? audio = null, outPath = null;
            double? expectBpm = null, start = null, window = null;

            for (var i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--audio" when i + 1 < args.Length: audio = args[++i]; break;
                    case "--out" when i + 1 < args.Length: outPath = args[++i]; break;
                    case "--expect-bpm" when i + 1 < args.Length: expectBpm = double.Parse(args[++i]); break;
                    case "--start" when i + 1 < args.Length: start = double.Parse(args[++i]); break;
                    case "--window" when i + 1 < args.Length: window = double.Parse(args[++i]); break;
                }
            }

            return new Options(audio, outPath, expectBpm, start, window);
        }
    }
}
