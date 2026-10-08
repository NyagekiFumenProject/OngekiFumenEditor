using System.Reflection;
using OngekiFumenEditor.RhythmAnalysisCheck;

namespace OngekiFumenEditor.RhythmDiagnostics;

/// <summary>
/// Additional envelope-ground-truth signals, not chart labels or a perceptual oracle.
/// Controls have no attack labels: moving spectral energy (glide/pitch vibrato), chord
/// beating, and stationary random fluctuations are intentionally not amplitude attacks.
/// A 100ms boundary taper is outside the scored region; no boundary is labeled.
/// </summary>
internal static class ImprovementSignals
{
    private const int Rate = 48000;
    private const int Channels = 2;
    private const int Seconds = 8;
    private const int Frames = Rate * Seconds;

    // Reuse the same drum voice as the public factories, following SignalCases' convention.
    // The public hat factory fixes its random seed, so this delegate permits a new realization.
    private static readonly Action<float[], int, int, double, Random> AddHat =
        typeof(SyntheticClickTrack).GetMethod("AddHat", BindingFlags.NonPublic | BindingFlags.Static)!
            .CreateDelegate<Action<float[], int, int, double, Random>>();

    internal static IEnumerable<(string Name, SyntheticTrack Track)> All(bool holdout = false)
    {
        if (!holdout)
        {
            yield return ("research-steady-sine-437hz", ToneControl(437, 437, 0.18));
            yield return ("research-constant-amplitude-glide", ToneControl(180, 1800, 0.18));
            yield return ("research-pitch-vibrato-no-tremolo", ToneControl(437, 437, 0.18, 5.2, 5.3));
            yield return ("research-stationary-noise", NoiseControl(0.06, 41001));
            yield return ("research-pad-light-noise", PadNoiseControl(1, 0.0007, 41002));
            yield return ("research-tonal-groups-120ms", TonalGroups());
            yield break;
        }

        // Factory drums retain the factory's seed (20261008), deliberately testing new
        // tempos/gains, not pretending to be independent noise realizations. Padding moves
        // every factory onset away from the ignored startup/end regions; no pad is truncated.
        yield return ("holdout-drums100-gain080", FactoryDrums(100, 0.80));
        yield return ("holdout-drums190-gain060", FactoryDrums(190, 0.60));
        yield return ("holdout-drums320-gain095", FactoryDrums(320, 0.95));
        yield return ("holdout-dense-hats-75ms-seed51001", DenseHats(0.075, 51001));

        // Identical envelope/timbre across these three gains isolates attenuation. The
        // shared holdout seed is intentional and is disjoint from all research seeds.
        yield return ("holdout-known-attacks-gain001", KnownAttacks(0.01, 51004));
        yield return ("holdout-known-attacks-gain010", KnownAttacks(0.10, 51004));
        yield return ("holdout-known-attacks-gain065", KnownAttacks(0.65, 51004));
        yield return ("holdout-steady-sine-713hz", ToneControl(713, 713, 0.11));
        yield return ("holdout-constant-amplitude-descending-glide", ToneControl(2200, 240, 0.14));
        yield return ("holdout-pitch-vibrato-no-tremolo", ToneControl(713, 713, 0.15, 6.4, 6.7));
        yield return ("holdout-stationary-noise", NoiseControl(0.04, 51002));
        yield return ("holdout-pad-light-noise", PadNoiseControl(0.70, 0.0011, 51003));
        yield return ("holdout-weak-after-tail-gap85ms", TailPairs(0.085, 0.85, 0.075, 0.19, 51005));
        yield return ("holdout-weak-after-tail-gap155ms", TailPairs(0.155, 0.65, 0.12, 0.27, 51006));
    }

    private static SyntheticTrack ToneControl(double startHz, double endHz, double gain,
        double vibratoDepthHz = 0, double vibratoRateHz = 0)
    {
        var samples = new float[Frames * Channels];
        var slope = (endHz - startHz) / Seconds;
        for (var frame = 0; frame < Frames; frame++)
        {
            var t = (double)frame / Rate;
            // Integrate instantaneous frequency: carrier amplitude is constant. Vibrato
            // changes only phase/frequency, never gain (it is explicitly not tremolo).
            var phase = 2 * Math.PI * (startHz * t + 0.5 * slope * t * t);
            if (vibratoDepthHz != 0)
                phase += vibratoDepthHz / vibratoRateHz * (1 - Math.Cos(2 * Math.PI * vibratoRateHz * t));
            Add(samples, frame, (float)(gain * Math.Sin(phase)));
        }
        return FinishControl(samples);
    }

    private static SyntheticTrack NoiseControl(double gain, int seed)
    {
        var samples = new float[Frames * Channels];
        AddStationaryNoise(samples, gain, seed);
        return FinishControl(samples);
    }

    private static SyntheticTrack PadNoiseControl(double padGain, double noiseGain, int seed)
    {
        var pad = SyntheticClickTrack.CreatePadOnly(Seconds, Rate, Channels);
        for (var i = 0; i < pad.Samples.Length; i++)
            pad.Samples[i] *= (float)padGain;
        AddStationaryNoise(pad.Samples, noiseGain, seed);
        return FinishControl(pad.Samples);
    }

    private static void AddStationaryNoise(float[] samples, double gain, int seed)
    {
        var random = new Random(seed);
        for (var frame = 0; frame < Frames; frame++)
            Add(samples, frame, (float)(gain * (2 * random.NextDouble() - 1)));
    }

