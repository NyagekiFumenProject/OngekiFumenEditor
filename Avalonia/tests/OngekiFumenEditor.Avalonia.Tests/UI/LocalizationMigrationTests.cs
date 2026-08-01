using System.Globalization;
using OngekiFumenEditor.Avalonia.UI.ValueConverters;
using Xunit;

namespace OngekiFumenEditor.Avalonia.Tests.UI;

public sealed class LocalizationMigrationTests
{
    [Fact]
    public void LocalizeConverter_FormatsAllStringifiedArgumentsInOrder()
    {
        var converter = new LocalizeConverter();

        var result = converter.Convert(
            ["{0:N1} / {1}", 1234.5m, "ready"],
            typeof(string),
            parameter: null!,
            CultureInfo.InvariantCulture);

        Assert.Equal("1234.5 / ready", result);
    }

    [Fact]
    public void LocalizeConverter_ConvertsNullArgumentsToEmptyStrings()
    {
        var converter = new LocalizeConverter();

        var result = converter.Convert(
            ["<{0}>", null!],
            typeof(string),
            parameter: null!,
            CultureInfo.InvariantCulture);

        Assert.Equal("<>", result);
    }

    [Theory]
    [InlineData()]
    [InlineData("{0}")]
    public void LocalizeConverter_RequiresFormatAndAtLeastOneArgument(params object[] values)
    {
        var converter = new LocalizeConverter();

        var exception = Assert.Throws<ArgumentException>(() => converter.Convert(
            values,
            typeof(string),
            parameter: null!,
            CultureInfo.InvariantCulture));

        Assert.Contains(">=2 values", exception.Message, StringComparison.Ordinal);
    }
}
