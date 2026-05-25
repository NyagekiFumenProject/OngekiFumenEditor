namespace OngekiFumenEditor.Benchmark.Infrastructure.ResultComparison;

public enum BaselineMode
{
    Prompt,
    Save,
    Skip
}

public sealed record BaselineOptions(
    BaselineMode Mode,
    bool Disabled,
    double MeanThresholdPercent = 5.0,
    double AllocThresholdPercent = 1.0)
{
    public static BaselineOptions Default => new(BaselineMode.Prompt, Disabled: false);
}
