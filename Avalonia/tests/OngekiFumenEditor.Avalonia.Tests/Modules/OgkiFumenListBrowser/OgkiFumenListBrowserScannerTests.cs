using System.Text;
using OngekiFumenEditor.Avalonia.Modules.OgkiFumenListBrowser.Services;
using OngekiFumenEditor.Avalonia.Utils.SimpleFileSystem;
using OngekiFumenEditor.Avalonia.Utils.SimpleFileSystem.Impl.AvaloniaStorageProvider;
using Xunit;

namespace OngekiFumenEditor.Avalonia.Tests.Modules.OgkiFumenListBrowser;

public sealed class OgkiFumenListBrowserScannerTests
{
    [global::Avalonia.Headless.XUnit.AvaloniaFact]
    public async Task ScanAsync_NestedPackage_ProjectsChartAudioAndCoverMetadata()
    {
        using var fixture = new Fixture();
        fixture.Write("data/music/pack/Music.xml", MusicXml(12, 34, "Nested Song", "Artist", "12", "8", "chart.ogkr"));
        fixture.Write("data/music/pack/chart.ogkr", "BPM_DEF 180\nCREATOR Chart Maker\n");
        fixture.Write("data/musicsource/source/MusicSource.xml", MusicSourceXml(34, "music034.wav"));
        fixture.Write("data/musicsource/source/music034.wav", "audio");
        fixture.Write("assets/ui_jacket_0012_s", [1, 2, 3]);

        using var root = await fixture.LoadRootAsync();
        var result = await new OgkiFumenListBrowserScanner([".wav"]).ScanAsync(root);

        var set = Assert.Single(result);
        var diff = Assert.Single(set.Difficults);
        Assert.Equal(12, set.MusicId);
        Assert.Equal(34, set.MusicSourceId);
        Assert.Equal("Nested Song", set.Title);
        Assert.Equal("Artist", set.Artist);
        Assert.Equal(12.08f, diff.Level);
        Assert.Equal(180, diff.Bpm);
        Assert.Equal("Chart Maker", diff.Creator);
        Assert.Equal("data/music/pack/chart.ogkr", diff.FumenLocator);
        Assert.Equal("data/musicsource/source/music034.wav", set.AudioLocator);
        Assert.NotNull(set.AudioFile);
        Assert.NotNull(set.JacketFile);
        Assert.Null(set.JacketBitmap);
    }

    [global::Avalonia.Headless.XUnit.AvaloniaFact]
    public async Task ScanAsync_Utf16MusicSourceXml_UsesDeclaredXmlEncoding()
    {
        using var fixture = new Fixture();
        fixture.Write("music/Music.xml", MusicXml(18, 19, "Utf16 Song", "Artist", "1", "0", "chart.ogkr"));
        fixture.Write("music/chart.ogkr", "chart");
        fixture.Write("source/MusicSource.xml", MusicSourceXml(19, "music019.wav"), Encoding.Unicode);
        fixture.Write("source/music019.wav", "audio");

        using var root = await fixture.LoadRootAsync();
        var result = await new OgkiFumenListBrowserScanner([".wav"]).ScanAsync(root);

        var set = Assert.Single(result);
        Assert.Equal("Utf16 Song", set.Title);
        Assert.Equal("source/music019.wav", set.AudioLocator);
    }

    [global::Avalonia.Headless.XUnit.AvaloniaFact]
    public async Task ScanAsync_AcbWithSameNameAwb_UsesSiblingAwbWithoutInspectingAcbContent()
    {
        using var fixture = new Fixture();
        fixture.Write("music/Music.xml", MusicXml(20, 21, "ACB Song", "Artist", "2", "0", "chart.ogkr"));
        fixture.Write("music/chart.ogkr", "chart");
        fixture.Write("source/MusicSource.xml", MusicSourceXml(21, "music021.acb"));
        fixture.Write("source/music021.acb", "this is not an ACB package");
        fixture.Write("source/music021.awb", "awb");

        using var root = await fixture.LoadRootAsync();
        var result = await new OgkiFumenListBrowserScanner([".acb"]).ScanAsync(root);

        var set = Assert.Single(result);
        Assert.Equal("source/music021.acb", set.AudioLocator);
        Assert.Equal("source/music021.awb", set.AudioAwbLocator);
        Assert.NotNull(set.AudioAwbFile);
    }

    [global::Avalonia.Headless.XUnit.AvaloniaFact]
    public async Task ScanAsync_AcbWithoutSameNameAwb_ExcludesSong()
    {
        using var fixture = new Fixture();
        fixture.Write("music/Music.xml", MusicXml(22, 23, "Missing AWB", "Artist", "2", "0", "chart.ogkr"));
        fixture.Write("music/chart.ogkr", "chart");
        fixture.Write("source/MusicSource.xml", MusicSourceXml(23, "music023.acb"));
        fixture.Write("source/music023.acb", "this is not an ACB package");

        using var root = await fixture.LoadRootAsync();
        Assert.Empty(await new OgkiFumenListBrowserScanner([".acb"]).ScanAsync(root));
    }

