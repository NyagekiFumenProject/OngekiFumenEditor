using Injectio.Attributes;
using NAudio.Wave;
using NAudio.Wave.Browser;
using OngekiFumenEditor.Avalonia.Kernel.Audio;
using OngekiFumenEditor.Avalonia.Models.Settings;
using OngekiFumenEditor.Avalonia.Utils;
using System;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Avalonia.Browser.Platforms.Services.Audio;

[RegisterSingleton<INAudioWavePlayerFactory>]
internal sealed class BrowserNAudioWavePlayerFactory : INAudioWavePlayerFactory
{
    private readonly IAudioPlatformCapabilities platformCapabilities;

    public BrowserNAudioWavePlayerFactory(IAudioPlatformCapabilities platformCapabilities)
    {
        this.platformCapabilities = platformCapabilities ?? throw new ArgumentNullException(nameof(platformCapabilities));
    }

    public Task<IWavePlayer> CreateDefaultWavePlayer()
    {
        var requestedOutput = (AudioOutputType)AudioSetting.Default.AudioOutputType;
        var resolution = platformCapabilities.ResolveOutput(requestedOutput);
        if (resolution.EffectiveBackend is not AudioBackendKind.BrowserAudioWorklet)
        {
            throw new PlatformNotSupportedException(
                $"The browser audio profile resolved to unsupported backend {resolution.EffectiveBackend}.");
        }

        if (resolution.IsFallback)
        {
            Log.LogWarning(
                $"Requested audio backend {resolution.RequestedOutput} is unavailable for " +
                $"{platformCapabilities.Profile}; using {resolution.EffectiveBackend}.");
        }

        var options = BrowserAudioWorkletSettingRules.ToOptions(BrowserAudioWorkletSetting.Default);
        return Task.FromResult<IWavePlayer>(new BrowserAudioWorkletPlayer(options));
    }
}
