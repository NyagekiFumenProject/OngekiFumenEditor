using Avalonia.Headless.XUnit;
using OngekiFumenEditor.Avalonia.Base;
using OngekiFumenEditor.Avalonia.Kernel.Audio;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Models;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Setup;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.ViewModels.Dialogs;
using OngekiFumenEditor.Avalonia.Parser;
using OngekiFumenEditor.Avalonia.Utils.SimpleFileSystem;
using Xunit;

namespace OngekiFumenEditor.Avalonia.Tests.Modules.FumenVisualEditor;

public sealed class EditorProjectSetupValidationTests
{
    [Theory]
    [InlineData(null, PortableEntryNameError.Empty)]
    [InlineData("", PortableEntryNameError.Empty)]
    [InlineData(" ", PortableEntryNameError.Empty)]
    [InlineData(".", PortableEntryNameError.DotSegment)]
    [InlineData("..", PortableEntryNameError.DotSegment)]
    [InlineData("dir/file.ogkr", PortableEntryNameError.RootedOrMultiSegment)]
    [InlineData("dir\\file.ogkr", PortableEntryNameError.RootedOrMultiSegment)]
    [InlineData(" bad.ogkr", PortableEntryNameError.LeadingOrTrailingWhitespace)]
    [InlineData("bad.ogkr ", PortableEntryNameError.LeadingOrTrailingWhitespace)]
    [InlineData("bad.", PortableEntryNameError.TrailingPeriod)]
    [InlineData("bad:name.ogkr", PortableEntryNameError.InvalidCharacter)]
    [InlineData("CON.ogkr", PortableEntryNameError.ReservedDeviceName)]
    [InlineData("com1.txt", PortableEntryNameError.ReservedDeviceName)]
    [InlineData("LPT9.nyagekiProj", PortableEntryNameError.ReservedDeviceName)]
    public void PortableName_InvalidCasesReturnStableError(
        string? name,
        PortableEntryNameError expected)
    {
        Assert.Equal(expected, PortableEntryNameValidator.Validate(name).Error);
    }

    [Theory]
    [InlineData("Song 01.nyagekiProj")]
    [InlineData("Song.v2.ogkr")]
    [InlineData("初音ミク.ogkr")]
    [InlineData("e\u0301.ogkr")]
    [InlineData("😀.ogkr")]
    public void PortableName_ValidUnicodeAndInternalSeparatorsArePreserved(string name)
    {
        Assert.True(PortableEntryNameValidator.Validate(name).IsValid);
    }

    [Fact]
    public void PortableName_EnforcesUtf16AndUtf8Boundaries()
    {
        Assert.True(PortableEntryNameValidator.Validate(new string('a', 255)).IsValid);
        Assert.Equal(
            PortableEntryNameError.TooLongUtf16,
            PortableEntryNameValidator.Validate(new string('a', 256)).Error);
        Assert.True(PortableEntryNameValidator.Validate(new string('界', 85)).IsValid);
        Assert.Equal(
            PortableEntryNameError.TooLongUtf8,
            PortableEntryNameValidator.Validate(new string('界', 86)).Error);
    }

    [Theory]
    [InlineData("120", true)]
    [InlineData("0.00001", true)]
    [InlineData("10000.123456", true)]
    [InlineData("", false)]
    [InlineData("0", false)]
    [InlineData("-1", false)]
    [InlineData("NaN", false)]
    [InlineData("Infinity", false)]
    [InlineData("-Infinity", false)]
    public void InitialBpm_OnlyAcceptsPositiveFiniteValues(string text, bool expected)
    {
        Assert.Equal(expected, EditorProjectSetupValidation.TryParseBpm(text, out _));
    }

    [Fact]
    public void CreateBlankFumen_SetsFiveBpmValuesAndKeepsProgJudgeDefault()
    {
        const double bpm = 173.25;
        var expectedProgJudge = new OngekiFumen().MetaInfo.ProgJudgeBpm;

        var fumen = EditorProjectSetupValidation.CreateBlankFumen(bpm);

        Assert.Equal(bpm, fumen.MetaInfo.BpmDefinition.First);
        Assert.Equal(bpm, fumen.MetaInfo.BpmDefinition.Common);
        Assert.Equal(bpm, fumen.MetaInfo.BpmDefinition.Minimum);
        Assert.Equal(bpm, fumen.MetaInfo.BpmDefinition.Maximum);
        Assert.Equal(bpm, fumen.BpmList.FirstBpm);
        Assert.Equal(expectedProgJudge, fumen.MetaInfo.ProgJudgeBpm);
    }