    [global::Avalonia.Headless.XUnit.AvaloniaTheory]
    [InlineData("missing.ogkr", "missing")]
    [InlineData("/absolute.ogkr", "absolute")]
    [InlineData("https://outside/chart.ogkr", "uri")]
    [InlineData("../../outside.ogkr", "escape")]
    public async Task ScanAsync_InvalidOrMissingChart_IsExcluded(string chartPath, string _)
    {
        using var fixture = new Fixture();
        fixture.Write("music/Music.xml", MusicXml(1, 2, "Song", "Artist", "1", "0", chartPath));
        fixture.Write("source/MusicSource.xml", MusicSourceXml(2, "music001.wav"));
        fixture.Write("source/music001.wav", "audio");

        using var root = await fixture.LoadRootAsync();
        var result = await new OgkiFumenListBrowserScanner(["wav"]).ScanAsync(root);

        Assert.Empty(result);
    }

    [global::Avalonia.Headless.XUnit.AvaloniaTheory]
    [InlineData("missing")]
    [InlineData("unsupported.txt")]
    public async Task ScanAsync_MissingOrUnsupportedAudio_ExcludesWholeSong(string audioName)
    {
        using var fixture = new Fixture();
        fixture.Write("music/Music.xml", MusicXml(1, 2, "Song", "Artist", "1", "0", "chart.ogkr"));
        fixture.Write("music/chart.ogkr", "chart");
        fixture.Write("source/MusicSource.xml", MusicSourceXml(2, audioName));
        if (audioName != "missing")
            fixture.Write($"source/{audioName}", "audio");

        using var root = await fixture.LoadRootAsync();
        Assert.Empty(await new OgkiFumenListBrowserScanner([".wav"]).ScanAsync(root));
    }

    [global::Avalonia.Headless.XUnit.AvaloniaFact]
    public async Task ScanAsync_DuplicateMusicId_IsContinuouslyDeduplicatedAfterSort()
    {
        using var fixture = new Fixture();
        fixture.Write("music/a/Music.xml", MusicXml(5, 1, "First", "Artist", "1", "0", "chart.ogkr"));
        fixture.Write("music/a/chart.ogkr", "chart");
        fixture.Write("music/b/Music.xml", MusicXml(5, 1, "Second", "Artist", "1", "0", "chart.ogkr"));
        fixture.Write("music/b/chart.ogkr", "chart");
        fixture.Write("source/MusicSource.xml", MusicSourceXml(1, "music005.wav"));
        fixture.Write("source/music005.wav", "audio");

        using var root = await fixture.LoadRootAsync();
        var result = await new OgkiFumenListBrowserScanner([".wav"]).ScanAsync(root);

        var set = Assert.Single(result);
        Assert.Equal("First", set.Title);
    }

    [Fact]
    public void RelativePath_RejectsAbsoluteUriDriveAndRootEscape()
    {
        Assert.False(OgkiFumenListBrowserPath.TryNormalizeRelative("C:/outside.ogkr", out _));
        Assert.False(OgkiFumenListBrowserPath.TryNormalizeRelative("https://example/chart.ogkr", out _));
        Assert.False(OgkiFumenListBrowserPath.TryCombineRelative("music", "../../outside.ogkr", out _));
        Assert.True(OgkiFumenListBrowserPath.TryCombineRelative("music/pack", "../chart.ogkr", out var locator));
        Assert.Equal("music/chart.ogkr", locator);
    }

    private static string MusicXml(
        int musicId,
        int sourceId,
        string title,
        string artist,
        string integerPart,
        string fractionalPart,
        string chartPath) => $"""
            <MusicData>
              <Name><id>{musicId}</id><str>{title}</str></Name>
              <ArtistName><str>{artist}</str></ArtistName>
              <MusicSourceName><id>{sourceId}</id></MusicSourceName>
              <Genre><str>VARIETY</str></Genre>
              <FumenData><FumenData>
                <FumenConstIntegerPart>{integerPart}</FumenConstIntegerPart>
                <FumenConstFractionalPart>{fractionalPart}</FumenConstFractionalPart>
                <FumenFile><path>{chartPath}</path></FumenFile>
              </FumenData></FumenData>
            </MusicData>
            """;

    private static string MusicSourceXml(int sourceId, string audioName) => $"""
        <MusicSourceData>
          <Name><id>{sourceId}</id></Name>
          <acbFile><path>{audioName}</path></acbFile>
        </MusicSourceData>
        """;

    private sealed class Fixture : IDisposable
    {
        public Fixture()
        {
            RootPath = Path.Combine(Path.GetTempPath(), "OngekiFumenEditor.OgkiScannerTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(RootPath);
        }

        public string RootPath { get; }

        public void Write(string relativePath, string content) => Write(relativePath, Encoding.UTF8.GetBytes(content));

        public void Write(string relativePath, string content, Encoding encoding)
        {
            var preamble = encoding.GetPreamble();
            Write(relativePath, [.. preamble, .. encoding.GetBytes(content)]);
        }

        public void Write(string relativePath, byte[] content)
        {
            var path = Path.Combine(RootPath, relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllBytes(path, content);
        }

        public async Task<ISimpleDirectory> LoadRootAsync()
        {
            var window = new global::Avalonia.Controls.Window();
            var folder = await window.StorageProvider.TryGetFolderFromPathAsync(new Uri(RootPath))
                ?? throw new InvalidOperationException("Unable to create storage folder.");
            return await AvaloniaStorageProviderFileSystemBuilder.LoadFromAvaloniaStorageFolder(folder);
        }

        public void Dispose()
        {
            if (Directory.Exists(RootPath))
                Directory.Delete(RootPath, recursive: true);
        }
    }
}
