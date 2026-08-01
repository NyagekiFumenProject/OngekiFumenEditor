using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Kernel.Audio;
using System;
using System.Runtime.InteropServices;

namespace OngekiFumenEditor.Avalonia.Desktop.Platforms.Services.Audio;

[RegisterSingleton<IAudioPlatformCapabilities>]
internal sealed class DesktopAudioPlatformCapabilities : AudioPlatformCapabilities
{
    public DesktopAudioPlatformCapabilities()
        : base(
#if NATIVE_AOT
            AudioPlatformProfile.WindowsNativeAot,
#else
            AudioPlatformProfile.WindowsJit,
#endif
            OperatingSystem.IsWindows() && RuntimeInformation.ProcessArchitecture == Architecture.X64)
    {
    }
}
