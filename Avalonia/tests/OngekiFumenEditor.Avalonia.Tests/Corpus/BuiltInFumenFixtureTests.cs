using System.Text;
using Avalonia.Headless.XUnit;
using Xunit;

namespace OngekiFumenEditor.Avalonia.Tests.Corpus;

public sealed class BuiltInFumenFixtureTests
{
    [AvaloniaFact]
    public async Task BuiltInFixture_ParseFormatReparse_PreservesSupportedSemanticFingerprint()
    {
        var fixturePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "minimal.nyageki");
        Assert.True(File.Exists(fixturePath), $"Built-in fixture was not copied to: {fixturePath}");
        using var harness = new NyagekiCorpusHarness();

        var parsed = await harness.ParseFileAsync(fixturePath);
        var originalFingerprint = FumenSemanticFingerprint.Capture(parsed);
        var serialized = await harness.Formatter.SerializeAsync(parsed);
        var reparsed = await harness.ParseBytesAsync(serialized);
        var reparsedFingerprint = FumenSemanticFingerprint.Capture(reparsed);

        Assert.Equal("1.0.0", originalFingerprint.Version);
        Assert.Equal("Avalonia migration test", originalFingerprint.Creator);
        Assert.Equal(1, originalFingerprint.Lanes);
        Assert.Equal(1, originalFingerprint.Taps);
        Assert.Equal(1, originalFingerprint.TapsWithLaneReference);
        Assert.NotEmpty(serialized);
        Assert.Contains("Lane", Encoding.UTF8.GetString(serialized), StringComparison.Ordinal);
        Assert.Equal(originalFingerprint, reparsedFingerprint);
    }
}
