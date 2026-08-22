using OngekiFumenEditor.Avalonia.Desktop.Modules.FumenVisualEditor.FastOpen;
using OngekiFumenEditor.Avalonia.Utils;
using OngekiFumenEditor.Avalonia.Utils.Logs;
using Xunit;

namespace OngekiFumenEditor.Avalonia.Desktop.Tests.Modules.FumenVisualEditor.FastOpen;

public sealed class DesktopFastOpenAudioResolverTests
{
    private readonly string root;
    private static readonly string[] AudioExts = [".ogg", ".wav", ".mp3", ".acb"];

    public DesktopFastOpenAudioResolverTests()
    {
        // Resolver 内部使用静态 Log；普通单测没有 Avalonia 应用，注入空输出实例。
        Log.Initialize(new Log(Array.Empty<ILogOutput>()));
        root = Path.Combine(Path.GetTempPath(), "fastOpenTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(root, recursive: true);
        }
        catch
        {
            // ignored
        }
    }

    private string CreateStandardLayout(int musicId, string audioExt = ".ogg", string? musicXmlContent = null)
    {
        var idStr = DesktopFastOpenAudioResolverTests_FormatMusicId(musicId);
        // 标准布局：谱面目录上两级才是 musicsource 的父目录。
        var chartDir = Path.Combine(root, "data", "option", $"pack{idStr}");
        Directory.CreateDirectory(chartDir);
        var chartPath = Path.Combine(chartDir, $"{idStr}_001.ogkr");
        File.WriteAllText(chartPath, "fumen");

        var sourceDir = Path.Combine(root, "data", "musicsource", $"musicsource{idStr}");
        Directory.CreateDirectory(sourceDir);
        if (audioExt is not null)
            File.WriteAllText(Path.Combine(sourceDir, $"music{idStr}{audioExt}"), "audio");

        if (musicXmlContent is not null)
            File.WriteAllText(Path.Combine(chartDir, "Music.xml"), musicXmlContent);

        return chartPath;
    }

    // 与被测逻辑无关的本地四位格式化，避免直接依赖内部方法语义。
    private static string DesktopFastOpenAudioResolverTests_FormatMusicId(int musicId)
    {
        var raw = musicId.ToString();
        return musicId < 1000 ? new string('0', 4 - raw.Length) + raw : raw;
    }

    private static string MusicXmlWithId(int id)
    {
        return $@"<?xml version=""1.0"" encoding=""utf-8""?>
<MusicData>
  <MusicSourceName><id>{id}</id></MusicSourceName>
  <Name><str>Test Song</str></Name>
</MusicData>";
    }

    [Fact]
    public async Task MusicXmlValidId_FindsAudio()
    {
        var chartPath = CreateStandardLayout(1, musicXmlContent: MusicXmlWithId(1));

        var audio = await DesktopFastOpenAudioResolver.TryResolveAudioFilePathAsync(chartPath, AudioExts);

        Assert.NotNull(audio);
        Assert.EndsWith("music0001.ogg", audio);
    }

    [Fact]
    public async Task MusicXmlMissing_FallsBackToFileName()
    {
        var chartPath = CreateStandardLayout(42);

        var audio = await DesktopFastOpenAudioResolver.TryResolveAudioFilePathAsync(chartPath, AudioExts);

        Assert.NotNull(audio);
        Assert.EndsWith("music0042.ogg", audio);
    }

    [Fact]
    public async Task MusicXmlInvalidId_FallsBackToFileName()
    {
        var invalidXml = @"<?xml version=""1.0"" encoding=""utf-8""?>
<MusicData>
  <MusicSourceName><id>not-a-number</id></MusicSourceName>
</MusicData>";
        var chartPath = CreateStandardLayout(7, musicXmlContent: invalidXml);

        var audio = await DesktopFastOpenAudioResolver.TryResolveAudioFilePathAsync(chartPath, AudioExts);

        Assert.NotNull(audio);
        Assert.EndsWith("music0007.ogg", audio);
    }

    [Theory]
    [InlineData(1, "0001")]
    [InlineData(999, "0999")]
    [InlineData(1000, "1000")]
    [InlineData(1234, "1234")]
    public async Task MusicId_IsPaddedToFourDigits(int id, string expectedIdStr)
    {
        var chartPath = CreateStandardLayout(id, musicXmlContent: MusicXmlWithId(id));

        var audio = await DesktopFastOpenAudioResolver.TryResolveAudioFilePathAsync(chartPath, AudioExts);

        Assert.NotNull(audio);
        Assert.EndsWith($"music{expectedIdStr}.ogg", audio);
    }

    [Fact]
    public async Task MissingStandardFolder_FindsViaConstrainedPackageScan()
    {
        // 标准上两级位置不放 musicsource，改放到 package 树内其他位置，验证受限递归。
        var idStr = "0123";
        var chartDir = Path.Combine(root, "package", "music", "0123", "music");
        Directory.CreateDirectory(chartDir);
        var chartPath = Path.Combine(chartDir, "0123_001.ogkr");
        File.WriteAllText(chartPath, "fumen");
        File.WriteAllText(Path.Combine(chartDir, "Music.xml"), MusicXmlWithId(123));

        var hiddenSource = Path.Combine(root, "package", "option", "data", "musicsource0123");
        Directory.CreateDirectory(hiddenSource);
        File.WriteAllText(Path.Combine(hiddenSource, "music0123.wav"), "audio");

        var audio = await DesktopFastOpenAudioResolver.TryResolveAudioFilePathAsync(chartPath, AudioExts);

        Assert.NotNull(audio);
        Assert.EndsWith("music0123.wav", audio);
    }

    [Fact]
    public async Task ExtensionMatching_IsCaseInsensitive()
    {
        var chartPath = CreateStandardLayout(5, audioExt: ".OGG", musicXmlContent: MusicXmlWithId(5));

        var audio = await DesktopFastOpenAudioResolver.TryResolveAudioFilePathAsync(chartPath, AudioExts);

        Assert.NotNull(audio);
        Assert.EndsWith("music0005.OGG", audio);
    }

    [Fact]
    public async Task UnsupportedAudioExtension_ReturnsNull()
    {
        var chartPath = CreateStandardLayout(9, audioExt: ".txt", musicXmlContent: MusicXmlWithId(9));

        var audio = await DesktopFastOpenAudioResolver.TryResolveAudioFilePathAsync(chartPath, AudioExts);

        Assert.Null(audio);
    }

    [Fact]
    public async Task NoAudioAnywhere_ReturnsNull()
    {
        var chartPath = CreateStandardLayout(11, audioExt: null, musicXmlContent: MusicXmlWithId(11));

        var audio = await DesktopFastOpenAudioResolver.TryResolveAudioFilePathAsync(chartPath, AudioExts);

        Assert.Null(audio);
    }

    [Fact]
    public async Task NoMusicIdResolvable_ReturnsNull()
    {
        // Music.xml 无 id 且文件名不含 (\d+)_\d+。
        var chartDir = Path.Combine(root, "nomusicid");
        Directory.CreateDirectory(chartDir);
        var chartPath = Path.Combine(chartDir, "freestyle.ogkr");
        File.WriteAllText(chartPath, "fumen");
        File.WriteAllText(
            Path.Combine(chartDir, "Music.xml"),
            @"<?xml version=""1.0""?><MusicData><MusicSourceName><id></id></MusicSourceName></MusicData>");

        var audio = await DesktopFastOpenAudioResolver.TryResolveAudioFilePathAsync(chartPath, AudioExts);

        Assert.Null(audio);
    }
}
