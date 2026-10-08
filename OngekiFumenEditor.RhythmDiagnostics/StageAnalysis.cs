using System.Reflection;
using OngekiFumenEditor.Kernel.Audio;
using OngekiFumenEditor.Kernel.Audio.Rhythm;
using OngekiFumenEditor.Modules.AudioPlayerToolViewer.Graphics.WaveformDrawing;

namespace OngekiFumenEditor.RhythmDiagnostics;

// Diagnostic-only: reuse production FFT, log compression, Gaussian smoothing and scaling.
// Retired mean/box stages remain here only as explicit research controls.
internal static class ProductionStages
{
    private static T Bind<T>(string name) where T : Delegate =>
        typeof(DefaultRhythmAnalyzer).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static)!
            .CreateDelegate<T>();

    internal static readonly Func<int, int[][]> BandBins = Bind<Func<int, int[][]>>("BuildBandBins");
    internal static readonly Func<float, float, float, float> LogLevel = Bind<Func<float, float, float, float>>("LogLevel");
    internal static readonly Func<double, float[]> GaussianKernel = Bind<Func<double, float[]>>("BuildGaussianKernel");
    internal static readonly Func<float[], float[], float[]> GaussianSmooth = Bind<Func<float[], float[], float[]>>("GaussianSmooth");
    internal static readonly Func<float[], int, float[]> LocalMax = Bind<Func<float[], int, float[]>>("SlidingMax");
    internal static readonly Func<float[], double, float> Percentile = Bind<Func<float[], double, float>>("Percentile");

    internal static float[] MeanSmooth(float[] values, int window)
    {
        if (window <= 1)
            return values;
        var result = new float[values.Length];
        var half = window / 2;
        var prefix = new double[values.Length + 1];
        for (var i = 0; i < values.Length; i++)
            prefix[i + 1] = prefix[i] + values[i];
        for (var i = 0; i < values.Length; i++)
        {
            var from = Math.Max(0, i - half);
            var to = Math.Min(values.Length, i + half + 1);
            result[i] = (float)((prefix[to] - prefix[from]) / (to - from));
        }
        return result;
    }
}

internal sealed record Spectrum(int SampleRate, int Hop, int FrameCount, int[][] Bins,
    float[] LogLevels, float[][] BandEnergy, float[] MonoRms, float[] ChannelRms, float[] HighRms)
{
    internal const int FftSize = 1024;
    internal const int BinCount = FftSize / 2 + 1;
    internal const int FrameRate = 200;

    internal static Spectrum Capture(float[] samples, int rate, int channels)
    {
        var sampleCount = samples.Length / channels;
        var hop = Math.Max(1, rate / FrameRate);
        var frameCount = sampleCount / hop + 1;
        var bins = ProductionStages.BandBins(rate)
            ?? throw new ArgumentException("Sample rate has an empty frequency band.");
        var levels = new float[frameCount * BinCount];
        var energy = CreateBands(frameCount);
        var mono = new float[sampleCount];
        var monoSquares = new double[frameCount];
        var channelSquares = new double[frameCount];
        var highSquares = new double[frameCount];
        var counts = new int[frameCount];
        var highPassA = (float)Math.Exp(-2 * Math.PI * 800 / rate);
        var previousInput = 0f;
        var previousOutput = 0f;
        for (var i = 0; i < sampleCount; i++)
        {
            var sum = 0f;
            var frame = i / hop;
            for (var c = 0; c < channels; c++)
            {
                var value = samples[i * channels + c];
                sum += value;
                channelSquares[frame] += (double)value * value / channels;
            }
            var mixed = sum / channels;
            mono[i] = mixed;
            var filtered = highPassA * (previousOutput + mixed - previousInput);
            previousInput = mixed;
            previousOutput = filtered; // Continuous filter, NOT reset at each RMS block.
            monoSquares[frame] += (double)mixed * mixed;
            highSquares[frame] += (double)filtered * filtered;
            counts[frame]++;
        }

        var window = new float[FftSize];
        var windowGain = 0f;
        for (var i = 0; i < FftSize; i++)
        {
            window[i] = 0.5f - 0.5f * MathF.Cos(2 * MathF.PI * i / FftSize);
            windowGain += window[i];
        }
        windowGain /= FftSize;
        var magnitudeScale = 2.0f / (FftSize * windowGain);
        var fft = new Radix2Fft(FftSize);
        var real = new float[FftSize];
        var imaginary = new float[FftSize];
        for (var frame = 0; frame < frameCount; frame++)
        {
            var start = frame * hop - FftSize / 2;
            for (var i = 0; i < FftSize; i++)
            {
                var sampleIndex = start + i;
                var value = (uint)sampleIndex < (uint)sampleCount ? mono[sampleIndex] : 0f;
                real[i] = value * window[i];
                imaginary[i] = 0f;
            }
            fft.Forward(real, imaginary);
            for (var k = 1; k < BinCount; k++)
                levels[frame * BinCount + k] = ProductionStages.LogLevel(real[k], imaginary[k], magnitudeScale);
            for (var band = 0; band < bins.Length; band++)
            {
                double power = 0;
                foreach (var k in bins[band])
                {
                    power += ((double)real[k] * real[k] + (double)imaginary[k] * imaginary[k])
                        * magnitudeScale * magnitudeScale / 2;
                }

                energy[band][frame] = (float)Math.Sqrt(power);
            }
        }

        float[] Rms(double[] squares) => squares.Select((sum, i) =>
            counts[i] == 0 ? 0 : (float)Math.Sqrt(sum / counts[i])).ToArray();
        return new(rate, hop, frameCount, bins, levels, energy,
            Rms(monoSquares), Rms(channelSquares), Rms(highSquares));
    }

    internal static float[][] CreateBands(int length) =>
        [new float[length], new float[length], new float[length]];
}

