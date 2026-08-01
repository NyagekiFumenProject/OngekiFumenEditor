using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Kernel.Audio;

namespace OngekiFumenEditor.Avalonia.Browser.Platforms.Services.Audio;

[RegisterSingleton<IAudioPlatformCapabilities>]
internal sealed class BrowserAudioPlatformCapabilities : AudioPlatformCapabilities
{
    public BrowserAudioPlatformCapabilities()
        : base(AudioPlatformProfile.Browser, supportsVarspeed: false)
    {
    }
}
