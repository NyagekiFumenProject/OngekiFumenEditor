using System;
using System.IO;

namespace OngekiFumenEditor.Avalonia.Tests.Corpus;

internal enum CorpusLocationSource
{
    EnvironmentOverride,
    LocalDefault,
    Missing
}

internal sealed record CorpusLocation(
    string CandidatePath,
    bool IsAvailable,
    bool IsExplicitOverride,
    CorpusLocationSource Source,
    string Diagnostic);

internal static class CorpusLocator
{
    public const string EnvironmentVariableName = "ONGEKI_FUMEN_TEST_CORPUS_ROOT";
    public const string LocalDefaultPath = @"C:\Users\mikir\Desktop\音寄谱\拉面";
    public const string MissingDiagnosticCode = "CORPUS_MISSING";

    public static CorpusLocation Locate() => Locate(
        Environment.GetEnvironmentVariable(EnvironmentVariableName),
        LocalDefaultPath,
        Directory.Exists);

    internal static CorpusLocation Locate(
        string? environmentPath,
        string localDefaultPath,
        Func<string, bool> directoryExists)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(localDefaultPath);
        ArgumentNullException.ThrowIfNull(directoryExists);

        if (!string.IsNullOrWhiteSpace(environmentPath))
        {
            var overridePath = Path.GetFullPath(environmentPath.Trim());
            return directoryExists(overridePath)
                ? new CorpusLocation(
                    overridePath,
                    true,
                    true,
                    CorpusLocationSource.EnvironmentOverride,
                    $"Corpus resolved from {EnvironmentVariableName}.")
                : new CorpusLocation(
                    overridePath,
                    false,
                    true,
                    CorpusLocationSource.Missing,
                    $"{MissingDiagnosticCode}: {EnvironmentVariableName} points to a missing directory: {overridePath}");
        }

        var defaultPath = Path.GetFullPath(localDefaultPath);
        return directoryExists(defaultPath)
            ? new CorpusLocation(
                defaultPath,
                true,
                false,
                CorpusLocationSource.LocalDefault,
                "Corpus resolved from the local default path.")
            : new CorpusLocation(
                defaultPath,
                false,
                false,
                CorpusLocationSource.Missing,
                $"{MissingDiagnosticCode}: neither {EnvironmentVariableName} nor the local default directory is available: {defaultPath}");
    }
}
