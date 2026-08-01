using System;
using System.Collections.Generic;
using System.IO;
using Xunit;

namespace OngekiFumenEditor.Avalonia.Tests.Corpus;

public sealed class CorpusLocatorTests
{
    [Fact]
    public void Locate_EnvironmentVariableAndDefaultExist_UsesEnvironmentOverrideOnly()
    {
        var environmentPath = Path.Combine(Path.GetTempPath(), "ongeki-corpus-environment");
        var defaultPath = Path.Combine(Path.GetTempPath(), "ongeki-corpus-default");
        var visitedPaths = new List<string>();

        var result = CorpusLocator.Locate(environmentPath, defaultPath, path =>
        {
            visitedPaths.Add(path);
            return true;
        });

        Assert.True(result.IsAvailable);
        Assert.True(result.IsExplicitOverride);
        Assert.Equal(CorpusLocationSource.EnvironmentOverride, result.Source);
        Assert.Equal(Path.GetFullPath(environmentPath), result.CandidatePath);
        Assert.Equal(new[] { Path.GetFullPath(environmentPath) }, visitedPaths);
    }

    [Fact]
    public void Locate_ExplicitEnvironmentDirectoryMissing_DoesNotFallBackToDefault()
    {
        var environmentPath = Path.Combine(Path.GetTempPath(), "ongeki-corpus-missing-environment");
        var defaultPath = Path.Combine(Path.GetTempPath(), "ongeki-corpus-existing-default");
        var normalizedDefaultPath = Path.GetFullPath(defaultPath);
        var visitedPaths = new List<string>();

        var result = CorpusLocator.Locate(environmentPath, defaultPath, path =>
        {
            visitedPaths.Add(path);
            return string.Equals(path, normalizedDefaultPath, StringComparison.OrdinalIgnoreCase);
        });

        Assert.False(result.IsAvailable);
        Assert.True(result.IsExplicitOverride);
        Assert.Equal(CorpusLocationSource.Missing, result.Source);
        Assert.StartsWith(CorpusLocator.MissingDiagnosticCode, result.Diagnostic, StringComparison.Ordinal);
        Assert.DoesNotContain(normalizedDefaultPath, visitedPaths);
    }

    [Fact]
    public void Locate_BlankEnvironmentValue_DefaultExists_UsesDefault()
    {
        var defaultPath = Path.Combine(Path.GetTempPath(), "ongeki-corpus-default-only");
        var normalizedDefaultPath = Path.GetFullPath(defaultPath);

        var result = CorpusLocator.Locate("  ", defaultPath, path =>
            string.Equals(path, normalizedDefaultPath, StringComparison.OrdinalIgnoreCase));

        Assert.True(result.IsAvailable);
        Assert.False(result.IsExplicitOverride);
        Assert.Equal(CorpusLocationSource.LocalDefault, result.Source);
        Assert.Equal(normalizedDefaultPath, result.CandidatePath);
    }

    [Fact]
    public void Locate_NoConfiguredDirectoryExists_ReturnsClassifiedMissingResult()
    {
        var defaultPath = Path.Combine(Path.GetTempPath(), "ongeki-corpus-unavailable-default");

        var result = CorpusLocator.Locate(null, defaultPath, _ => false);

        Assert.False(result.IsAvailable);
        Assert.False(result.IsExplicitOverride);
        Assert.Equal(CorpusLocationSource.Missing, result.Source);
        Assert.StartsWith(CorpusLocator.MissingDiagnosticCode, result.Diagnostic, StringComparison.Ordinal);
        Assert.Contains(CorpusLocator.EnvironmentVariableName, result.Diagnostic, StringComparison.Ordinal);
    }
}
