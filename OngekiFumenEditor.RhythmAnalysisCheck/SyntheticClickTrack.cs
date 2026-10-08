using OngekiFumenEditor.Kernel.Audio.Rhythm;

namespace OngekiFumenEditor.RhythmAnalysisCheck;

/// <summary>合成曲目：交错浮点 PCM + 真实击打时刻（ground truth）。</summary>
internal sealed record SyntheticTrack(float[] Samples, int SampleRate, int Channels,
    IReadOnlyList<TimeSpan> HitTimes, TimeSpan Duration)
{
    public OngekiFumenEditor.Kernel.Audio.SampleData ToSampleData()
    {
        var bytes = new byte[Samples.Length * sizeof(float)];
        Buffer.BlockCopy(Samples, 0, bytes, 0, bytes.Length);
        return new(bytes, new OngekiFumenEditor.Kernel.Audio.SampleInfo
        {
            SampleRate = SampleRate,
            Channels = Channels,
            BitsPerSample = 32,
        });
    }
}

/// <summary>
/// 合成测试信号：底鼓/军鼓/踩镲 + 一条持续和弦（验证「持续音被背景抑制、鼓点仍可见」）。
/// 所有随机源固定种子，用例可复现。
/// </summary>
internal static class SyntheticClickTrack
{
    private const int DefaultSampleRate = 48000;
    private const int DefaultChannels = 2;

    /// <summary>标准 4/4 鼓组：正拍交替底鼓/军鼓，反拍踩镲，另叠一条持续和弦。</summary>
    public static SyntheticTrack CreateDrumPattern(double bpm, double seconds,
        int sampleRate = DefaultSampleRate, int channels = DefaultChannels, bool includePad = true)
    {
        var samples = new float[(int)(sampleRate * seconds) * channels];
        var hits = new List<TimeSpan>();
        var random = new Random(20261008);
        var beatSeconds = 60.0 / bpm;

        var index = 0;
        for (var time = 0.0; time + beatSeconds * 0.5 < seconds; time += beatSeconds, index++)
        {
            if (index % 2 == 0)
                AddKick(samples, sampleRate, channels, time);
            else
                AddSnare(samples, sampleRate, channels, time, random);
            hits.Add(TimeSpan.FromSeconds(time));

            var offbeat = time + beatSeconds * 0.5;
            if (offbeat + 0.1 < seconds)
            {
                AddHat(samples, sampleRate, channels, offbeat, random);
                hits.Add(TimeSpan.FromSeconds(offbeat));
            }
        }

        if (includePad)
            AddPad(samples, sampleRate, channels, seconds);

        return new SyntheticTrack(samples, sampleRate, channels, hits, TimeSpan.FromSeconds(seconds));
    }

    /// <summary>只有反拍踩镲（高频、冲击型瞬态）：检验高频内容也能被曲线看见。</summary>
    public static SyntheticTrack CreateHatOnly(double bpm, double seconds,
        int sampleRate = DefaultSampleRate, int channels = DefaultChannels)
    {
        var samples = new float[(int)(sampleRate * seconds) * channels];
        var hits = new List<TimeSpan>();
        var random = new Random(20261008);
        var beatSeconds = 60.0 / bpm;

        for (var time = 0.0; time + 0.2 < seconds; time += beatSeconds)
        {
            AddHat(samples, sampleRate, channels, time, random);
            hits.Add(TimeSpan.FromSeconds(time));
        }

        return new SyntheticTrack(samples, sampleRate, channels, hits, TimeSpan.FromSeconds(seconds));
    }

    /// <summary>只有持续和弦，没有任何击打：曲线应当是平的（不该被噪声抬出一堆假峰）。</summary>
    public static SyntheticTrack CreatePadOnly(double seconds,
        int sampleRate = DefaultSampleRate, int channels = DefaultChannels)
    {
        var samples = new float[(int)(sampleRate * seconds) * channels];
        AddPad(samples, sampleRate, channels, seconds);
        return new SyntheticTrack(samples, sampleRate, channels, Array.Empty<TimeSpan>(), TimeSpan.FromSeconds(seconds));
    }

    /// <summary>整段静音。</summary>
    public static SyntheticTrack CreateSilence(double seconds,
        int sampleRate = DefaultSampleRate, int channels = DefaultChannels)
        => new(new float[(int)(sampleRate * seconds) * channels], sampleRate, channels,
            Array.Empty<TimeSpan>(), TimeSpan.FromSeconds(seconds));

    private static void AddKick(float[] samples, int sampleRate, int channels, double start)
    {
        var length = (int)(0.18 * sampleRate);
        var startIndex = (int)(start * sampleRate);
        var phase = 0.0;

        for (var i = 0; i < length; i++)
        {
            var index = startIndex + i;
            if (index >= samples.Length / channels)
                break;

            var t = (double)i / sampleRate;
            var frequency = 48 + 92 * Math.Exp(-t / 0.018);      // 音高快速下滑的底鼓
            phase += 2 * Math.PI * frequency / sampleRate;
            var value = (float)(0.9 * Math.Exp(-t / 0.10) * Math.Sin(phase));
            Add(samples, channels, index, value);
        }
    }

    private static void AddSnare(float[] samples, int sampleRate, int channels, double start, Random random)
    {
        var length = (int)(0.15 * sampleRate);
        var startIndex = (int)(start * sampleRate);

        for (var i = 0; i < length; i++)
        {
            var index = startIndex + i;
            if (index >= samples.Length / channels)
                break;

            var t = (double)i / sampleRate;
            var noise = (float)(random.NextDouble() * 2 - 1);
            var tone = (float)Math.Sin(2 * Math.PI * 190 * t);
            var value = (float)(Math.Exp(-t / 0.07) * (0.45 * noise + 0.25 * tone));
            Add(samples, channels, index, value);
        }
    }

    private static void AddHat(float[] samples, int sampleRate, int channels, double start, Random random)
    {
        var length = (int)(0.06 * sampleRate);
        var startIndex = (int)(start * sampleRate);
        var previousInput = 0f;
        var previousOutput = 0f;

        for (var i = 0; i < length; i++)
        {
            var index = startIndex + i;
            if (index >= samples.Length / channels)
                break;

            var t = (double)i / sampleRate;
            var noise = (float)(random.NextDouble() * 2 - 1);
            // 一阶高通，把噪声推到高频段（踩镲）
            var filtered = 0.92f * (previousOutput + noise - previousInput);
            previousInput = noise;
            previousOutput = filtered;
            var value = (float)(0.22 * Math.Exp(-t / 0.025)) * filtered;
            Add(samples, channels, index, value);
        }
    }

    private static void AddPad(float[] samples, int sampleRate, int channels, double seconds)
    {
        var length = (int)(seconds * sampleRate);
        double[] partials = { 220, 277.18, 329.63, 440 };

        for (var i = 0; i < length; i++)
        {
            var t = (double)i / sampleRate;
            var value = 0.0;
            foreach (var frequency in partials)
                value += Math.Sin(2 * Math.PI * frequency * t);

            Add(samples, channels, i, (float)(0.06 * value / partials.Length));
        }
    }

    private static void Add(float[] samples, int channels, int frameIndex, float value)
    {
        for (var c = 0; c < channels; c++)
            samples[frameIndex * channels + c] += value;
    }
}