    [AvaloniaFact]
    public async Task OgkrRoundTrip_PreservesUnequalMinimumAndMaximum()
    {
        var parserManager = IoC.Get<IFumenParserManager>();
        var serializer = Assert.IsAssignableFrom<IFumenSerializable>(
            parserManager.GetSerializer("chart.ogkr"));
        var deserializer = Assert.IsAssignableFrom<IFumenDeserializable>(
            parserManager.GetDeserializer("chart.ogkr"));
        var source = new OngekiFumen();
        source.MetaInfo.BpmDefinition.First = 120;
        source.MetaInfo.BpmDefinition.Common = 150;
        source.MetaInfo.BpmDefinition.Minimum = 90;
        source.MetaInfo.BpmDefinition.Maximum = 240;

        var bytes = await serializer.SerializeAsync(source);
        await using var stream = new MemoryStream(bytes, writable: false);
        var restored = await deserializer.DeserializeAsync(stream);

        Assert.Equal(120, restored.MetaInfo.BpmDefinition.First);
        Assert.Equal(150, restored.MetaInfo.BpmDefinition.Common);
        Assert.Equal(90, restored.MetaInfo.BpmDefinition.Minimum);
        Assert.Equal(240, restored.MetaInfo.BpmDefinition.Maximum);
    }