internal sealed record Variant(string Name, int ReferenceFrames, int SmoothMs,
    bool Difference = false, bool GlobalScale = false, CandidateSettings? Candidate = null,
    double GaussianSigmaMs = 0, float MinimumScale = 0, bool SharedScale = false)
{
    internal static Variant Baseline { get; } = new("freqmax15-floor03-scale0.1-perband", 3, 0,
        Candidate: new(MinimumRise: 0.03f), GaussianSigmaMs: 7.5, MinimumScale: 0.1f);

    internal static IEnumerable<Variant> All()
    {
        yield return Baseline;
        // Factorial comparison distinguishes the reference from the smoothing effect.
        foreach (var referenceMs in new[] { 150, 80, 50, 30 })
        {
            foreach (var smoothMs in new[] { 50, 25, 10, 0 })
                yield return new($"mean{referenceMs}-smooth{smoothMs}", referenceMs / 5, smoothMs);
        }

        yield return new("mean150-smooth50-globalScale", 30, 50, GlobalScale: true);
        foreach (var lagMs in new[] { 5, 15, 30 })
        {
            foreach (var smoothMs in new[] { 50, 10, 0 })
                yield return new($"delta{lagMs}-smooth{smoothMs}", lagMs / 5, smoothMs, Difference: true);
        }

        yield return new("freqmax15-r1-smooth10", 3, 10, Candidate: new());
        yield return new("freqmax15-r2-smooth10", 3, 10, Candidate: new(FrequencyRadius: 2));
        yield return new("freqmax15-r1-sigma7.5", 3, 0, Candidate: new(), GaussianSigmaMs: 7.5);
        yield return new("freqmax15-r1-floor03", 3, 10, Candidate: new(MinimumRise: 0.03f));
        yield return new("freqmax15-r1-adaptive", 3, 10, Candidate: new(AdaptiveFloor: true));
        yield return new("freqmax15-r2-adaptive", 3, 10, Candidate: new(FrequencyRadius: 2, AdaptiveFloor: true));
        yield return new("freqmax15-r1-adaptive-sigma5", 3, 0,
            Candidate: new(AdaptiveFloor: true), GaussianSigmaMs: 5);
        yield return new("freqmax15-r1-adaptive-sigma7.5", 3, 0,
            Candidate: new(AdaptiveFloor: true), GaussianSigmaMs: 7.5);
        yield return new("energy15-smooth10", 3, 10, Candidate: new(CandidateKind.BandEnergy));
        yield return new("energy15-floor03", 3, 10,
            Candidate: new(CandidateKind.BandEnergy, MinimumRise: 0.03f));
        yield return new("energy15-adaptive", 3, 10,
            Candidate: new(CandidateKind.BandEnergy, AdaptiveFloor: true));
        yield return new("energy15-adaptive-sigma7.5", 3, 0,
            Candidate: new(CandidateKind.BandEnergy, AdaptiveFloor: true), GaussianSigmaMs: 7.5);
        yield return new("freqmax15-sigma7.5-scale03", 3, 0,
            Candidate: new(), GaussianSigmaMs: 7.5, MinimumScale: 0.03f);
        yield return new("freqmax15-floor03-sigma7.5-scale03", 3, 0,
            Candidate: new(MinimumRise: 0.03f), GaussianSigmaMs: 7.5, MinimumScale: 0.03f);
        foreach (var factor in new[] { 2f, 3f, 4f })
        {
            yield return new($"energy-signed{factor}-perband", 3, 0,
                Candidate: new(CandidateKind.BandEnergySignedNoise, NoiseMadFactor: factor),
                GaussianSigmaMs: 7.5, MinimumScale: 0.03f);
            yield return new($"energy-signed{factor}-shared", 3, 0,
                Candidate: new(CandidateKind.BandEnergySignedNoise, NoiseMadFactor: factor),
                GaussianSigmaMs: 7.5, MinimumScale: 0.03f, SharedScale: true);
        }
        foreach (var scale in new[] { 0.1f, 0.3f, 0.6f })
        {
            if (scale != 0.1f)
                yield return new($"freqmax15-floor03-scale{scale:0.0}-perband", 3, 0,
                    Candidate: new(MinimumRise: 0.03f), GaussianSigmaMs: 7.5, MinimumScale: scale);
            yield return new($"freqmax15-floor03-scale{scale:0.0}-shared", 3, 0,
                Candidate: new(MinimumRise: 0.03f), GaussianSigmaMs: 7.5,
                MinimumScale: scale, SharedScale: true);
        }
        foreach (var minimum in new[] { 0.03f, 0.06f })
        {
            foreach (var full in new[] { 0.10f, 0.15f })
            {
                yield return new($"coherent-m{minimum * 100:00}-f{full * 100:00}", 3, 0,
                    Candidate: new(MinimumRise: 0.03f, EnergyGateMinimum: minimum, EnergyGateFull: full),
                    GaussianSigmaMs: 7.5, MinimumScale: 0.1f);
            }
        }
    }
}

