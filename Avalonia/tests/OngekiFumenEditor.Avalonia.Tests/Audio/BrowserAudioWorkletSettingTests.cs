using NAudio.Wave.Browser;
using OngekiFumenEditor.Avalonia.Browser.Platforms.Services.Audio;
using Xunit;

namespace OngekiFumenEditor.Avalonia.Tests.Audio;

public sealed class BrowserAudioWorkletSettingTests
{
    [Fact]
    public void NewSettingUsesDeclaredDefaults()
    {
        var setting = new BrowserAudioWorkletSetting();

        Assert.Equal(BrowserAudioWorkletSetting.DefaultBufferDurationMilliseconds,
            setting.BufferDurationMilliseconds);
        Assert.Equal(BrowserAudioWorkletSetting.DefaultInitialBufferFrameCount,
            setting.InitialBufferFrameCount);
        Assert.Equal(BrowserAudioWorkletSetting.DefaultUseDeviceSampleRate,
            setting.UseDeviceSampleRate);
    }

    [Fact]
    public void ResetRestoresAllDeclaredDefaults()
    {
        var setting = new BrowserAudioWorkletSetting
        {
            BufferDurationMilliseconds = 375,
            InitialBufferFrameCount = 2048,
            UseDeviceSampleRate = false,
        };

        setting.Reset();

        Assert.Equal(BrowserAudioWorkletSetting.DefaultBufferDurationMilliseconds,
            setting.BufferDurationMilliseconds);
        Assert.Equal(BrowserAudioWorkletSetting.DefaultInitialBufferFrameCount,
            setting.InitialBufferFrameCount);
        Assert.Equal(BrowserAudioWorkletSetting.DefaultUseDeviceSampleRate,
            setting.UseDeviceSampleRate);
    }

    [Fact]
    public void RulesPreserveValidBoundaryValues()
    {
        var setting = new BrowserAudioWorkletSetting
        {
            BufferDurationMilliseconds = BrowserAudioWorkletSetting.MinBufferDurationMilliseconds,
            InitialBufferFrameCount = BrowserAudioWorkletSetting.MaxInitialBufferFrameCount,
            UseDeviceSampleRate = false,
        };

        BrowserAudioWorkletSettingRules.Normalize(setting, out var changed);

        Assert.False(changed);
        Assert.Equal(BrowserAudioWorkletSetting.MinBufferDurationMilliseconds,
            setting.BufferDurationMilliseconds);
        Assert.Equal(BrowserAudioWorkletSetting.MaxInitialBufferFrameCount,
            setting.InitialBufferFrameCount);
        Assert.False(setting.UseDeviceSampleRate);
    }

    [Fact]
    public void RulesNormalizeOutOfRangeFieldsIndividually()
    {
        var setting = new BrowserAudioWorkletSetting
        {
            BufferDurationMilliseconds = 0,
            InitialBufferFrameCount = 9000,
            UseDeviceSampleRate = false,
        };

        BrowserAudioWorkletSettingRules.Normalize(setting, out var changed);

        Assert.True(changed);
        Assert.Equal(BrowserAudioWorkletSetting.DefaultBufferDurationMilliseconds,
            setting.BufferDurationMilliseconds);
        Assert.Equal(BrowserAudioWorkletSetting.DefaultInitialBufferFrameCount,
            setting.InitialBufferFrameCount);
        Assert.False(setting.UseDeviceSampleRate);
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
