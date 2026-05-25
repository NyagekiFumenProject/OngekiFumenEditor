# OngekiFumenEditor.Benchmark

BenchmarkDotNet benchmarks for UI-independent fumen paths: parsing, displayable object enumeration, collection queries, and curve path interpolation. The project initializes `App(false)` and `AppBootstrapper(false)` to set up MEF/IoC without creating a window or starting WPF rendering.

Samples are embedded from `Data/FumenSamples`, so benchmark runs do not depend on the original sample directory.

## Commands

Build:

```powershell
dotnet build .\OngekiFumenEditor.Benchmark\OngekiFumenEditor.Benchmark.csproj -c Release
```

Run all benchmarks:

```powershell
dotnet run -c Release --project .\OngekiFumenEditor.Benchmark -- --filter *
```

Run one benchmark class:

```powershell
dotnet run -c Release --project .\OngekiFumenEditor.Benchmark -- --filter *ParsingBenchmarks*
```

Fast smoke test:

```powershell
dotnet run -c Release --project .\OngekiFumenEditor.Benchmark -- --filter *ParsingBenchmarks* --job Dry
```

ETW profiling is available on Windows when needed:

```powershell
dotnet run -c Release --project .\OngekiFumenEditor.Benchmark -- --filter *CurvePathBenchmarks* --profiler ETW
```

## Baselines

After all benchmarks finish, the runner loads the previously saved baseline for each benchmark class, prints a comparison table (Mean / Allocated, with Δ% and a status of `OK` / `REGRESSION` / `IMPROVED` / `NEW` / `GONE`), and asks once whether to save the current results as the new baseline.

- Baselines live under `BenchmarkDotNet.Artifacts/Baselines/{BenchmarkClassFullName}.json`. The directory is already covered by `.gitignore`, so baselines are local-only by default.
- Default thresholds: ±5% on Mean, ±1% on Allocated bytes. Changes within the threshold are reported as `OK`.
- Colored output uses ANSI escapes; it is disabled automatically when stdout is redirected or when `NO_COLOR` is set.
- When stdin is redirected (CI), the prompt is suppressed and nothing is saved unless the mode is overridden.

### CLI flags

| Flag | Effect |
|------|--------|
| `--no-baseline-prompt` | Print the comparison but skip the save prompt. |
| `--save-baseline` | Save current results without prompting (overwrites existing baselines). |
| `--no-baseline-compare` | Skip the entire compare / save subsystem. |

### Environment variable

`BENCHMARK_BASELINE_MODE=prompt|save|skip` mirrors the three modes above. CLI flags take precedence over the env var.

Examples:

```powershell
# Save baseline silently (useful for "I trust this run, record it")
dotnet run -c Release --project .\OngekiFumenEditor.Benchmark -- --filter *CollectionQueryBenchmarks* --save-baseline

# Diff against baseline but never save (CI)
dotnet run -c Release --project .\OngekiFumenEditor.Benchmark -- --filter * --no-baseline-prompt

# Restore previous behavior (no diff, no save)
dotnet run -c Release --project .\OngekiFumenEditor.Benchmark -- --filter * --no-baseline-compare
```
