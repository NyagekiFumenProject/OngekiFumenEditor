namespace OngekiFumenEditor.RhythmDiagnostics;

internal enum CandidateKind
{
    FrequencyMax,
    BandEnergy,
    BandEnergySignedNoise
}

internal sealed record CandidateSettings(CandidateKind Kind = CandidateKind.FrequencyMax,
    int LagFrames = 3, int FrequencyRadius = 1, float MinimumRise = 0,
    bool AdaptiveFloor = false, int NoiseWindowFrames = 51, float NoiseMadFactor = 3,
    float EnergyGateMinimum = 0, float EnergyGateFull = 0);

// Experimental diagnostic alternatives, not production onset detection.
// MinimumRise and the adaptive floor are in raw log-novelty units, before any
// smoothing, normalization or mixing of the three bands.
internal static class ImprovementAnalysis
{
    internal static float[][] Compute(Spectrum spectrum, CandidateSettings settings)
    {
        var flux = Spectrum.CreateBands(spectrum.FrameCount);
        if (spectrum.FrameCount < 2)
            return flux;

        var lagFrames = Math.Max(1, settings.LagFrames);
        switch (settings.Kind)
        {
            case CandidateKind.FrequencyMax:
                ComputeFrequencyMax(spectrum, settings, lagFrames, flux);
                break;
            case CandidateKind.BandEnergy:
                ComputeBandEnergy(spectrum, settings.MinimumRise, lagFrames, flux);
                break;
            case CandidateKind.BandEnergySignedNoise:
                ComputeSignedEnergyNoise(spectrum, settings, lagFrames, flux);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(settings), settings.Kind,
                    "Unknown experimental novelty kind.");
        }

        if (settings.EnergyGateFull > 0)
            ApplyEnergyGate(spectrum, settings, lagFrames, flux);

