using System.Globalization;
using System.Diagnostics;
using System.Text;
using NAudio.Wave;
using OngekiFumenEditor.Kernel.Audio;

namespace OngekiFumenEditor.RhythmDiagnostics;

internal static class Program
{
    private static int Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        try
        {
            string? audio = null;
            var holdout = false;
            var audioOnly = false;
            string[]? selectedNames = null;
            var targets = new[] { 92.88, 93.00 };
            var directory = Path.Combine(Path.GetTempPath(), "OngekiRhythmDiagnostics", DateTime.Now.ToString("yyyyMMdd-HHmmss"));
            for (var i = 0; i < args.Length; i++)
            {
                if (args[i] == "--help")
                {
                    Console.WriteLine("dotnet run --project OngekiFumenEditor.RhythmDiagnostics -c Release -- [--audio <wav>] [--at <seconds,seconds>] [--out <directory>] [--holdout] [--audio-only] [--variants <name,name>]");
                    Console.WriteLine("Default: research/control cases. --holdout: independent holdout cases. Audio mode exports all traces and stages.svg.");
                    return 0;
                }
                if (args[i] == "--holdout")
                {
                    holdout = true;
                    continue;
                }
                if (args[i] == "--audio-only")
                {
                    audioOnly = true;
                    continue;
                }
                if (i + 1 >= args.Length)
                    throw new ArgumentException("Missing option value: " + args[i]);
                var option = args[i++];
                switch (option)
                {
                    case "--audio": audio = args[i]; break;
                    case "--at": targets = args[i].Split(',').Select(double.Parse).Order().ToArray(); break;
                    case "--out": directory = Path.GetFullPath(args[i]); break;
                    case "--variants": selectedNames = args[i].Split(','); break;
                    default: throw new ArgumentException("Unknown option: " + option);
                }
            }
            if (targets.Length == 0 || targets.Any(t => !double.IsFinite(t) || t < 0))
                throw new ArgumentException("Target times must be finite and non-negative.");
            if (audioOnly && audio is null)
                throw new ArgumentException("--audio-only requires --audio.");
            var variants = Variant.All().ToArray();
            if (selectedNames is not null)
            {
                var requested = selectedNames.ToHashSet(StringComparer.Ordinal);
                var unknown = requested.Except(variants.Select(v => v.Name)).ToArray();
                if (unknown.Length > 0)
                    throw new ArgumentException("Unknown variant(s): " + string.Join(',', unknown));
                variants = variants.Where(v => v == Variant.Baseline || requested.Contains(v.Name)).ToArray();
            }
            Directory.CreateDirectory(directory);
            Console.WriteLine("Visibility criterion: normalized height >= 0.05, prominence >= 10% over +/-60ms valleys, within +/-40ms of a KNOWN hit.");
            Console.WriteLine("Real chart notes are not ground truth for musical accents; raw peaks are reported without a visibility filter.");
            Console.WriteLine("Production stages are linked; the editor is never rebuilt or launched. All variants are diagnostic-only.");
            Console.WriteLine("VARIANTS: " + string.Join(',', variants.Select(v => v.Name)));
            if (audio is not null)
                RunAudio(audio, targets, directory, variants);
            if (!audioOnly)
                RunSynthetic(directory, holdout, variants);
            Console.WriteLine("OUTPUT: " + directory);
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error);
            return 1;
        }
    }

    private static void RunAudio(string path, double[] targets, string directory, IReadOnlyList<Variant> variants)
    {
        using var reader = new AudioFileReader(path);
        var rate = reader.WaveFormat.SampleRate;
        var channels = reader.WaveFormat.Channels;
        var all = new List<float>();
        var buffer = new float[rate * channels];
        int count;
        while ((count = reader.Read(buffer, 0, buffer.Length)) > 0)
            all.AddRange(buffer.AsSpan(0, count));
        var samples = all.ToArray();
        var duration = samples.Length / (double)channels / rate;
        if (targets.Any(t => t >= duration))
            throw new ArgumentException("Target time is beyond the audio duration.");
        Console.WriteLine($"AUDIO {Path.GetFileName(path)}: {duration:F3}s, {rate} Hz, {channels} ch");
        var spectrum = Spectrum.Capture(samples, rate, channels);
        var results = variants.Select(v => StageResult.Analyze(spectrum, v)).ToArray();
        var baseline = results[0];
        var difference = StageResult.VerifyBaseline(ToData(samples, rate, channels), baseline);
        Console.WriteLine($"BASELINE identity with production over {spectrum.FrameCount} frames: max error={difference:G9}");
        var from = Math.Max(0, targets.Min() - 0.20);
        var to = Math.Min(duration, targets.Max() + 0.25);
        foreach (var result in results)
            DiagnosticOutput.Trace(directory, spectrum, result, from, to);
        DiagnosticOutput.Plot(directory, spectrum, results, targets, from, to);

        Console.WriteLine("BASELINE local peaks (all maxima, no visibility threshold):");
        foreach (var (name, values) in DiagnosticOutput.Series(spectrum, baseline).Where(s =>
            s.Name.StartsWith("flux_") || s.Name.StartsWith("smoothed_") || s.Name.StartsWith("normalized_") || s.Name == "enhanced_total"))
        {
            var peaks = PeakMeasure.Find(values);
            foreach (var target in targets)
                Console.WriteLine($"  {target:F3}s {name,-20}: {Describe(peaks, target)}");
        }
        Console.WriteLine("ONE-FACTOR/FACTORIAL VARIANTS: normalized total peaks near each target:");
        using var summary = new StreamWriter(Path.Combine(directory, "audio-peaks.csv"), false, Encoding.UTF8);
        summary.WriteLine("variant,target_seconds,peak_seconds,height,prominence,relative_prominence,visible");
        foreach (var result in results)
        {
            var peaks = PeakMeasure.Find(result.Total);
            foreach (var target in targets)
            {
                var nearby = peaks.Where(p => Math.Abs(p.Time - target) <= 0.040001).ToArray();
                Console.WriteLine($"  {result.Variant.Name,-31} {target:F3}s: {Describe(peaks, target)}");
                if (nearby.Length == 0)
                    summary.WriteLine($"{result.Variant.Name},{target:F6},,,,,false");
                foreach (var peak in nearby)
                    summary.WriteLine($"{result.Variant.Name},{target:F6},{peak.Time:F6},{peak.Height:G9},{peak.Prominence:G9},{peak.RelativeProminence:G9},{PeakMeasure.Visible(peak)}");
            }
        }
    }

    private static void RunSynthetic(string directory, bool holdout, IReadOnlyList<Variant> variants)
    {
        using var summary = new StreamWriter(Path.Combine(directory, "synthetic-summary.csv"), false, Encoding.UTF8);
        using var hitsWriter = new StreamWriter(Path.Combine(directory, "synthetic-hits.csv"), false, Encoding.UTF8);
        summary.WriteLine("case,variant,hits,matched,extra,max,baseline_error,mean_error_ms,p95_error_ms,analysis_ms,raw_max,flux_low_max,flux_mid_max,flux_high_max");
        hitsWriter.WriteLine("case,variant,target_seconds,peak_seconds,height,prominence,relative_prominence,visible");
        Console.WriteLine($"SYNTHETIC {(holdout ? "holdout" : "research")} baseline results (one-to-one matching; ignore first/last 0.5s):");
        var cases = holdout ? ImprovementSignals.All(true) : SignalCases.All().Concat(ImprovementSignals.All());
        foreach (var (name, track) in cases)
        {
            var spectrum = Spectrum.Capture(track.Samples, track.SampleRate, track.Channels);
            var baselineWatch = Stopwatch.StartNew();
            var baseline = StageResult.Analyze(spectrum, Variant.Baseline);
            baselineWatch.Stop();
            var difference = StageResult.VerifyBaseline(track.ToSampleData(), baseline);
            var hits = track.HitTimes.Select(t => t.TotalSeconds)
                .Where(t => t >= 0.5 && t <= track.Duration.TotalSeconds - 0.5).ToArray();
            foreach (var variant in variants)
            {
                var watch = Stopwatch.StartNew();
                var result = variant == Variant.Baseline ? baseline : StageResult.Analyze(spectrum, variant);
                watch.Stop();
                if (result.Total.Any(v => !float.IsFinite(v) || v < 0 || v > 1.000001f))
                    throw new InvalidOperationException($"Invalid envelope range for {name}/{variant.Name}.");
                var peaks = PeakMeasure.Find(result.Total);
                var match = PeakMeasure.Match(peaks, hits, track.Duration.TotalSeconds);
                var elapsed = variant == Variant.Baseline ? baselineWatch.Elapsed.TotalMilliseconds : watch.Elapsed.TotalMilliseconds;
                summary.WriteLine($"{name},{variant.Name},{hits.Length},{match.Matched},{match.Extra},{result.Total.Max():G9},{difference:G9},{match.MeanErrorMs:F6},{match.P95ErrorMs:F6},{elapsed:F6},{result.RawTotal.Max():G9},{result.Flux[0].Max():G9},{result.Flux[1].Max():G9},{result.Flux[2].Max():G9}");
                if (variant == Variant.Baseline)
                    Console.WriteLine($"  {name,-32}: {match.Matched}/{hits.Length} hits, {match.Extra} extra peaks, max={result.Total.Max():F4}, baseline error={difference:G9}");
                foreach (var hit in hits)
                {
                    var nearby = peaks.Where(p => Math.Abs(p.Time - hit) <= 0.040001).ToArray();
                    if (nearby.Length == 0)
                        hitsWriter.WriteLine($"{name},{variant.Name},{hit:F6},,,,,false");
                    foreach (var peak in nearby)
                        hitsWriter.WriteLine($"{name},{variant.Name},{hit:F6},{peak.Time:F6},{peak.Height:G9},{peak.Prominence:G9},{peak.RelativeProminence:G9},{PeakMeasure.Visible(peak)}");
                }
            }
            if (name == "silence" && baseline.Total.Any(v => v != 0))
                throw new InvalidOperationException("Silence produced a nonzero baseline curve.");
            if (name == "pair120-equal-short")
            {
                var match = PeakMeasure.Match(PeakMeasure.Find(baseline.Total), hits, track.Duration.TotalSeconds);
                if (match.Matched != hits.Length)
                    throw new InvalidOperationException("Baseline failed to resolve separated, equal-amplitude short strikes.");
            }
        }
    }

    private static string Describe(List<Peak> peaks, double target)
    {
        var nearby = peaks.Where(p => Math.Abs(p.Time - target) <= 0.040001).Select(p =>
            $"{p.Time:F3}s h={p.Height:F4} prominence={p.RelativeProminence:P1}");
        return string.Join("; ", nearby.DefaultIfEmpty("none"));
    }

    private static SampleData ToData(float[] samples, int rate, int channels)
    {
        var bytes = new byte[samples.Length * sizeof(float)];
        Buffer.BlockCopy(samples, 0, bytes, 0, bytes.Length);
        return new(bytes, new SampleInfo { SampleRate = rate, Channels = channels, BitsPerSample = 32 });
    }
}
