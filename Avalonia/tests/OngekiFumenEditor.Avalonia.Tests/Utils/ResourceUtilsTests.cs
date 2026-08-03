using System.Reflection;
using System.Text;
using Avalonia.Platform;
using OngekiFumenEditor.Avalonia.Kernel.Audio.DefaultCommonImpl.Sound;
using OngekiFumenEditor.Avalonia.Utils;
using OngekiFumenEditor.Avalonia.Utils.SimpleFileSystem.Impl.LocalFileSystem;
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
    public void OpenReadResourceStream_ExistingOverride_UsesOverrideFile()
    {
        using var directory = new TemporaryDirectory();
        directory.Write("MusicSource.xml", "desktop override");

        using var stream = ResourceUtils.OpenReadResourceStream(
            "MusicSource.xml",
            directory.RootPath,
            allowLocalOverride: true);
        using var reader = new StreamReader(stream, Encoding.UTF8);

        Assert.Equal("desktop override", reader.ReadToEnd());
    }

    [Fact]
    public void OpenReadResourceStream_MissingOverride_FallsBackToEmbeddedResource()
    {
        using var directory = new TemporaryDirectory();

        using var stream = ResourceUtils.OpenReadResourceStream(
            "MusicSource.xml",
            directory.RootPath,
            allowLocalOverride: true);
        using var reader = new StreamReader(stream, Encoding.UTF8);

        Assert.Contains("<dataName>", reader.ReadToEnd(), StringComparison.Ordinal);
    }

    [Fact]
    public void OpenReadResourceStream_LocalOverridesDisabled_IgnoresOverrideFile()
    {
        using var directory = new TemporaryDirectory();
        directory.Write("MusicSource.xml", "desktop override");

        using var stream = ResourceUtils.OpenReadResourceStream(
            "MusicSource.xml",
            directory.RootPath,
            allowLocalOverride: false);
        using var reader = new StreamReader(stream, Encoding.UTF8);

        Assert.Contains("<dataName>", reader.ReadToEnd(), StringComparison.Ordinal);
    }

    [Fact]
    public void OpenReadResourceStream_BackslashPath_LoadsEmbeddedResource()
    {
        using var directory = new TemporaryDirectory();

        using var stream = ResourceUtils.OpenReadResourceStream(
            @"editor\textureSizeAnchor.ini",
            directory.RootPath,
            allowLocalOverride: true);
        using var reader = new StreamReader(stream, Encoding.UTF8);

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
    public void ResourceOverrideAssetLoader_CoreResourceUri_UsesOverrideAndFallsBackPerFile()
    {
        using var directory = new TemporaryDirectory();
        var overrideBytes = Encoding.UTF8.GetBytes("icon override");
        directory.Write("Icons/search.png", overrideBytes);
        var inner = new StandardAssetLoader(typeof(ResourceUtils).Assembly);
        var loader = new ResourceOverrideAssetLoader(inner, directory.RootPath);
        var overrideUri = ResourceUtils.GetResourceUri("Icons/search.png");

        Assert.True(loader.Exists(overrideUri));
        using (var stream = loader.Open(overrideUri))
        {
            Assert.Equal(overrideBytes, ReadAllBytes(stream));
        }

        var (overrideStream, assembly) = loader.OpenAndGetAssembly(overrideUri);
        using (overrideStream)
        {
            Assert.Equal(typeof(ResourceUtils).Assembly, assembly);
            Assert.Equal(overrideBytes, ReadAllBytes(overrideStream));
        }

        using var fallbackStream = loader.Open(ResourceUtils.GetResourceUri("MusicSource.xml"));
        using var reader = new StreamReader(fallbackStream, Encoding.UTF8);
        Assert.Contains("<dataName>", reader.ReadToEnd(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task OpenSoundFileAsync_CustomThenExecutableThenEmbedded_UsesPerFilePriority()
    {
        using var directory = new TemporaryDirectory();
        var customFolder = directory.CreateDirectory("custom-sounds");
        var resourceRoot = directory.CreateDirectory("Resources");
        var customPath = directory.Write("custom-sounds/tap.wav", "custom sound");
        var overridePath = directory.Write("Resources/sounds/tap.wav", "desktop sound");

        using (var custom = await DefaultFumenSoundPlayer.OpenSoundFileAsync(
                   "tap.wav",
                   customFolder,
                   directory.RootPath,
                   resourceRoot,
                   allowLocalFiles: true))
        {
            Assert.IsType<LocalSimpleFile>(custom);
            Assert.Equal(Path.GetFullPath(customPath), custom.LocalPath);
            Assert.Equal("custom sound", Encoding.UTF8.GetString(await custom.ReadAllBytes()));
        }

        File.Delete(customPath);
        using (var executableOverride = await DefaultFumenSoundPlayer.OpenSoundFileAsync(
                   "tap.wav",
                   customFolder,
                   directory.RootPath,
                   resourceRoot,
                   allowLocalFiles: true))
        {
            Assert.Null(executableOverride.LocalPath);
            Assert.Equal("desktop sound", Encoding.UTF8.GetString(await executableOverride.ReadAllBytes()));
        }

        File.Delete(overridePath);
        var expectedEmbeddedBytes = ReadEmbeddedBytes("sounds/tap.wav");
        using var embedded = await DefaultFumenSoundPlayer.OpenSoundFileAsync(
            "tap.wav",
            customFolder,
            directory.RootPath,
            resourceRoot,
            allowLocalFiles: true);
        Assert.Null(embedded.LocalPath);
        Assert.Equal(expectedEmbeddedBytes, await embedded.ReadAllBytes());
    }

    [Fact]
    public async Task OpenSoundFileAsync_LocalFilesDisabled_UsesEmbeddedResourceOnly()
    {
        using var directory = new TemporaryDirectory();
        var customFolder = directory.CreateDirectory("custom-sounds");
        var resourceRoot = directory.CreateDirectory("Resources");
        directory.Write("custom-sounds/tap.wav", "custom sound");
        directory.Write("Resources/sounds/tap.wav", "desktop sound");
        var expectedEmbeddedBytes = ReadEmbeddedBytes("sounds/tap.wav");

        using var file = await DefaultFumenSoundPlayer.OpenSoundFileAsync(
            "tap.wav",
            customFolder,
            directory.RootPath,
            resourceRoot,
            allowLocalFiles: false);

        Assert.Null(file.LocalPath);
        Assert.Equal(expectedEmbeddedBytes, await file.ReadAllBytes());
    }

    private static byte[] ReadEmbeddedBytes(string resourcePath)
    {
        using var stream = ResourceUtils.OpenReadResourceStream(
            resourcePath,
            Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")),
            allowLocalOverride: false);
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

    private sealed class TemporaryDirectory : IDisposable
    {
        public string RootPath { get; } = Path.Combine(
            Path.GetTempPath(),
            "OngekiFumenEditor.ResourceUtilsTests",
            Guid.NewGuid().ToString("N"));

        public TemporaryDirectory() => Directory.CreateDirectory(RootPath);

        public string CreateDirectory(string relativePath)
        {
            var path = Path.Combine(RootPath, relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(path);
            return path;
        }

        public string Write(string relativePath, string content) =>
            Write(relativePath, Encoding.UTF8.GetBytes(content));

        public string Write(string relativePath, byte[] content)
        {
            var path = Path.Combine(RootPath, relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllBytes(path, content);
            return path;
        }

        public void Dispose()
        {
            if (Directory.Exists(RootPath))
                Directory.Delete(RootPath, recursive: true);
        }
    }
}
