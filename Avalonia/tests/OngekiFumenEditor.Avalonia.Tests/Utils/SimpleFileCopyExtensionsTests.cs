using OngekiFumenEditor.Avalonia.Tests.Modules.FumenVisualEditor;
using OngekiFumenEditor.Avalonia.Utils.SimpleFileSystem;
using Xunit;

namespace OngekiFumenEditor.Avalonia.Tests.Utils;

public sealed class SimpleFileCopyExtensionsTests
{
    [Fact]
    public async Task CopyContentToAsync_CommitsSourceBytesIntoTarget()
    {
        byte[] payload = [0x10, 0x20, 0x30, 0x40, 0x50];
        using var source = new StubAwbFile("source.awb", payload);
        using var target = new StubAwbFile("target.awb", [0xAA]);

        await source.CopyContentToAsync(target);

        Assert.Equal(payload, await target.ReadAllBytes());
    }

    [Fact]
    public async Task CopyContentToAsync_FailedSourceKeepsTargetContentIntact()
    {
        using var broken = new BrokenOpenFile("broken.awb", [1, 2, 3]);
        using var target = new StubAwbFile("target.awb", [0xBB]);

        await Assert.ThrowsAsync<IOException>(
            () => broken.CopyContentToAsync(target));

        Assert.Equal([0xBB], await target.ReadAllBytes());
    }
}
