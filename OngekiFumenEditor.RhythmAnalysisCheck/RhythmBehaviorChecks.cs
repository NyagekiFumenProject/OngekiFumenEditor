using OngekiFumenEditor.Kernel.Audio.Rhythm;

namespace OngekiFumenEditor.RhythmAnalysisCheck;

/// <summary>Permanent onset-visibility regressions using PCM ground truth, not chart notes.</summary>
internal static class RhythmBehaviorChecks
{
    private const int SampleRate = 48000;
    private const int ControlSeconds = 8;
    private const double BoundarySeconds = 0.5;
    private const double MatchToleranceSeconds = 0.035;
    private const double ValleyWindowSeconds = 0.060;

    internal static void Run(IRhythmAnalyzer analyzer, Action<bool, string, string> check)
    {
        CheckAttacks(analyzer, SyntheticClickTrack.CreateDrumPattern(250, 8), 0.95,
            "behavior: 250 BPM drum+pad attacks have separate visible peaks", check);
        CheckAttacks(analyzer, CreateTailPair(), 1.0,
            "behavior: weak same-timbre attack remains distinct after a strong tail", check);

        CheckNoPhantoms(analyzer, CreateTone(0.11),
            "behavior: constant 713 Hz tone has no visible interior phantom peaks", check);
        CheckNoPhantoms(analyzer, CreateTone(0.15, 6.4, 6.7),
            "behavior: mild pitch-only vibrato has no visible interior phantom peaks", check);
        CheckNoPhantoms(analyzer, CreatePadWithTinyNoise(),
            "behavior: sustained pad with tiny noise has no visible interior phantom peaks", check);
    }

    private static void CheckAttacks(IRhythmAnalyzer analyzer, SyntheticTrack track,
        double requiredFraction, string name, Action<bool, string, string> check)
    {
        var envelope = Analyze(analyzer, track, name, check);
        if (envelope is null)
            return;

        var hits = track.HitTimes.Select(time => time.TotalSeconds)
            .Where(time => IsInterior(time, track.Duration.TotalSeconds)).OrderBy(time => time).ToArray();
        var peaks = VisiblePeaks(envelope, track.Duration.TotalSeconds);
        var used = new bool[peaks.Count];
        var missed = new List<double>();
        var matched = 0;
        var worstError = 0.0;
        foreach (var hit in hits)
        {
            var nearest = -1;
            var distance = double.PositiveInfinity;
            for (var i = 0; i < peaks.Count; i++)
            {
                var error = Math.Abs(peaks[i] / envelope.FrameRateHz - hit);
                if (!used[i] && error <= MatchToleranceSeconds && error < distance)
                {
                    nearest = i;
                    distance = error;
                }
            }

            if (nearest < 0)
                missed.Add(hit);
            else
            {
                // Both fixtures have 120ms attack spacing: their +/-35ms match windows
                // do not overlap. Consume each peak once; a broad hump cannot prove two hits.
                used[nearest] = true;
                matched++;
                worstError = Math.Max(worstError, distance);
            }
        }

        check(hits.Length > 0 && matched >= Math.Ceiling(requiredFraction * hits.Length), name,
            $"matched={matched}/{hits.Length}, required={requiredFraction:P0}, " +
            $"visible interior peaks={peaks.Count}, worst matched error={worstError * 1000:F1}ms; " +
            "missed seconds=" + (missed.Count == 0 ? "none" : string.Join(", ", missed.Select(time => $"{time:F3}"))));
    }

    private static void CheckNoPhantoms(IRhythmAnalyzer analyzer, SyntheticTrack track,
        string name, Action<bool, string, string> check)
    {
        var envelope = Analyze(analyzer, track, name, check);
        if (envelope is null)
            return;

        var peaks = VisiblePeaks(envelope, track.Duration.TotalSeconds);
        check(peaks.Count == 0, name,
            $"visible peaks={peaks.Count} between {BoundarySeconds:F1}s and " +
            $"{track.Duration.TotalSeconds - BoundarySeconds:F1}s; " +
            "first peak seconds=" + (peaks.Count == 0 ? "none" :
                string.Join(", ", peaks.Take(8).Select(frame => $"{frame / envelope.FrameRateHz:F3}"))));
    }

    private static RhythmEnvelope? Analyze(IRhythmAnalyzer analyzer, SyntheticTrack track,
        string name, Action<bool, string, string> check)
    {
        var envelope = analyzer.Analyze(track.ToSampleData());
        if (envelope is null)
        {
            check(false, name, "Analyze returned null for supported deterministic float PCM.");
            return null;
        }
        if (!double.IsFinite(envelope.FrameRateHz) || envelope.FrameRateHz <= 0 ||
            envelope.FrameCount < 3 || envelope.Total.Any(value => !float.IsFinite(value)))
        {
            check(false, name, $"Unusable envelope: frames={envelope.FrameCount}, rate={envelope.FrameRateHz}Hz, " +
                "expected finite samples and a positive frame rate.");
            return null;
        }
        return envelope;
    }

