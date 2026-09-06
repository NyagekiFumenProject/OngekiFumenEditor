using Gekimini.Avalonia.Framework.Dialogs;
using Gekimini.Avalonia.Framework.RecentFiles;
using Gekimini.Avalonia.Modules.Shell;
using OngekiFumenEditor.Avalonia.Kernel.Audio;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Models;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Setup;
using OngekiFumenEditor.Avalonia.Parser;
using System.Globalization;
using System.Reflection;
using System.Xml.Linq;
using OngekiFumenEditor.Avalonia.Modules.OgkiFumenListBrowser.ViewModels;
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
        fixture.Write("musicsource/MusicSource.xml", MusicSourceXml(19, "music019.wav"), Encoding.Unicode);
        fixture.Write("musicsource/music019.wav", "audio");

        using var root = await fixture.LoadRootAsync();
        var result = await new OgkiFumenListBrowserScanner([".wav"]).ScanAsync(root);

        var set = Assert.Single(result);
        Assert.Equal("Utf16 Song", set.Title);
        Assert.Equal("musicsource/music019.wav", set.AudioLocator);
    }

    [global::Avalonia.Headless.XUnit.AvaloniaFact]
    public async Task ScanAsync_AcbWithSameNameAwb_UsesSiblingAwbWithoutInspectingAcbContent()
    {
        using var fixture = new Fixture();
        fixture.Write("music/Music.xml", MusicXml(20, 21, "ACB Song", "Artist", "2", "0", "chart.ogkr"));
        fixture.Write("music/chart.ogkr", "chart");
        fixture.Write("musicsource/MusicSource.xml", MusicSourceXml(21, "music021.acb"));
        fixture.Write("musicsource/music021.acb", "this is not an ACB package");
        fixture.Write("musicsource/music021.awb", "awb");

        using var root = await fixture.LoadRootAsync();
        var result = await new OgkiFumenListBrowserScanner([".acb"]).ScanAsync(root);

        var set = Assert.Single(result);
        Assert.Equal("musicsource/music021.acb", set.AudioLocator);
        Assert.Equal("musicsource/music021.awb", set.AudioAwbLocator);
        Assert.NotNull(set.AudioAwbFile);
    }

    [global::Avalonia.Headless.XUnit.AvaloniaFact]
    public async Task ScanAsync_AcbWithoutSameNameAwb_RetainsAudioForEmbeddedWaveforms()
    {
        using var fixture = new Fixture();
        fixture.Write("music/Music.xml", MusicXml(22, 23, "Missing AWB", "Artist", "2", "0", "chart.ogkr"));
        fixture.Write("music/chart.ogkr", "chart");
        fixture.Write("musicsource/MusicSource.xml", MusicSourceXml(23, "music023.acb"));
        fixture.Write("musicsource/music023.acb", "this is not an ACB package");

        using var root = await fixture.LoadRootAsync();
        var set = Assert.Single(await new OgkiFumenListBrowserScanner([".acb"]).ScanAsync(root));
        Assert.Equal("musicsource/music023.acb", set.AudioLocator);
        Assert.NotNull(set.AudioFile);
        Assert.Null(set.AudioAwbFile);
        Assert.Null(set.AudioAwbLocator);
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
        fixture.Write("musicsource/MusicSource.xml", MusicSourceXml(2, "music001.wav"));
        fixture.Write("musicsource/music001.wav", "audio");

        using var root = await fixture.LoadRootAsync();
        var result = await new OgkiFumenListBrowserScanner(["wav"]).ScanAsync(root);

        Assert.Empty(result);
    }

    [global::Avalonia.Headless.XUnit.AvaloniaTheory]
    [InlineData("missing")]
    [InlineData("unsupported.txt")]
    public async Task ScanAsync_MissingOrUnsupportedAudio_RetainsChartWithoutAudio(string audioName)
    {
        using var fixture = new Fixture();
        fixture.Write("music/Music.xml", MusicXml(1, 2, "Song", "Artist", "1", "0", "chart.ogkr"));
        fixture.Write("music/chart.ogkr", "chart");
        fixture.Write("musicsource/MusicSource.xml", MusicSourceXml(2, audioName));
        if (audioName != "missing")
            fixture.Write($"musicsource/{audioName}", "audio");

        using var root = await fixture.LoadRootAsync();
        var set = Assert.Single(await new OgkiFumenListBrowserScanner([".wav"]).ScanAsync(root));
        Assert.Equal("music/chart.ogkr", Assert.Single(set.Difficults).FumenLocator);
        Assert.Null(set.AudioFile);
        Assert.Null(set.AudioLocator);
    }

    [global::Avalonia.Headless.XUnit.AvaloniaFact]
    public async Task ScanAsync_DuplicateMusicId_IsContinuouslyDeduplicatedAfterSort()
    {
        using var fixture = new Fixture();
        fixture.Write("music/a/Music.xml", MusicXml(5, 1, "First", "Artist", "1", "0", "chart.ogkr"));
        fixture.Write("music/a/chart.ogkr", "chart");
        fixture.Write("music/b/Music.xml", MusicXml(5, 1, "Second", "Artist", "1", "0", "chart.ogkr"));
        fixture.Write("music/b/chart.ogkr", "chart");
        fixture.Write("musicsource/MusicSource.xml", MusicSourceXml(1, "music005.wav"));
        fixture.Write("musicsource/music005.wav", "audio");

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

    [global::Avalonia.Headless.XUnit.AvaloniaFact]
    public async Task ScanAsync_EmptyDifficultySlot_PreservesIndexAndFirstHeaderValues()
    {
        using var fixture = new Fixture();
        var xml = XElement.Parse(MusicXml(1, 2, "Song", "Artist", "12", "50", "chart.ogkr"));
        xml.Element("FumenData")!.AddFirst(new XElement("FumenData"));
        fixture.Write("music/Music.xml", xml.ToString());
        fixture.Write("music/chart.ogkr", "BPM_DEF 0\nBPM_DEF 180\nCREATOR Maker  \n");

        using var root = await fixture.LoadRootAsync();
        var diff = Assert.Single(Assert.Single(await new OgkiFumenListBrowserScanner([]).ScanAsync(root)).Difficults);
        Assert.Equal("Advanced", diff.DiffName);
        Assert.Equal(0, diff.Bpm);
        Assert.Equal("Maker  ", diff.Creator);
    }

    [global::Avalonia.Headless.XUnit.AvaloniaFact]
    public async Task ScanAsync_OnlyUsesMetadataInNamedSubtreesAndDirectSmallJackets()
    {
        using var fixture = new Fixture();
        fixture.Write("backup/Music.xml", MusicXml(1, 2, "Backup", "Artist", "1", "0", "chart.ogkr"));
        fixture.Write("backup/chart.ogkr", "chart");
        fixture.Write("backup/MusicSource.xml", MusicSourceXml(2, "wrong.wav"));
        fixture.Write("backup/wrong.wav", "audio");
        fixture.Write("DATA/MUSIC/pack/Music.xml", MusicXml(1, 2, "Song", "Artist", "1", "0", "chart.ogkr"));
        fixture.Write("DATA/MUSIC/pack/chart.ogkr", "chart");
        fixture.Write("DATA/MUSICSOURCE/pack/MusicSource.xml", MusicSourceXml(2, "right.wav"));
        fixture.Write("DATA/MUSICSOURCE/pack/right.wav", "audio");
        fixture.Write("a/assets/ui_jacket_0001_small", "wrong jacket");
        fixture.Write("a/assets/ui_jacket_0001_s_bad", "wrong jacket");
        fixture.Write("a/assets/nested/ui_jacket_0001_s", "wrong jacket");
        fixture.Write("a/assets/ui_jacket_s", "malformed jacket");
        fixture.Write("z/assets/ui_jacket_0001_s", "right jacket");

        using var root = await fixture.LoadRootAsync();
        var set = Assert.Single(await new OgkiFumenListBrowserScanner([".wav"]).ScanAsync(root));
        Assert.Equal("Song", set.Title);
        Assert.Equal("DATA/MUSICSOURCE/pack/right.wav", set.AudioLocator);
        Assert.Equal("z/assets/ui_jacket_0001_s", set.JacketLocator);
    }

    [global::Avalonia.Headless.XUnit.AvaloniaFact]
    public async Task ScanAsync_SelectedMusicRoot_UsesProviderNameWithoutChangingRelativeLocators()
    {
        using var fixture = new Fixture();
        fixture.Write("Music.xml", MusicXml(1, 2, "Song", "Artist", "1", "0", "chart.ogkr"));
        fixture.Write("chart.ogkr", "chart");
        using var root = await fixture.LoadRootAsync();

        var set = Assert.Single(await new OgkiFumenListBrowserScanner([]).ScanAsync(root, rootDirectoryName: "Music"));
        Assert.Equal("chart.ogkr", Assert.Single(set.Difficults).FumenLocator);
        Assert.Null(set.AudioFile);
    }

    [global::Avalonia.Headless.XUnit.AvaloniaFact]
    public async Task ApplyKeywords_MatchesArtistTitleCreatorAndOrdersByWpfDistanceThreshold()
    {
        using var fixture = new Fixture();
        fixture.WriteSong(1, "abcdefghij", "zzzzzzzzzz", (180, null));
        fixture.WriteSong(2, "abcdefghik", "zzzzzzzzzz", (180, null));
        fixture.WriteSong(3, "abcdefzzzz", "zzzzzzzzzz", (180, null));
        fixture.WriteSong(4, "abcdezzzzz", "zzzzzzzzzz", (180, null));
        fixture.WriteSong(5, "pppppppppp", "ABCDEFGHIJ extended", (180, null));
        fixture.WriteSong(6, "pppppppppp", "zzzzzzzzzz", (180, "ABCDEFGHIJ"));
        fixture.WriteSong(7, "pppppppppp", "", (180, null));
        fixture.WriteSong(8, "qqqqqqqqqq", "zzzzzzzzzz", (180, null));
        fixture.WriteSong(9, "abcdef", "zzzzzzzzzz", (180, null));
        using var browser = await fixture.CreateBrowserAsync();

        browser.Keywords = "AbCdEfGhIj";
        browser.ApplyKeywordsCommand.Execute(null);
        Assert.Equal(new[] { 1, 5, 6, 7, 9, 2, 3 }, browser.DisplayFumenSets.Select(set => set.MusicId));

        browser.Keywords = "     abcdefghij     ";
        browser.ApplyKeywordsCommand.Execute(null);
        Assert.DoesNotContain(browser.DisplayFumenSets, set => set.MusicId is 2 or 3);
        Assert.Contains(browser.DisplayFumenSets, set => set.MusicId == 1);
    }

    [global::Avalonia.Headless.XUnit.AvaloniaFact]
    public async Task ApplyKeywords_BpmBoundsUseOneDifficultyAndCurrentCulture()
    {
        using var fixture = new Fixture();
        fixture.WriteSong(1, "Song A", "Artist", (120, "Maker"), (180.5f, "Maker"));
        fixture.WriteSong(2, "Song B", "Artist", (160, "Maker"), (200, "Maker"));
        fixture.WriteSong(3, "qqqqqqqqqq", "zzzzzzzzzz", (180.5f, "rrrrrrrrrr"));
        using var browser = await fixture.CreateBrowserAsync();
        var previousCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            browser.Keywords = "Song";
            browser.BpmMin = "180,5";
            browser.BpmMax = "180,5";
            browser.ApplyKeywordsCommand.Execute(null);
            Assert.Equal(1, Assert.Single(browser.DisplayFumenSets).MusicId);

            browser.BpmMin = "not a number";
            browser.BpmMax = "160";
            browser.ApplyKeywordsCommand.Execute(null);
            Assert.Equal(new[] { 1, 2 }, browser.DisplayFumenSets.Select(set => set.MusicId));

            browser.BpmMin = "201";
            browser.BpmMax = "120";
            browser.ApplyKeywordsCommand.Execute(null);
            Assert.Empty(browser.DisplayFumenSets);
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
        }
    }

    [global::Avalonia.Headless.XUnit.AvaloniaFact]
    public async Task Loaded_WithoutBookmark_DoesNotPresentStaleRootAsSelected()
    {
        var setting = OngekiFumenEditor.Avalonia.Modules.OgkiFumenListBrowser.Models.Settings.OgkiFumenListBrowserSetting.Default;
        var previousBookmark = setting.RootFolderBookmark;
        var previousDisplayName = setting.RootFolderDisplayName;
        try
        {
            setting.RootFolderBookmark = string.Empty;
            setting.RootFolderDisplayName = "unavailable game root";
            using var browser = CreateBrowser();
            var view = new OngekiFumenEditor.Avalonia.Modules.OgkiFumenListBrowser.Views.OgkiFumenListBrowserView();
            browser.OnViewAfterLoaded(view);
            await browser.RefreshAsync();

            Assert.True(browser.ShowNoRootState);
            Assert.False(browser.HasRootFolder);
            Assert.Empty(browser.DisplayFumenSets);
        }
        finally
        {
            setting.RootFolderBookmark = previousBookmark;
            setting.RootFolderDisplayName = previousDisplayName;
        }
    }

    [global::Avalonia.Headless.XUnit.AvaloniaFact]
    public async Task RefreshAsync_DeletedRoot_ClearsStaleResultsAndBusyState()
    {
        using var fixture = new Fixture();
        fixture.WriteSong(1, "Song", "Artist", (180, null));
        using var browser = await fixture.CreateBrowserAsync();
        Assert.Equal(1, Assert.Single(browser.DisplayFumenSets).MusicId);

        Directory.Delete(fixture.RootPath, recursive: true);
        await browser.RefreshAsync();

        Assert.Empty(browser.DisplayFumenSets);
        Assert.False(browser.IsBusy);
        Assert.True(browser.ShowErrorState);
    }

    [global::Avalonia.Headless.XUnit.AvaloniaFact]
    public async Task RefreshAsync_ChangedLibrary_UsesCurrentEntriesAndMetadata()
    {
        using var fixture = new Fixture();
        fixture.WriteSong(1, "Original", "Artist", (180, null));
        fixture.WriteSong(2, "Removed", "Artist", (160, null));
        using var browser = await fixture.CreateBrowserAsync();
        Assert.Equal(new[] { 1, 2 }, browser.DisplayFumenSets.Select(set => set.MusicId));

        fixture.WriteSong(1, "Updated", "Artist", (240, "New creator"));
        Directory.Delete(Path.Combine(fixture.RootPath, "music", "2"), recursive: true);
        fixture.WriteSong(3, "Added", "Artist", (120, null));
        await browser.RefreshAsync();

        Assert.Equal(new[] { 1, 3 }, browser.DisplayFumenSets.Select(set => set.MusicId));
        var updated = browser.DisplayFumenSets[0];
        Assert.Equal("Updated", updated.Title);
        var chart = Assert.Single(updated.Difficults);
        Assert.Equal(240, chart.Bpm);
        Assert.Equal("New creator", chart.Creator);
        Assert.False(browser.IsBusy);
    }

    [global::Avalonia.Headless.XUnit.AvaloniaFact]
    public async Task LoadFumen_MissingAudio_NotifiesUser()
    {
        using var fixture = new Fixture();
        fixture.WriteSong(1, "Song", "Artist", (180, null));
        using var browser = await fixture.CreateBrowserAsync();
        var diff = Assert.Single(Assert.Single(browser.DisplayFumenSets).Difficults);
        var dialogs = ProgrammableDialogManager.Instance;
        var previousCount = dialogs.MessageDialogCount;

        await browser.LoadFumenCommand.ExecuteAsync(diff);

        Assert.Equal(previousCount + 1, dialogs.MessageDialogCount);
        Assert.False(browser.IsBusy);
    }

    private static OgkiFumenListBrowserViewModel CreateBrowser() => new(
        new MetadataOnlyAudioManager(),
        IoC.Get<IFumenParserManager>(),
        new UnusedEditorProvider(),
        IoC.Get<IShell>(),
        IoC.Get<IDialogManager>(),
        IoC.Get<IEditorRecentFilesManager>(),
        IoC.Get<IOgkiFumenListBrowserJacketDecoder>());

    private sealed class UnusedEditorProvider : FumenVisualEditorProviderBase
    {
        public override bool CanCreateNew => false;
        protected override IEditorProjectSetupFilePicker CreateSetupFilePicker() => throw new NotSupportedException();
        protected override Task<EditorFileAccessContext> RestoreContextAsync(
            EditorFileAccessContextSnapshot snapshot, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class MetadataOnlyAudioManager : IAudioManager
    {
        private static readonly (string fileExt, string extDesc)[] Extensions = [(".wav", "Wave"), (".acb", "ACB")];
        public bool EnableVarspeed => false;
        public float SoundVolume { get; set; }
        public float MusicVolume { get; set; }
        public float MusicSpeed { get; set; }
        public IEnumerable<(string fileExt, string extDesc)> SupportAudioFileExtensionList => Extensions;
        public Task<ISoundPlayer> LoadSoundAsync(Stream stream) => throw new NotSupportedException();
        public Task<IAudioPlayer> LoadAudioAsync(Stream audioFileStream) => throw new NotSupportedException();
        public Task<IAudioPlayer> LoadAudioAsync(Stream acbStream, Stream externalAwbStream) => throw new NotSupportedException();
        public void Dispose() { }
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

        public void WriteSong(int musicId, string title, string artist, params (float Bpm, string? Creator)[] charts)
        {
            var xml = XElement.Parse(MusicXml(musicId, musicId, title, artist, "1", "0", "chart.ogkr"));
            var difficulties = xml.Element("FumenData")!;
            var template = new XElement(difficulties.Element("FumenData")!);
            difficulties.RemoveNodes();
            for (var index = 0; index < charts.Length; index++)
            {
                var entry = new XElement(template);
                entry.Element("FumenFile")!.Element("path")!.Value = $"chart{index}.ogkr";
                difficulties.Add(entry);
                var creator = charts[index].Creator is { } value ? $"CREATOR {value}\n" : string.Empty;
                Write($"music/{musicId}/chart{index}.ogkr", $"BPM_DEF {charts[index].Bpm.ToString(CultureInfo.InvariantCulture)}\n{creator}");
            }
            Write($"music/{musicId}/Music.xml", xml.ToString());
        }

        public async Task<OgkiFumenListBrowserViewModel> CreateBrowserAsync()
        {
            var window = new global::Avalonia.Controls.Window();
            var folder = await window.StorageProvider.TryGetFolderFromPathAsync(new Uri(RootPath))
                ?? throw new InvalidOperationException("Unable to create storage folder.");
            var browser = CreateBrowser();
            var setRoot = typeof(OgkiFumenListBrowserViewModel).GetMethod(
                "SetRootFromStorageFolderAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
            await (Task)setRoot.Invoke(browser, [folder, false])!;
            return browser;
        }

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
