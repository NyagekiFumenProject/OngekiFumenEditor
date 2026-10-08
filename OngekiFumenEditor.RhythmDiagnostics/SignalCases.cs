using System.Reflection;
using OngekiFumenEditor.RhythmAnalysisCheck;

namespace OngekiFumenEditor.RhythmDiagnostics;

internal static class SignalCases
{
    internal static IEnumerable<(string Name, SyntheticTrack Track)> All()
    {
        yield return ("drums150", SyntheticClickTrack.CreateDrumPattern(150, 12));
        yield return ("drums250", SyntheticClickTrack.CreateDrumPattern(250, 12));
        yield return ("hats250-eighth", Hats(0.12));
        yield return ("hats250-sixteenth", Hats(0.06));
        yield return ("pair120-equal-short", Pair(0.12, 1, 0.015));
        yield return ("pair120-weak-longtail", Pair(0.12, 0.35, 0.10));
        yield return ("pair60-weak-longtail", Pair(0.06, 0.35, 0.10));
        yield return ("pair200-weak-longtail", Pair(0.20, 0.35, 0.10));
        yield return ("isolated-weak", Pair(0.12, 0.35, 0.10, firstAmplitude: 0));
        yield return ("longtail-only", Pair(0.12, 0, 0.10));
        yield return ("pad-only", SyntheticClickTrack.CreatePadOnly(6));
        yield return ("silence", SyntheticClickTrack.CreateSilence(6));
    }

    private static SyntheticTrack Hats(double interval)
    {
        const int rate = 48000;
        const int channels = 2;
        const int seconds = 8;
        var samples = new float[seconds * rate * channels];
        var hits = new List<TimeSpan>();
        var addHat = typeof(SyntheticClickTrack).GetMethod("AddHat", BindingFlags.NonPublic | BindingFlags.Static)!
            .CreateDelegate<Action<float[], int, int, double, Random>>();
        var random = new Random(20261009);
        for (var t = 1.0; t < seconds - 1; t += interval)
        {
            addHat(samples, rate, channels, t, random);
            hits.Add(TimeSpan.FromSeconds(t));
        }
        return new(samples, rate, channels, hits, TimeSpan.FromSeconds(seconds));
    }

    private static SyntheticTrack Pair(double interval, double secondAmplitude,
        double firstDecay, double firstAmplitude = 1)
    {
        const int rate = 48000;
        const int seconds = 6;
        var samples = new float[seconds * rate];
        var hits = new List<TimeSpan>();
        if (firstAmplitude > 0)
        {
            Burst(1.0, firstAmplitude, firstDecay);
            hits.Add(TimeSpan.FromSeconds(1));
        }
        if (secondAmplitude > 0)
        {
            Burst(1.0 + interval, secondAmplitude, 0.015);
            hits.Add(TimeSpan.FromSeconds(1.0 + interval));
        }
        return new(samples, rate, 1, hits, TimeSpan.FromSeconds(seconds));

        void Burst(double start, double amplitude, double decay)
        {
            // Same reproducible wide-band timbre for both strikes, independent of amplitude.
            var random = new Random(20261009);
            var offset = (int)Math.Round(start * rate);
            var length = (int)(decay * 8 * rate);
            for (var i = 0; i < length && offset + i < samples.Length; i++)
            {
                var t = (double)i / rate;
                var attack = Math.Min(1, t / 0.001);
                var noise = random.NextDouble() * 2 - 1;
                samples[offset + i] += (float)(amplitude * attack * Math.Exp(-t / decay) * noise);
            }
        }
    }
}

internal sealed record Peak(int Frame, float Height, float Prominence)
{
    internal double Time => Frame / (double)Spectrum.FrameRate;
    internal double RelativeProminence => Height > 0 ? Prominence / Height : 0;
}

internal static class PeakMeasure
{
    // Real-chart note positions are NOT ground truth for accents. These thresholds describe
    // curve visibility only: >= 0.05 height, >= 10% prominence, within +/-40ms of a known hit.
    internal static List<Peak> Find(float[] curve)
    {
        var result = new List<Peak>();
        for (var i = 1; i < curve.Length - 1; i++)
        {
            if (curve[i] < curve[i - 1] || curve[i] <= curve[i + 1])
                continue;
            var left = curve[i];
            var right = curve[i];
            for (var j = Math.Max(0, i - 12); j < i; j++)
                left = Math.Min(left, curve[j]);
            for (var j = i + 1; j <= Math.Min(curve.Length - 1, i + 12); j++)
                right = Math.Min(right, curve[j]);
            result.Add(new(i, curve[i], curve[i] - Math.Max(left, right)));
        }
        return result;
    }

    internal static bool Visible(Peak peak) => peak.Height >= 0.05f && peak.RelativeProminence >= 0.1;

    internal static (int Matched, int Extra, double MeanErrorMs, double P95ErrorMs) Match(
        List<Peak> peaks, double[] hits, double duration)
    {
        var available = peaks.Where(p => p.Time >= 0.5 && p.Time <= duration - 0.5 && Visible(p)).ToList();
        var matched = 0;
        var errors = new List<double>();
        foreach (var hit in hits)
        {
            var nearest = available.Where(p => Math.Abs(p.Time - hit) <= 0.040001)
                .MinBy(p => Math.Abs(p.Time - hit));
            if (nearest is not null)
            {
                matched++;
                errors.Add(Math.Abs(nearest.Time - hit) * 1000);
                available.Remove(nearest); // One peak cannot prove two distinct strikes.
            }
        }
        errors.Sort();
        var mean = errors.Count > 0 ? errors.Average() : double.NaN;
        var p95 = errors.Count > 0 ? errors[(int)Math.Ceiling(errors.Count * 0.95) - 1] : double.NaN;
        return (matched, available.Count, mean, p95);
    }
}
