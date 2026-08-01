using Injectio.Attributes;
using NAudio.Wave;
using NAudio.Wave.Browser;
using OngekiFumenEditor.Avalonia.Kernel.Audio;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Avalonia.Browser.Platforms.Services.Audio;

[RegisterSingleton<INAudioWavePlayerFactory>]
internal sealed class BrowserNAudioWavePlayerFactory : INAudioWavePlayerFactory
{
    public Task<IWavePlayer> CreateDefaultWavePlayer()
        => Task.FromResult<IWavePlayer>(
            new BrowserAudioWorkletPlayer(BrowserAudioLatencyProfile.Interactive));
}
