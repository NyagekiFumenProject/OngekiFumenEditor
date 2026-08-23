using Avalonia.Platform;
using OngekiFumenEditor.Avalonia.Kernel.Audio.DefaultCommonImpl.Sound;
using OngekiFumenEditor.Avalonia.Utils;
using Xunit;

namespace OngekiFumenEditor.Avalonia.Tests.Utils;

public sealed class ResourceUtilsTests
{
    [Fact]
    public void EmbeddedResources_MatchAllFilesUnderResourceSourceDirectory()
    {
        var sourceRoot = Path.Combine(
            FindRepositoryRoot(),
            "src",
            "OngekiFumenEditor.Avalonia",
            "Resources");
        var expected = Directory.EnumerateFiles(sourceRoot, "*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(sourceRoot, path).Replace('\\', '/'))
            .Order(StringComparer.Ordinal)
            .ToArray();

        var loader = new StandardAssetLoader(typeof(ResourceUtils).Assembly);
        var actual = loader.GetAssets(
                new Uri("avares://OngekiFumenEditor.Avalonia/Resources/"),
                baseUri: null)
            .Select(uri => Uri.UnescapeDataString(uri.AbsolutePath)["/Resources/".Length..])
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(96, expected.Length);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void OpenReadResourceStream_LoadsEmbeddedResource()
    {
        using var stream = ResourceUtils.OpenReadResourceStream(
            @"editor\textureSizeAnchor.ini");
        using var reader = new StreamReader(stream);

        Assert.Contains("tapSize", reader.ReadToEnd(), StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("/absolute.txt")]
    [InlineData("../escape.txt")]
    [InlineData("editor//tap.png")]
    [InlineData("editor/./tap.png")]
    public void NormalizeResourcePath_InvalidPath_Throws(string resourcePath)
    {
        Assert.Throws<ArgumentException>(() => ResourceUtils.NormalizeResourcePath(resourcePath));
    }

    [Fact]
    public void OpenSoundStream_LoadsEmbeddedSound()
    {
        var expectedEmbeddedBytes = ReadEmbeddedBytes("sounds/tap.wav");

        using var stream = DefaultFumenSoundPlayer.OpenSoundStream("tap.wav");

        Assert.Equal(expectedEmbeddedBytes, ReadAllBytes(stream));
    }

    private static byte[] ReadEmbeddedBytes(string resourcePath)
    {
        using var stream = ResourceUtils.OpenReadResourceStream(resourcePath);
        return ReadAllBytes(stream);
    }

    private static byte[] ReadAllBytes(Stream stream)
    {
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "OngekiFumenEditor.Avalonia.sln")))
                return directory.FullName;
        }

        throw new DirectoryNotFoundException("Could not find the Avalonia repository root.");
    }

}