    private static bool IsInterior(double seconds, double duration) =>
        seconds >= BoundarySeconds && seconds <= duration - BoundarySeconds;

    private static List<int> VisiblePeaks(RhythmEnvelope envelope, double duration)
    {
        var curve = envelope.Total;
        var radius = (int)Math.Floor(ValleyWindowSeconds * envelope.FrameRateHz);
        var peaks = new List<int>();
        for (var i = 1; i < curve.Length - 1; i++)
        {
            // A flat-topped local maximum is represented only by its right edge.
            if (!IsInterior(i / envelope.FrameRateHz, duration) || curve[i] < 0.05f ||
                curve[i] < curve[i - 1] || curve[i] <= curve[i + 1])
                continue;

            var leftValley = curve[i];
            var rightValley = curve[i];
            for (var j = Math.Max(0, i - radius); j < i; j++)
                leftValley = Math.Min(leftValley, curve[j]);
            for (var j = i + 1; j <= Math.Min(curve.Length - 1, i + radius); j++)
                rightValley = Math.Min(rightValley, curve[j]);

            // Relative prominence = (height - higher of the two +/-60ms valleys) / height.
            var prominence = curve[i] - Math.Max(leftValley, rightValley);
            if (prominence / curve[i] >= 0.10)
                peaks.Add(i);
        }
        return peaks;
    }

    private static SyntheticTrack CreateTailPair()
    {
        const int seconds = 6;
        var samples = new float[SampleRate * seconds];
        Burst(1.0, 1.0, 0.10);
        Burst(1.12, 0.35, 0.015);
        return new(samples, SampleRate, 1,
            new[] { TimeSpan.FromSeconds(1.0), TimeSpan.FromSeconds(1.12) }, TimeSpan.FromSeconds(seconds));

        void Burst(double start, double amplitude, double decay)
        {
            // Restart the seed for identical wide-band timbre. Ground truth is the two
            // 1ms amplitude ramps, not the random fluctuations or the decaying tail.
            var random = new Random(20261009);
            var offset = (int)Math.Round(start * SampleRate);
            var length = (int)(8 * decay * SampleRate);
            for (var i = 0; i < length && offset + i < samples.Length; i++)
            {
                var t = i / (double)SampleRate;
                var attack = Math.Min(1, t / 0.001);
                var noise = 2 * random.NextDouble() - 1;
                samples[offset + i] += (float)(amplitude * attack * Math.Exp(-t / decay) * noise);
            }
        }
    }

    private static SyntheticTrack CreateTone(double gain, double depthHz = 0, double rateHz = 0)
    {
        var samples = new float[ControlSeconds * SampleRate * 2];
        for (var frame = 0; frame < samples.Length / 2; frame++)
        {
            var t = frame / (double)SampleRate;
            var phase = 2 * Math.PI * 713 * t;
            // Integrate frequency modulation analytically: gain never changes in the
            // scored interior, so pitch vibrato is not an amplitude attack or tremolo.
            if (depthHz != 0)
                phase += depthHz / rateHz * (1 - Math.Cos(2 * Math.PI * rateHz * t));
            samples[frame * 2] = samples[frame * 2 + 1] = (float)(gain * Math.Sin(phase));
        }
        return FinishControl(samples);
    }

    private static SyntheticTrack CreatePadWithTinyNoise()
    {
        var pad = SyntheticClickTrack.CreatePadOnly(ControlSeconds, SampleRate, 2);
        var random = new Random(51003);
        for (var frame = 0; frame < pad.Samples.Length / 2; frame++)
        {
            var noise = (float)(0.0011 * (2 * random.NextDouble() - 1));
            for (var channel = 0; channel < 2; channel++)
            {
                var index = frame * 2 + channel;
                pad.Samples[index] = pad.Samples[index] * 0.7f + noise;
            }
        }
        // This specific low-noise control is stationary; it does not claim that arbitrary
        // noise levels or all exponential tails are free of peaks in a sensitive analyzer.
        return FinishControl(pad.Samples);
    }

    private static SyntheticTrack FinishControl(float[] samples)
    {
        // Start/stop are real amplitude transitions, not phantom attacks. A 100ms taper
        // lies wholly inside the excluded first/last 0.5s and leaves the interior unchanged.
        const int taperFrames = SampleRate / 10;
        var frames = samples.Length / 2;
        for (var frame = 0; frame < taperFrames; frame++)
        {
            var gain = (float)(0.5 - 0.5 * Math.Cos(Math.PI * frame / taperFrames));
            for (var channel = 0; channel < 2; channel++)
            {
                samples[frame * 2 + channel] *= gain;
                samples[(frames - 1 - frame) * 2 + channel] *= gain;
            }
        }
        return new(samples, SampleRate, 2, Array.Empty<TimeSpan>(), TimeSpan.FromSeconds(ControlSeconds));
    }
}
