using System.Globalization;
using System.Text;
using System.Xml.Linq;

namespace OngekiFumenEditor.RhythmDiagnostics;

internal static class DiagnosticOutput
{
    internal static IEnumerable<(string Name, float[] Values)> Series(Spectrum spectrum, StageResult result)
    {
        yield return ("mono_rms", spectrum.MonoRms);
        yield return ("channels_rms", spectrum.ChannelRms);
        yield return ("continuous_highpass800_rms", spectrum.HighRms);
        var names = new[] { "low", "mid", "high" };
        for (var band = 0; band < 3; band++)
        {
            yield return ($"spectral_energy_{names[band]}", spectrum.BandEnergy[band]);
            yield return ($"flux_{names[band]}", result.Flux[band]);
            yield return ($"smoothed_{names[band]}", result.Smoothed[band]);
            yield return ($"normalized_{names[band]}", result.Normalized[band]);
        }
        yield return ("flux_total", result.RawTotal);
        yield return ("smoothed_total", result.SmoothedTotal);
        yield return ("normalized_total", result.Total);
        yield return ("enhanced_total", result.Enhanced);
    }

    internal static void Trace(string directory, Spectrum spectrum, StageResult result, double from, double to)
    {
        var series = Series(spectrum, result).ToArray();
        using var writer = new StreamWriter(Path.Combine(directory, result.Variant.Name + ".csv"), false, Encoding.UTF8);
        writer.WriteLine("time_seconds,sample_center_seconds,rms_block_center_seconds," + string.Join(',', series.Select(s => s.Name)));
        var first = Math.Max(0, (int)Math.Floor(from * Spectrum.FrameRate));
        var last = Math.Min(spectrum.FrameCount - 1, (int)Math.Ceiling(to * Spectrum.FrameRate));
        for (var i = first; i <= last; i++)
        {
            writer.Write(FormattableString.Invariant($"{i / (double)Spectrum.FrameRate:F6},{i * spectrum.Hop / (double)spectrum.SampleRate:F6},{(i + 0.5) * spectrum.Hop / spectrum.SampleRate:F6}"));
            foreach (var (_, values) in series)
                writer.Write("," + values[i].ToString("G9", CultureInfo.InvariantCulture));
            writer.WriteLine();
        }
    }

    internal static void Plot(string directory, Spectrum spectrum, IReadOnlyList<StageResult> results,
        double[] targets, double from, double to)
    {
        var baseline = results[0];
        var rows = new List<(string Name, float[] Values)>
        {
            ("Continuous high-pass 800 Hz RMS (5 ms blocks)", spectrum.HighRms),
            ("Baseline HIGH band: before smoothing", baseline.Flux[2]),
            ("Baseline HIGH band: after smoothing", baseline.Smoothed[2]),
            ("Baseline total: before smoothing (unnormalized)", baseline.RawTotal),
            ("Baseline total: after smoothing (unnormalized)", baseline.SmoothedTotal),
            ("Baseline total: after band normalization", baseline.Total),
            ("Baseline total: Enhanced display mapping", baseline.Enhanced),
        };
        foreach (var result in results.Skip(1))
            rows.Add((result.Variant.Name, result.Total));

        const int width = 1200;
        const int rowHeight = 115;
        const int left = 85;
        const int right = 30;
        var height = 75 + rows.Count * rowHeight;
        XNamespace ns = "http://www.w3.org/2000/svg";
        var svg = new XElement(ns + "svg", new XAttribute("width", width), new XAttribute("height", height),
            new XAttribute("viewBox", $"0 0 {width} {height}"),
            new XElement(ns + "rect", new XAttribute("width", "100%"), new XAttribute("height", "100%"), new XAttribute("fill", "#101723")));
        void Text(double x, double y, string text, string color = "#d7e2ee") => svg.Add(new XElement(ns + "text",
            new XAttribute("x", F(x)), new XAttribute("y", F(y)), new XAttribute("fill", color),
            new XAttribute("font-family", "Consolas,monospace"), new XAttribute("font-size", 14), text));
        double X(double t) => left + (t - from) / (to - from) * (width - left - right);
        Text(left, 25, "Rhythm stage diagnosis: row-local y scales; dashed markers are requested times, NOT detected accents.");
        Text(left, 48, $"Mean reference 150 ms; nominal smoothing 50 ms is actually 11 samples / 55 ms at 200 Hz.");
        var first = Math.Max(0, (int)Math.Floor(from * Spectrum.FrameRate));
        var last = Math.Min(spectrum.FrameCount - 1, (int)Math.Ceiling(to * Spectrum.FrameRate));
        for (var row = 0; row < rows.Count; row++)
        {
            var (name, values) = rows[row];
            var top = 70 + row * rowHeight;
            var bottom = top + 90;
            var max = 0f;
            for (var i = first; i <= last; i++)
                max = Math.Max(max, values[i]);
            var scale = max > 0 ? max : 1;
            Text(left, top + 15, name + "   y-max=" + max.ToString("G4", CultureInfo.InvariantCulture));
            var points = new StringBuilder();
            for (var i = first; i <= last; i++)
            {
                // RMS blocks are time-stamped at their center; STFT/curve data at the frame center.
                var time = row == 0 ? (i + 0.5) * spectrum.Hop / spectrum.SampleRate : i / (double)Spectrum.FrameRate;
                points.Append(F(X(time))).Append(',').Append(F(bottom - values[i] / scale * 62)).Append(' ');
            }
            svg.Add(new XElement(ns + "polyline", new XAttribute("points", points.ToString()),
                new XAttribute("fill", "none"), new XAttribute("stroke", "#5de0c1"), new XAttribute("stroke-width", "1.7")));
            foreach (var target in targets)
            {
                svg.Add(new XElement(ns + "line", new XAttribute("x1", F(X(target))), new XAttribute("x2", F(X(target))),
                    new XAttribute("y1", top + 20), new XAttribute("y2", bottom), new XAttribute("stroke", "#d99565"),
                    new XAttribute("stroke-dasharray", "4 4")));
                Text(X(target) + 4, bottom + 15, target.ToString("F3", CultureInfo.InvariantCulture), "#d99565");
            }
        }
        new XDocument(svg).Save(Path.Combine(directory, "stages.svg"));
    }

    private static string F(double value) => value.ToString("F2", CultureInfo.InvariantCulture);
}
