using System.Text;
using Avalonia.Platform;
using OngekiFumenEditor.Avalonia.Utils;
using Xunit;

namespace OngekiFumenEditor.Avalonia.Desktop.Tests.Resources;

public sealed class DesktopResourceOverrideRegistrationTests
{
    [Fact]
    public void InstallResourceOverrideAssetLoader_GlobalAssetLoaderReadsDesktopOverride()
    {
        var overrideRoot = Path.Combine(
            Path.GetTempPath(),
            "OngekiFumenEditor.DesktopResourceOverrideTests",
            Guid.NewGuid().ToString("N"));
        var overrideFilePath = Path.Combine(overrideRoot, "Icons", "search.png");
        var expected = Encoding.UTF8.GetBytes("desktop icon override");

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(overrideFilePath)!);
            File.WriteAllBytes(overrideFilePath, expected);

            Program.InstallResourceOverrideAssetLoader(overrideRoot);

            using var stream = AssetLoader.Open(ResourceUtils.GetResourceUri("Icons/search.png"));
            using var buffer = new MemoryStream();
            stream.CopyTo(buffer);
            Assert.Equal(expected, buffer.ToArray());
        }
        finally
        {
            if (Directory.Exists(overrideRoot))
                Directory.Delete(overrideRoot, recursive: true);
        }
    }
}
