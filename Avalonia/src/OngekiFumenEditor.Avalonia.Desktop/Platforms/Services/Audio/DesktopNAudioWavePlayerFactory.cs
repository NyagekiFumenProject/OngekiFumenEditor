using Injectio.Attributes;
using NAudio.Wave;
using OngekiFumenEditor.Avalonia.Kernel.Audio;
using OngekiFumenEditor.Avalonia.Models.Settings;
using OngekiFumenEditor.Avalonia.Utils;
using System;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Avalonia.Desktop.Platforms.Services.Audio;

[RegisterSingleton<INAudioWavePlayerFactory>]
internal sealed class DesktopNAudioWavePlayerFactory : INAudioWavePlayerFactory
{
    private readonly IAudioPlatformCapabilities platformCapabilities;

    public DesktopNAudioWavePlayerFactory(IAudioPlatformCapabilities platformCapabilities)
    {
        this.platformCapabilities = platformCapabilities;
    }

    public async Task<IWavePlayer> CreateDefaultWavePlayer()
    {
        var outputType = (AudioOutputType)AudioSetting.Default.AudioOutputType;
        var resolution = platformCapabilities.ResolveOutput(outputType);
        if (resolution.IsFallback)
        {
            Log.LogWarning(
                $"Requested audio backend {resolution.RequestedOutput} is unavailable for " +
                $"{platformCapabilities.Profile}; using {resolution.EffectiveBackend}.");
        }

        return resolution.EffectiveBackend switch
        {
            AudioBackendKind.Asio => await CreateAsioPlayer(),
            AudioBackendKind.Wasapi => await CreateWasapiPlayer(),
            _ => throw new PlatformNotSupportedException(
                $"No desktop audio output is available for {platformCapabilities.Profile}.")
        };
    }

    private static async Task<IWavePlayer> CreateWasapiPlayer()
        => await new WasapiPlayerBuilder()
            .WithSharedMode()
            .WithEventSync()
            .WithLatency(20)
            .WithLowLatency()
            .WithMmcssThreadPriority()
            .BuildAsync();

    private static Task<IWavePlayer> CreateAsioPlayer()
    {
#if NATIVE_AOT
        throw new PlatformNotSupportedException(
            "ASIO is not included in the Native-AOT distribution. Use the JIT/ASIO distribution instead.");
#else
        return Task.FromResult<IWavePlayer>(new AsioOut { AutoStop = false });
#endif
    }
}
