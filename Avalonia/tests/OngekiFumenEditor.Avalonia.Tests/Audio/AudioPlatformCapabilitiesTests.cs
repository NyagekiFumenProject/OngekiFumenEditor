using Avalonia.Headless.XUnit;
using OngekiFumenEditor.Avalonia.Kernel.Audio;
using Microsoft.Extensions.Logging.Abstractions;
using OngekiFumenEditor.Avalonia.Kernel.SettingPages.Audio.ViewModels;
using OngekiFumenEditor.Avalonia.Models.Settings;
using Xunit;

namespace OngekiFumenEditor.Avalonia.Tests.Audio;

public sealed class AudioPlatformCapabilitiesTests
{
    [Fact]
    public void WindowsNativeAot_ExposesWasapiOnly_AndFallsBackFromAsioExplicitly()
    {
        var capabilities = new AudioPlatformCapabilities(
            AudioPlatformProfile.WindowsNativeAot,
            supportsVarspeed: true);

        var resolution = capabilities.ResolveOutput(AudioOutputType.Asio);

        Assert.Equal([AudioOutputType.Wasapi], capabilities.SelectableOutputTypes);
        Assert.False(capabilities.CanSelectOutputBackend);
        Assert.True(capabilities.SupportsVarspeed);
        Assert.Equal(AudioBackendKind.Wasapi, resolution.EffectiveBackend);
        Assert.Equal(AudioOutputFallbackReason.UnsupportedBackend, resolution.FallbackReason);
        Assert.True(resolution.IsFallback);
    }

    [Fact]
    public void WindowsJit_ExposesWasapiAndAsio_AndKeepsAsioRequest()
    {
        var capabilities = new AudioPlatformCapabilities(
            AudioPlatformProfile.WindowsJit,
            supportsVarspeed: true);

        var resolution = capabilities.ResolveOutput(AudioOutputType.Asio);

        Assert.Equal(
            [AudioOutputType.Wasapi, AudioOutputType.Asio],
            capabilities.SelectableOutputTypes);
        Assert.True(capabilities.CanSelectOutputBackend);
        Assert.Equal(AudioBackendKind.Asio, resolution.EffectiveBackend);
        Assert.Equal(AudioOutputFallbackReason.None, resolution.FallbackReason);
        Assert.False(resolution.IsFallback);
    }

    [Fact]
    public void WindowsJit_LegacyWaveOut_FallsBackToWasapiWithLegacyReason()
    {
        var capabilities = new AudioPlatformCapabilities(
            AudioPlatformProfile.WindowsJit,
            supportsVarspeed: true);

        var resolution = capabilities.ResolveOutput(AudioOutputType.WaveOut);

        Assert.Equal(AudioBackendKind.Wasapi, resolution.EffectiveBackend);
        Assert.Equal(AudioOutputFallbackReason.LegacyWaveOut, resolution.FallbackReason);
        Assert.Equal("WASAPI", resolution.EffectiveBackendName);
    }

    [Fact]
    public void Browser_ExposesOnlyRealWorkletAndDecodeCapabilities()
    {
        var capabilities = new AudioPlatformCapabilities(
            AudioPlatformProfile.Browser,
            supportsVarspeed: true);

        var resolution = capabilities.ResolveOutput(AudioOutputType.Asio);

        Assert.Empty(capabilities.SelectableOutputTypes);
        Assert.False(capabilities.CanSelectOutputBackend);
        Assert.False(capabilities.SupportsVarspeed);
        Assert.Equal(AudioBackendKind.BrowserAudioWorklet, capabilities.DefaultBackend);
        Assert.Equal(AudioBackendKind.BrowserAudioWorklet, resolution.EffectiveBackend);
        Assert.True(resolution.IsFallback);
        Assert.Equal(AudioOutputFallbackReason.UnsupportedBackend, resolution.FallbackReason);
        Assert.Equal([".wav", ".aif", ".aiff"], capabilities.SupportedAudioFileExtensions);
    }

    [Fact]
    public void WindowsProfiles_AdvertiseDesktopReaderExtensions()
    {
        var capabilities = new AudioPlatformCapabilities(
            AudioPlatformProfile.WindowsNativeAot,
            supportsVarspeed: false);

        Assert.Equal(
            [".mp3", ".wav", ".aif", ".aiff", ".acb"],
            capabilities.SupportedAudioFileExtensions);
        Assert.False(capabilities.SupportsVarspeed);
    }

    [Fact]
    public void WindowsNativeAot_ApplySettings_PreservesStoredJitAsioPreference()
    {
        var capabilities = new AudioPlatformCapabilities(
            AudioPlatformProfile.WindowsNativeAot,
            supportsVarspeed: true);
        var setting = new AudioSetting { AudioOutputType = (int)AudioOutputType.Asio };
        var saveCount = 0;
        var viewModel = new AudioSettingViewModel(
            NullLogger<AudioSettingViewModel>.Instance,
            capabilities,
            setting,
            new AudioPlayerToolViewerSetting(),
            () => saveCount++);

        Assert.False(viewModel.CanSelectAudioOutput);
        Assert.True(viewModel.HasFixedAudioOutput);
        Assert.True(viewModel.HasAudioOutputFallback);
        Assert.Equal(AudioOutputType.Wasapi, viewModel.SelectedAudioOutputType);

        viewModel.ApplyChanges();

        Assert.Equal((int)AudioOutputType.Asio, setting.AudioOutputType);
        Assert.Equal(1, saveCount);
    }

    [AvaloniaFact]
    public void WindowsJit_ApplySettings_PersistsUserSelectedBackend()
    {
        var capabilities = new AudioPlatformCapabilities(
            AudioPlatformProfile.WindowsJit,
            supportsVarspeed: true);
        var setting = new AudioSetting { AudioOutputType = (int)AudioOutputType.Asio };
        var saveCount = 0;
        var viewModel = new AudioSettingViewModel(
            NullLogger<AudioSettingViewModel>.Instance,
            capabilities,
            setting,
            new AudioPlayerToolViewerSetting(),
            () => saveCount++);

        Assert.True(viewModel.CanSelectAudioOutput);
        Assert.False(viewModel.HasAudioOutputFallback);

        viewModel.SelectedAudioOutputType = AudioOutputType.Wasapi;
        viewModel.ApplyChanges();

        Assert.Equal((int)AudioOutputType.Wasapi, setting.AudioOutputType);
        Assert.Equal(1, saveCount);
    }

    [Fact]
    public void Browser_FixedWorklet_ReportsAndPreservesStoredWindowsBackendRequest()
    {
        var capabilities = new AudioPlatformCapabilities(
            AudioPlatformProfile.Browser,
            supportsVarspeed: false);
        var setting = new AudioSetting { AudioOutputType = (int)AudioOutputType.Asio };
        var viewModel = new AudioSettingViewModel(
            NullLogger<AudioSettingViewModel>.Instance,
            capabilities,
            setting,
            new AudioPlayerToolViewerSetting(),
            saveSettings: static () => { });

        Assert.False(viewModel.CanSelectAudioOutput);
        Assert.True(viewModel.HasFixedAudioOutput);
        Assert.True(viewModel.HasAudioOutputFallback);

        viewModel.ApplyChanges();

        Assert.Equal((int)AudioOutputType.Asio, setting.AudioOutputType);
    }
}