internal sealed record StageResult(Variant Variant, float[][] Flux, float[][] Smoothed,
    float[][] Normalized, float[] RawTotal, float[] SmoothedTotal, float[] Total, float[] Enhanced)
{
    private static readonly float[] Weights = [1f, 1f, 0.6f];

    internal static StageResult Analyze(Spectrum spectrum, Variant variant)
    {
        var flux = variant.Candidate is null
            ? Spectrum.CreateBands(spectrum.FrameCount)
            : ImprovementAnalysis.Compute(spectrum, variant.Candidate);
        if (variant.Candidate is null)
        {
            var historyLength = variant.ReferenceFrames;
            var history = new float[Spectrum.BinCount * historyLength];
            var historySum = new float[Spectrum.BinCount];
            var cursor = 0;
            var rise = new float[Spectrum.BinCount];
            for (var frame = 0; frame < spectrum.FrameCount; frame++)
            {
                for (var k = 1; k < Spectrum.BinCount; k++)
                {
                    var level = spectrum.LogLevels[frame * Spectrum.BinCount + k];
                    if (frame == 0)
                    {
                        historySum[k] = level * historyLength;
                        for (var slot = 0; slot < historyLength; slot++)
                            history[k * historyLength + slot] = level;
                        continue;
                    }
                    var reference = variant.Difference
                        ? spectrum.LogLevels[Math.Max(0, frame - historyLength) * Spectrum.BinCount + k]
                        : historySum[k] / historyLength;
                    var delta = level - reference;
                    rise[k] = delta > 0 ? delta : 0;
                    var slotIndex = k * historyLength + cursor;
                    historySum[k] += level - history[slotIndex];
                    history[slotIndex] = level;
                }
                if (frame > 0)
                {
                    for (var band = 0; band < spectrum.Bins.Length; band++)
                    {
                        var sum = 0f;
                        foreach (var k in spectrum.Bins[band])
                            sum += rise[k];
                        flux[band][frame] = sum / spectrum.Bins[band].Length;
                    }
                }

                cursor = cursor + 1 >= historyLength ? 0 : cursor + 1;
            }
        }

        var smoothed = Spectrum.CreateBands(spectrum.FrameCount);
        var normalized = Spectrum.CreateBands(spectrum.FrameCount);
        var gaussianKernel = variant.GaussianSigmaMs > 0
            ? ProductionStages.GaussianKernel(variant.GaussianSigmaMs / 1000) : null;
        for (var band = 0; band < flux.Length; band++)
        {
            var smoothFrames = Math.Max(1, variant.SmoothMs * Spectrum.FrameRate / 1000);
            smoothed[band] = gaussianKernel is null
                ? ProductionStages.MeanSmooth(flux[band], smoothFrames)
                : ProductionStages.GaussianSmooth(flux[band], gaussianKernel);
            if (variant.SharedScale)
                continue;
            var localScale = ProductionStages.LocalMax(smoothed[band], 3 * Spectrum.FrameRate);
            var scaleFloor = MathF.Max(variant.MinimumScale,
                ProductionStages.Percentile(flux[band], 0.95) * 0.25f);
            var globalScale = smoothed[band].Max();
            for (var i = 0; i < spectrum.FrameCount; i++)
            {
                var scale = variant.GlobalScale ? globalScale : MathF.Max(localScale[i], scaleFloor);
                if (scale > 0)
                    normalized[band][i] = Math.Clamp(smoothed[band][i] / scale, 0, 1);
            }
        }
        if (variant.SharedScale)
        {
            var mixed = Mix(smoothed);
            var localScale = ProductionStages.LocalMax(mixed, 3 * Spectrum.FrameRate);
            var scaleFloor = MathF.Max(variant.MinimumScale,
                ProductionStages.Percentile(Mix(flux), 0.95) * 0.25f);
            for (var i = 0; i < spectrum.FrameCount; i++)
            {
                var scale = MathF.Max(localScale[i], scaleFloor);
                if (scale <= 0)
                    continue;
                // Components share one scale and may exceed 1 individually;
                // their weighted total remains the bounded consumer envelope.
                for (var band = 0; band < normalized.Length; band++)
                    normalized[band][i] = smoothed[band][i] / scale;
            }
        }

        var total = Mix(normalized);
        var enhanced = new float[total.Length];
        RhythmCurveTone.FromIntensity(RhythmCurveIntensity.Enhanced)
            .Apply(total, enhanced, new float[total.Length], Spectrum.FrameRate);
        return new(variant, flux, smoothed, normalized, Mix(flux), Mix(smoothed), total, enhanced);
    }

    private static float[] Mix(float[][] bands)
    {
        var result = new float[bands[0].Length];
        var weightSum = Weights.Sum();
        for (var band = 0; band < bands.Length; band++)
        {
            var weight = Weights[band] / weightSum;
            for (var i = 0; i < result.Length; i++)
                result[i] += bands[band][i] * weight;
        }
        return result;
    }


    internal static float VerifyBaseline(SampleData data, StageResult diagnostic)
    {
        var production = new DefaultRhythmAnalyzer().Analyze(data)
            ?? throw new InvalidOperationException("Production analyzer returned null.");
        if (production.FrameCount != diagnostic.Total.Length)
            throw new InvalidOperationException("Diagnostic frame count differs from production.");
        var maxDifference = 0f;
        for (var i = 0; i < production.FrameCount; i++)
        {
            if (!float.IsFinite(diagnostic.Total[i]))
                throw new InvalidOperationException($"Non-finite diagnostic value at frame {i}.");
            maxDifference = Math.Max(maxDifference, MathF.Abs(production.Total[i] - diagnostic.Total[i]));
        }
        if (maxDifference > 1e-6f)
            throw new InvalidOperationException($"Diagnostic baseline mismatch: {maxDifference:G9}");
        return maxDifference;
    }
}