    [Fact]
    public void FormatOptions_RejectAmbiguousSerializerExtensions()
    {
        var parser = new StubParserManager(
            ("First", ".ogkr"),
            ("Second", ".OGKR"));

        var exception = Assert.Throws<InvalidDataException>(() =>
            EditorProjectSetupValidation.GetFumenFormatOptions(parser));

        Assert.Contains(".ogkr", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ViewModel_DefaultsAndNameOriginsFollowConfirmedSetupRules()
    {
        using var root = new MemoryDirectory("Song Folder");
        using var directorySelection = new EditorProjectDirectorySelection(root, "Song Folder");
        using var session = new EditorProjectSetupSession(directorySelection, new NullPicker());
        using var audioManager = new NullAudioManager();
        var parser = new StubParserManager(("OGKR", ".ogkr"), ("Nyageki", ".nyageki"));
        var coordinator = new EditorProjectCreationCoordinator(
            parser,
            audioManager,
            (_, _) => Task.FromResult(true));
        using var viewModel = new EditorProjectSetupDialogViewModel(
            session,
            parser,
            audioManager,
            coordinator,
            _ => Task.CompletedTask);

        Assert.Equal("Song Folder", viewModel.ProjectName);
        Assert.Equal("Song Folder", viewModel.NewFumenStem);
        Assert.Equal(".ogkr", viewModel.SelectedFumenFormat?.Extension);
        Assert.Equal("240", viewModel.BaseBpmText);
        Assert.False(viewModel.CanCreate);

        viewModel.ProjectName = "Renamed";
        Assert.Equal("Renamed", viewModel.NewFumenStem);

        viewModel.NewFumenStem = "master";
        viewModel.ProjectName = "Renamed Again";
        Assert.Equal("master", viewModel.NewFumenStem);
    }

    [Fact]
    public void ViewModel_PreservesInvalidDirectorySuggestionAndRejectsManagedExtensions()
    {
        using var root = new MemoryDirectory("root");
        using var directorySelection = new EditorProjectDirectorySelection(root, " Bad ");
        using var session = new EditorProjectSetupSession(directorySelection, new NullPicker());
        using var audioManager = new NullAudioManager();
        var parser = new StubParserManager(("OGKR", ".ogkr"));
        var coordinator = new EditorProjectCreationCoordinator(
            parser,
            audioManager,
            (_, _) => Task.FromResult(true));
        using var viewModel = new EditorProjectSetupDialogViewModel(
            session,
            parser,
            audioManager,
            coordinator,
            _ => Task.CompletedTask);

        Assert.Equal(" Bad ", viewModel.ProjectName);
        Assert.Contains("invalid", viewModel.ValidationMessage, StringComparison.OrdinalIgnoreCase);

        viewModel.ProjectName = "Song.nyagekiProj";
        Assert.Contains("without", viewModel.ValidationMessage, StringComparison.OrdinalIgnoreCase);

        viewModel.ProjectName = "Song";
        session.SetAudioFile(new MemoryFile("audio.wav"));
        viewModel.TargetAudioFileName = "audio.wav";
        viewModel.NewFumenStem = "master.ogkr";
        Assert.Contains("choose the format", viewModel.ValidationMessage, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class StubParserManager(
        params (string Description, string Extension)[] formats) : IFumenParserManager
    {
        private readonly StubFumenCodec codec = new();

        public IFumenSerializable? GetSerializer(string saveFilePath) =>
            formats.Any(format => saveFilePath.EndsWith(
                format.Extension,
                StringComparison.OrdinalIgnoreCase))
                ? codec
                : null;

        public IFumenDeserializable? GetDeserializer(string loadFilePath) =>
            formats.Any(format => loadFilePath.EndsWith(
                format.Extension,
                StringComparison.OrdinalIgnoreCase))
                ? codec
                : null;

        public IEnumerable<(string desc, string[] fileFormat)> GetSerializerDescriptions() =>
            formats.Select(format => (format.Description, new[] { format.Extension }));

        public IEnumerable<(string desc, string[] fileFormat)> GetDeserializerDescriptions() =>
            GetSerializerDescriptions();
    }

    private sealed class StubFumenCodec : IFumenSerializable, IFumenDeserializable
    {
        public string FileFormatName => "Stub";
        public string[] SupportFumenFileExtensions => [".ogkr", ".nyageki"];
        public Task<byte[]> SerializeAsync(OngekiFumen fumen) => Task.FromResult(Array.Empty<byte>());
        public Task<OngekiFumen> DeserializeAsync(Stream stream) => Task.FromResult(new OngekiFumen());
    }

    private sealed class NullPicker : IEditorProjectSetupFilePicker
    {
        public Task<EditorProjectDirectorySelection?> PickProjectDirectoryAsync(
            CancellationToken cancellationToken = default) => Task.FromResult<EditorProjectDirectorySelection?>(null);

        public Task<ISimpleFile?> PickAudioAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<ISimpleFile?>(null);

        public Task<ISimpleFile?> PickExistingFumenAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<ISimpleFile?>(null);

        public Task<ISimpleFile?> PickExternalAwbAsync(
            string expectedFileName,
            CancellationToken cancellationToken = default) => Task.FromResult<ISimpleFile?>(null);
    }

    private sealed class NullAudioManager : IAudioManager
    {
        public bool EnableVarspeed => false;
        public float SoundVolume { get; set; }
        public float MusicVolume { get; set; }
        public float MusicSpeed { get; set; }
        public IEnumerable<(string fileExt, string extDesc)> SupportAudioFileExtensionList => [];
        public Task<ISoundPlayer> LoadSoundAsync(Stream stream) => throw new NotSupportedException();
        public Task<IAudioPlayer> LoadAudioAsync(Stream stream) =>
            throw new NotSupportedException();
        public Task<IAudioPlayer> LoadAudioAsync(Stream acbStream, Stream externalAwbStream) =>
            throw new NotSupportedException();
        public void Dispose()
        {
        }
    }

    private sealed class MemoryDirectory(string name) : ISimpleDirectory
    {
        public ISimpleDirectory? ParentDictionary => null;
        public ISimpleDirectory[] ChildDictionaries => [];
        public ISimpleFile[] ChildFiles => [];
        public string FullPath => $"memory://{name}";
        public string? LocalPath => null;
        public string DirectoryName => name;
        public bool ExistsDirectory(string dirName) => false;
        public bool ExistsFile(string fileName) => false;
        public ISimpleFile[] GetFiles(string pattern = "*") => [];
        public Task<ISimpleDirectory> GetOrCreateDirectoryAsync(
            string directoryName,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ISimpleFile> CreateFileAsync(
            string fileName,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public void Dispose()
        {
        }
    }

    private sealed class MemoryFile(string fileName) : ISimpleFile
    {
        public ISimpleDirectory? ParentDictionary => null;
        public string FullPath => $"memory://{fileName}";
        public string? LocalPath => null;
        public string FileName => fileName;
        public long FileLength => 0;
        public ValueTask<string[]> ReadAllLines() => ValueTask.FromResult(Array.Empty<string>());
        public ValueTask<byte[]> ReadAllBytes() => ValueTask.FromResult(Array.Empty<byte>());
        public Task<Stream> OpenRead() => Task.FromResult<Stream>(new MemoryStream());
        public Task<Stream> OpenWrite() => throw new NotSupportedException();
        public void Dispose()
        {
        }
    }
}