        if (settings.AdaptiveFloor)
            ApplyAdaptiveFloor(flux, settings);
        return flux;
    }

    private static void ApplyEnergyGate(Spectrum spectrum, CandidateSettings settings,
        int lagFrames, float[][] flux)
    {
        // Random movement of individual FFT bins is not a coherent amplitude attack.
        // Accept growth in either the whole spectrum (kicks) or mid+high energy
        // (hats over a sustained bass). Thresholds are natural-log amplitude ratios,
        // hence independent of recording gain; no chart/tempo/time information is used.
        for (var frame = 1; frame < spectrum.FrameCount; frame++)
        {
            var previous = Math.Max(0, frame - lagFrames);
            double totalNow = 0, totalBefore = 0, upperNow = 0, upperBefore = 0;
            for (var band = 0; band < spectrum.BandEnergy.Length; band++)
            {
                var now = (double)spectrum.BandEnergy[band][frame];
                var before = (double)spectrum.BandEnergy[band][previous];
                totalNow += now * now;
                totalBefore += before * before;
                if (band > 0)
                {
                    upperNow += now * now;
                    upperBefore += before * before;
                }
            }
            var growth = Math.Max(LogAmplitudeRise(totalNow, totalBefore),
                LogAmplitudeRise(upperNow, upperBefore));
            var confidence = Math.Clamp((growth - settings.EnergyGateMinimum)
                / (settings.EnergyGateFull - settings.EnergyGateMinimum), 0, 1);
            for (var band = 0; band < flux.Length; band++)
                flux[band][frame] *= (float)confidence;
        }
    }

    private static double LogAmplitudeRise(double powerNow, double powerBefore) =>
        powerBefore > 0 ? 0.5 * Math.Log(powerNow / powerBefore)
        : powerNow > 0 ? double.PositiveInfinity : 0;

    private static void ComputeFrequencyMax(Spectrum spectrum, CandidateSettings settings,
        int lagFrames, float[][] flux)
    {
        var radius = Math.Clamp(settings.FrequencyRadius, 0, Spectrum.BinCount - 2);
        var reference = new float[Spectrum.BinCount];
        var deque = new int[Spectrum.BinCount - 1];
        var rise = new double[Spectrum.BinCount];
        var cachedReferenceFrame = -1;
        for (var frame = 1; frame < spectrum.FrameCount; frame++)
        {
            // One lagged frame, spatially max-filtered; early frames use frame 0.
            var referenceFrame = Math.Max(0, frame - lagFrames);
            if (referenceFrame != cachedReferenceFrame)
            {
                BuildFrequencyReference(spectrum.LogLevels, referenceFrame * Spectrum.BinCount,
                    radius, reference, deque);
                cachedReferenceFrame = referenceFrame;
            }

            var offset = frame * Spectrum.BinCount;
            for (var bin = 1; bin < Spectrum.BinCount; bin++)
            {
                rise[bin] = Math.Max(0d, (double)spectrum.LogLevels[offset + bin]
                    - reference[bin] - settings.MinimumRise);
            }

            for (var band = 0; band < flux.Length; band++)
            {
                var bins = spectrum.Bins[band];
                if (bins.Length == 0)
                    continue;
                double sum = 0;
                foreach (var bin in bins)
                    sum += rise[bin];
                flux[band][frame] = PositiveFloat(sum / bins.Length);
            }
        }
    }

    private static void BuildFrequencyReference(float[] levels, int offset, int radius,
        float[] reference, int[] deque)
    {
        if (radius == 0)
        {
            Array.Copy(levels, offset + 1, reference, 1, Spectrum.BinCount - 1);
            return;
        }

        // Monotone deque computes all neighboring-bin maxima in linear time.
        // Its domain is every non-DC bin, so references may cross band borders.
        var head = 0;
        var tail = 0;
        var nextBin = 1;
        for (var bin = 1; bin < Spectrum.BinCount; bin++)
        {
            var left = Math.Max(1, bin - radius);
            var right = Math.Min(Spectrum.BinCount - 1, bin + radius);
            while (head < tail && deque[head] < left)
                head++;
            while (nextBin <= right)
            {
                var level = levels[offset + nextBin];
                while (tail > head && levels[offset + deque[tail - 1]] <= level)
                    tail--;
                deque[tail++] = nextBin++;
            }
            reference[bin] = levels[offset + deque[head]];
        }
    }

    private static void ComputeBandEnergy(Spectrum spectrum, float minimumRise,
        int lagFrames, float[][] flux)
    {
        // Log each energy only once. A reusable lag ring avoids a full extra
        // level curve; when the lag exceeds the song, only frame 0 is referenced.
        var historyLength = lagFrames < spectrum.FrameCount ? lagFrames : 1;
        var history = new double[historyLength];
        for (var band = 0; band < flux.Length; band++)
        {
            var energy = spectrum.BandEnergy[band];
            var firstLevel = EnergyLevel(energy[0]);
            history[0] = firstLevel;
            var cursor = historyLength == 1 ? 0 : 1;
            for (var frame = 1; frame < spectrum.FrameCount; frame++)
            {
                var reference = frame < lagFrames ? firstLevel : history[cursor];
                var level = EnergyLevel(energy[frame]);
                flux[band][frame] = PositiveFloat(level - reference - minimumRise);
                history[cursor] = level;
                cursor = cursor + 1 == historyLength ? 0 : cursor + 1;
            }
        }
    }

    private static void ComputeSignedEnergyNoise(Spectrum spectrum, CandidateSettings settings,
        int lagFrames, float[][] flux)
    {
        // Estimate fluctuation BEFORE half-wave rectification: a positive-only series
        // often has median=MAD=0, which makes the previous adaptive floor ineffective.
        var length = spectrum.FrameCount;
        var window = Math.Min(Math.Max(1, settings.NoiseWindowFrames), length);
        var leftRadius = (window - 1) / 2;
        var rightRadius = window / 2;
        var levels = new double[length];
        var signed = new double[length];
        var scratch = new double[window];
        var madScale = settings.NoiseMadFactor * 1.4826;
        for (var band = 0; band < flux.Length; band++)
        {
            for (var frame = 0; frame < length; frame++)
                levels[frame] = EnergyLevel(spectrum.BandEnergy[band][frame]);
            signed[0] = 0;
            for (var frame = 1; frame < length; frame++)
                signed[frame] = levels[frame] - levels[Math.Max(0, frame - lagFrames)];
            for (var frame = 1; frame < length; frame++)
            {
                var first = Math.Max(0, frame - leftRadius);
                var last = Math.Min(length - 1, frame + rightRadius);
                var count = last - first + 1;
                Array.Copy(signed, first, scratch, 0, count);
                Array.Sort(scratch, 0, count);
                var middle = count / 2;
                var median = count % 2 == 0
                    ? (scratch[middle - 1] + scratch[middle]) / 2 : scratch[middle];
                for (var i = 0; i < count; i++)
                    scratch[i] = Math.Abs(scratch[i] - median);
                Array.Sort(scratch, 0, count);
                var mad = count % 2 == 0
                    ? (scratch[middle - 1] + scratch[middle]) / 2 : scratch[middle];
                var threshold = Math.Max(0, median) + madScale * mad + settings.MinimumRise;
                flux[band][frame] = PositiveFloat(signed[frame] - threshold);
            }
        }
    }

    private static double EnergyLevel(float energy) =>
        // Energy is non-negative by construction; clamp a negative diagnostic
        // input to its physical domain. Double arithmetic avoids float overflow.
        Math.Log(1d + 300d * Math.Max(0d, energy));

    private static void ApplyAdaptiveFloor(float[][] flux, CandidateSettings settings)
    {
        var length = flux[0].Length;
        var window = Math.Max(1, settings.NoiseWindowFrames);
        var leftRadius = Math.Min((window - 1) / 2, length - 1);
        var rightRadius = Math.Min(window / 2, length - 1);
        var original = new float[length];
        var scratch = new float[Math.Min(window, length)];
        var madScale = (double)settings.NoiseMadFactor * 1.4826d;
        foreach (var band in flux)
        {
            // Never feed already corrected values into subsequent centered
            // windows. Reuse both buffers across all frames and bands.
            Array.Copy(band, original, length);
            for (var frame = 1; frame < length; frame++)
            {
                // Truncate at song edges, without padding or changing window
                // width for short songs. Even windows have one extra future frame.
                var first = Math.Max(0, frame - leftRadius);
                var last = frame + Math.Min(rightRadius, length - 1 - frame);
                var count = last - first + 1;
                Array.Copy(original, first, scratch, 0, count);
                Array.Sort(scratch, 0, count);
                var median = Median(scratch, count);
                for (var i = 0; i < count; i++)
                    scratch[i] = (float)Math.Abs(scratch[i] - median);
                Array.Sort(scratch, 0, count);
                // Experimental centered median + scaled MAD, measured in the
                // same raw novelty units as this band, not normalized intensity.
                var floor = median + madScale * Median(scratch, count);
                band[frame] = PositiveFloat(original[frame] - floor);
            }
            // The opening frame initializes the reference, never an attack.
            band[0] = 0;
        }
    }

    private static double Median(float[] sorted, int count)
    {
        var middle = count / 2;
        return count % 2 == 0
            ? ((double)sorted[middle - 1] + sorted[middle]) * 0.5d
            : sorted[middle];
    }

    // Saturation matters only for extreme finite diagnostic inputs: double
    // intermediates keep differences, sums and MAD thresholds from overflowing.
    private static float PositiveFloat(double value) =>
        value > 0 ? (float)Math.Min(value, float.MaxValue) : 0;
}
