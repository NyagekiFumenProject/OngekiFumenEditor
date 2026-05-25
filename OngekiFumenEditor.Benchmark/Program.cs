using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Toolchains;
using BenchmarkDotNet.Toolchains.InProcess.Emit;
using OngekiFumenEditor.Benchmark.Infrastructure.ResultComparison;

namespace OngekiFumenEditor.Benchmark;

internal static class Program
{
    // Reason: the benchmark project transitively references the WPF host (OngekiFumenEditor)
    // and Gemini. BenchmarkDotNet's default toolchain auto-generates a wrapper project and
    // re-builds the dependency graph with /p:OutDir overrides, which collides with WPF's
    // MarkupCompile temp project (XAML codegen breaks). We strip --job from CLI and apply
    // it ourselves with the InProcessEmit toolchain so jobs always run inside this
    // already-built host process and BDN never spawns a wrapper build.
    private static readonly IToolchain InProcessToolchain =
        new InProcessEmitToolchain(TimeSpan.FromHours(2), logOutput: false);

    [STAThread]
    private static void Main(string[] args)
    {
        var (presetJob, baselineOptions, remainingArgs) = ExtractCliOptions(args);
        var job = presetJob.WithToolchain(InProcessToolchain);

        var config = DefaultConfig.Instance
            .AddDiagnoser(MemoryDiagnoser.Default)
            .AddJob(job);

        var summaries = BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(remainingArgs, config);

        if (!baselineOptions.Disabled)
        {
            try
            {
                PostRunHandler.HandleSummaries(summaries, baselineOptions);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Baseline] Post-run handler failed: {ex}");
            }
        }
    }

    private static (Job Job, BaselineOptions Baseline, string[] Args) ExtractCliOptions(string[] args)
    {
        var preset = Job.Default;
        var mode = ResolveBaselineModeFromEnv();
        var disabled = false;
        var remaining = new List<string>(args.Length);

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];

            if ((arg.Equals("--job", StringComparison.OrdinalIgnoreCase)
                    || arg.Equals("-j", StringComparison.OrdinalIgnoreCase))
                && i + 1 < args.Length)
            {
                preset = ResolveJobPreset(args[i + 1]) ?? preset;
                i++;
                continue;
            }

            if (arg.StartsWith("--job=", StringComparison.OrdinalIgnoreCase))
            {
                preset = ResolveJobPreset(arg["--job=".Length..]) ?? preset;
                continue;
            }

            if (arg.Equals("--no-baseline-prompt", StringComparison.OrdinalIgnoreCase))
            {
                mode = BaselineMode.Skip;
                continue;
            }

            if (arg.Equals("--save-baseline", StringComparison.OrdinalIgnoreCase))
            {
                mode = BaselineMode.Save;
                continue;
            }

            if (arg.Equals("--no-baseline-compare", StringComparison.OrdinalIgnoreCase))
            {
                disabled = true;
                continue;
            }

            remaining.Add(arg);
        }

        var baseline = BaselineOptions.Default with { Mode = mode, Disabled = disabled };
        return (preset, baseline, remaining.ToArray());
    }

    private static BaselineMode ResolveBaselineModeFromEnv()
    {
        var env = Environment.GetEnvironmentVariable("BENCHMARK_BASELINE_MODE");
        if (string.IsNullOrWhiteSpace(env))
            return BaselineMode.Prompt;

        return env.Trim().ToLowerInvariant() switch
        {
            "save" => BaselineMode.Save,
            "skip" => BaselineMode.Skip,
            "prompt" => BaselineMode.Prompt,
            _ => BaselineMode.Prompt
        };
    }

    private static Job? ResolveJobPreset(string name) => name.ToLowerInvariant() switch
    {
        "dry" => Job.Dry,
        "short" => Job.ShortRun,
        "medium" => Job.MediumRun,
        "long" => Job.LongRun,
        "verylong" => Job.VeryLongRun,
        "default" => Job.Default,
        _ => null
    };
}