    private static SyntheticTrack FinishControl(float[] samples)
    {
        // Only the ignored boundary regions change amplitude. The interior noise has a
        // time-invariant distribution; individual stochastic excursions are not labels.
        const int taperFrames = Rate / 10;
        for (var frame = 0; frame < taperFrames; frame++)
        {
            var gain = (float)(0.5 - 0.5 * Math.Cos(Math.PI * frame / taperFrames));
            for (var channel = 0; channel < Channels; channel++)
            {
                samples[frame * Channels + channel] *= gain;
                samples[(Frames - 1 - frame) * Channels + channel] *= gain;
            }
        }
        return Track(samples, Array.Empty<TimeSpan>());
    }

    private static SyntheticTrack FactoryDrums(double bpm, double gain)
    {
        var source = SyntheticClickTrack.CreateDrumPattern(bpm, Seconds - 2, Rate, Channels, includePad: false);
        var samples = new float[Frames * Channels];
        for (var i = 0; i < source.Samples.Length; i++)
            samples[Rate * Channels + i] = (float)(gain * source.Samples[i]);
        // Preserve the factory's onset metadata, translated with the PCM. Factory
        // scheduling is continuous-time; its integer PCM starts differ by < one sample.
        var hits = source.HitTimes.Select(time => time + TimeSpan.FromSeconds(1)).ToArray();
        return Track(samples, hits);
    }

    private static SyntheticTrack DenseHats(double interval, int seed)
    {
        var samples = new float[Frames * Channels];
        var hits = new List<TimeSpan>();
        var random = new Random(seed);
        for (var index = 0; ; index++)
        {
            var offset = (int)Math.Round((1 + index * interval) * Rate);
            if (offset >= (Seconds - 1) * Rate)
                break;
            // Exact integer-sample starts avoid drift in the dense attack schedule.
            var start = offset / (double)Rate;
            AddHat(samples, Rate, Channels, start, random);
            // AddHat truncates its double start internally; reflect that actual offset.
            hits.Add(TimeSpan.FromSeconds((int)(start * Rate) / (double)Rate));
        }
        return Track(samples, hits);
    }

    private static SyntheticTrack TonalGroups()
    {
        var samples = new float[Frames * Channels];
        var hits = new List<TimeSpan>();
        double[] pitches = [260, 420, 680, 920, 340];
        double[] gains = [0.07, 0.11, 0.05, 0.09, 0.04];
        for (var group = 0; group < 4; group++)
        {
            for (var note = 0; note < pitches.Length; note++)
            {
                var offset = (int)Math.Round((1 + group * 1.4 + note * 0.120) * Rate);
                AddAttack(samples, offset, gains[note], 0.025, pitches[(note + group) % pitches.Length], null);
                hits.Add(TimeSpan.FromSeconds(offset / (double)Rate));
            }
        }
        return Track(samples, hits);
    }

    private static SyntheticTrack KnownAttacks(double gain, int seed)
    {
        var samples = new float[Frames * Channels];
        var hits = new List<TimeSpan>();
        var random = new Random(seed);
        for (var note = 0; note < 7; note++)
        {
            var offset = (int)Math.Round((1 + note * 0.83) * Rate);
            AddAttack(samples, offset, gain, 0.022, 0, random);
            hits.Add(TimeSpan.FromSeconds(offset / (double)Rate));
        }
        return Track(samples, hits);
    }

    private static SyntheticTrack TailPairs(double gap, double strongGain, double weakGain,
        double strongDecay, int seed)
    {
        var samples = new float[Frames * Channels];
        var hits = new List<TimeSpan>();
        var random = new Random(seed);
        for (var pair = 0; pair < 3; pair++)
        {
            var strongOffset = (1 + pair * 2) * Rate;
            var weakOffset = strongOffset + (int)Math.Round(gap * Rate);
            AddAttack(samples, strongOffset, strongGain, strongDecay, 0, random);
            AddAttack(samples, weakOffset, weakGain, 0.017, 0, random);
            hits.Add(TimeSpan.FromSeconds(strongOffset / (double)Rate));
            hits.Add(TimeSpan.FromSeconds(weakOffset / (double)Rate));
        }
        return Track(samples, hits);
    }

    private static void AddAttack(float[] samples, int offset, double gain, double decay,
        double frequency, Random? random)
    {
        var length = (int)Math.Ceiling(8 * decay * Rate);
        for (var i = 0; i < length && offset + i < Frames; i++)
        {
            var t = (double)i / Rate;
            // An explicit 2ms amplitude attack, exponential tail and quiet 5ms release.
            // HitTimes mark envelope starts; random values affect timbre, not truth.
            var attack = Math.Min(1, t / 0.002);
            var release = Math.Min(1, (length - 1 - i) / (Rate * 0.005));
            var carrier = random is null ? Math.Sin(2 * Math.PI * frequency * t) : 2 * random.NextDouble() - 1;
            Add(samples, offset + i, (float)(gain * attack * Math.Exp(-t / decay) * release * carrier));
        }
    }

    private static void Add(float[] samples, int frame, float value)
    {
        for (var channel = 0; channel < Channels; channel++)
            samples[frame * Channels + channel] += value;
    }

    private static SyntheticTrack Track(float[] samples, IReadOnlyList<TimeSpan> hits) =>
        new(samples, Rate, Channels, hits, TimeSpan.FromSeconds(samples.Length / (double)(Rate * Channels)));
}
