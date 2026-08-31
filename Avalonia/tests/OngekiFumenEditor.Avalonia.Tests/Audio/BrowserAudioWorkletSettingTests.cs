using NAudio.Wave.Browser;
using OngekiFumenEditor.Avalonia.Browser.Platforms.Services.Audio;
using Xunit;

namespace OngekiFumenEditor.Avalonia.Tests.Audio;

public sealed class BrowserAudioWorkletSettingTests
{
    [Fact]
    public void MissingStorageReturnsDefaultsWithoutWriteBack()
    {
        var writes = 0;
        var store = new BrowserAudioWorkletSettingStore(
            () => string.Empty,
            _ => writes++);

        var setting = store.Load();

        Assert.Equal(BrowserAudioWorkletSetting.DefaultBufferDurationMilliseconds,
            setting.BufferDurationMilliseconds);
        Assert.Equal(BrowserAudioWorkletSetting.DefaultInitialBufferFrameCount,
            setting.InitialBufferFrameCount);
        Assert.True(setting.UseDeviceSampleRate);
        Assert.Equal(0, writes);
    }

    [Fact]
    public void ValidBoundaryValuesArePreserved()
    {
        var writes = 0;
        var store = new BrowserAudioWorkletSettingStore(
            () => "{\"BufferDurationMilliseconds\":20,\"InitialBufferFrameCount\":8192,\"UseDeviceSampleRate\":false}",
            _ => writes++);

        var setting = store.Load();

        Assert.Equal(20, setting.BufferDurationMilliseconds);
        Assert.Equal(8192, setting.InitialBufferFrameCount);
        Assert.False(setting.UseDeviceSampleRate);
        Assert.Equal(0, writes);
    }

    [Fact]
    public void OutOfRangeFieldsRevertIndividuallyAndWriteOnce()
    {
        string persisted = "{\"BufferDurationMilliseconds\":0,\"InitialBufferFrameCount\":9000,\"UseDeviceSampleRate\":false}";
        var writes = new List<string>();
        var store = new BrowserAudioWorkletSettingStore(
            () => persisted,
            value => writes.Add(value));

        var setting = store.Load();

        Assert.Equal(BrowserAudioWorkletSetting.DefaultBufferDurationMilliseconds,
            setting.BufferDurationMilliseconds);
        Assert.Equal(BrowserAudioWorkletSetting.DefaultInitialBufferFrameCount,
            setting.InitialBufferFrameCount);
        Assert.False(setting.UseDeviceSampleRate);
        Assert.Single(writes);
        Assert.Contains("\"BufferDurationMilliseconds\":20", writes[0], StringComparison.Ordinal);
        Assert.Contains("\"InitialBufferFrameCount\":512", writes[0], StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("{")]
    [InlineData("{\"BufferDurationMilliseconds\":\"slow\"}")]
    [InlineData("null")]
    public void MalformedOrTypeMismatchedStorageResetsWholeGroup(string persisted)
    {
        var writes = new List<string>();
        var store = new BrowserAudioWorkletSettingStore(
            () => persisted,
            value => writes.Add(value));

        var setting = store.Load();

        Assert.Equal(BrowserAudioWorkletSetting.DefaultBufferDurationMilliseconds,
            setting.BufferDurationMilliseconds);
        Assert.Equal(BrowserAudioWorkletSetting.DefaultInitialBufferFrameCount,
            setting.InitialBufferFrameCount);
        Assert.True(setting.UseDeviceSampleRate);
        Assert.Single(writes);
    }

    [Fact]
    public void WriteFailureDoesNotPreventUsingNormalizedValues()
    {
        var store = new BrowserAudioWorkletSettingStore(
            () => "{\"BufferDurationMilliseconds\":-1}",
            _ => throw new InvalidOperationException("storage unavailable"));

        BrowserAudioWorkletSetting setting = null;
        var exception = Record.Exception(() => setting = store.Load());

        Assert.Null(exception);
        Assert.NotNull(setting);
        Assert.Equal(BrowserAudioWorkletSetting.DefaultBufferDurationMilliseconds,
            setting.BufferDurationMilliseconds);
    }

    [Fact]
    public void RulesMapValuesToExplicitNaudioOptions()
    {
        var setting = new BrowserAudioWorkletSetting
        {
            BufferDurationMilliseconds = 375,
            InitialBufferFrameCount = 2048,
            UseDeviceSampleRate = false,
        };

        BrowserAudioWorkletOptions options = BrowserAudioWorkletSettingRules.ToOptions(setting);

        Assert.Equal(375, options.BufferDurationMilliseconds);
        Assert.Equal(2048, options.InitialBufferFrameCount);
        Assert.False(options.UseDeviceSampleRate);
    }
}
