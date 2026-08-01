using Injectio.Attributes;
using NAudio.Wave;
using OngekiFumenEditor.Avalonia.Kernel.Audio;
using OngekiFumenEditor.Avalonia.Models.Settings;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Avalonia.Desktop.Platforms.Services.Audio;

[RegisterSingleton<INAudioWavePlayerFactory>]
internal sealed class DesktopNAudioWavePlayerFactory : INAudioWavePlayerFactory
{
    public async Task<IWavePlayer> CreateDefaultWavePlayer()
    {
        var outputType = (AudioOutputType)AudioSetting.Default.AudioOutputType;
        return outputType switch
        {
            AudioOutputType.Asio => await CreateAsioPlayer(),
            AudioOutputType.Wasapi => await CreateWasapiPlayer(),
            // WaveOut is kept as a persisted legacy value. WASAPI is the supported desktop fallback.
            AudioOutputType.WaveOut or _ => await CreateWasapiPlayer()
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
        // NAudio.Asio still uses runtime-generated delegates. Keep Native-AOT builds usable by
        // falling back to the source-generated-COM WASAPI backend.
        return CreateWasapiPlayer();
#else
        return Task.FromResult<IWavePlayer>(new AsioOut { AutoStop = false });
#endif
    }
}
