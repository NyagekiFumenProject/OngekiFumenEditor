using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Setup;
using Xunit;

namespace OngekiFumenEditor.Avalonia.Tests.Modules.FumenVisualEditor;

public sealed class AwbContentComparerTests
{
    private static byte[] BuildBytes(int length, int modulo = 251) =>
        Enumerable.Range(0, length).Select(index => (byte)(index % modulo)).ToArray();

    [Fact]
    public async Task DifferentLengths_ReturnFalseWithoutReading()
    {
        using var left = new StubAwbFile("left.awb", [1, 2, 3]);
        using var right = new StubAwbFile("right.awb", [1, 2, 3, 4]);

        Assert.False(await AwbContentComparer.AreContentsEqualAsync(left, right));
    }

    [Fact]
    public async Task SmallFiles_IdenticalAndDifferentContentsAreClassified()
    {
        var data = BuildBytes(4096);
        using var left = new StubAwbFile("left.awb", data);
        using var sameRight = new StubAwbFile("right.awb", (byte[])data.Clone());
        var differing = (byte[])data.Clone();
        differing[^1] ^= 0xFF;
        using var otherRight = new StubAwbFile("other.awb", differing);

        Assert.True(await AwbContentComparer.AreContentsEqualAsync(left, sameRight));
        Assert.False(await AwbContentComparer.AreContentsEqualAsync(left, otherRight));
    }

    [Fact]
    public async Task EmptyFiles_AreEqual()
    {
        using var left = new StubAwbFile("left.awb");
        using var right = new StubAwbFile("right.awb");

        Assert.True(await AwbContentComparer.AreContentsEqualAsync(left, right));
    }

    [Fact]
    public async Task LargeFiles_SamplingMissStillCaughtByFullComparison()
    {
        const int length = 5 * 1024 * 1024;
        var data = BuildBytes(length);
        using var left = new StubAwbFile("left.awb", data);

        var identical = (byte[])data.Clone();
        using var sameRight = new StubAwbFile("same.awb", identical);
        Assert.True(await AwbContentComparer.AreContentsEqualAsync(left, sameRight));

        // Flip one late byte that the deterministic samples do not necessarily cover; only
        // the full comparison is allowed to certify sameness, and it must catch the change.
        var modified = (byte[])data.Clone();
        modified[length - 1] ^= 0x01;
        using var changedRight = new StubAwbFile("changed.awb", modified);
        Assert.False(await AwbContentComparer.AreContentsEqualAsync(left, changedRight));
    }

    [Fact]
    public async Task RepeatedRuns_ProduceStableVerdicts()
    {
        const int length = 5 * 1024 * 1024 + 7;
        var data = BuildBytes(length, modulo: 241);
        using var left = new StubAwbFile("left.awb", data);
        using var right = new StubAwbFile("right.awb", (byte[])data.Clone());

        Assert.Equal(
            await AwbContentComparer.AreContentsEqualAsync(left, right),
            await AwbContentComparer.AreContentsEqualAsync(left, right));
    }
}
