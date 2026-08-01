namespace OngekiFumenEditor.Avalonia.Kernel.Audio;

public enum AudioPlatformProfile
{
    Unknown,
    WindowsNativeAot,
    WindowsJit,
    Browser
}

public enum AudioBackendKind
{
    None,
    Wasapi,
    Asio,
    BrowserAudioWorklet
}

public enum AudioOutputFallbackReason
{
    None,
    LegacyWaveOut,
    UnsupportedBackend,
    NoOutputBackend
}

public readonly record struct AudioOutputResolution(
    AudioOutputType RequestedOutput,
    AudioBackendKind EffectiveBackend,
    AudioOutputFallbackReason FallbackReason)
{
    public bool IsFallback => FallbackReason is not AudioOutputFallbackReason.None;

    public string EffectiveBackendName => EffectiveBackend switch
    {
        AudioBackendKind.Wasapi => "WASAPI",
        AudioBackendKind.Asio => "ASIO",
        AudioBackendKind.BrowserAudioWorklet => "Browser AudioWorklet",
        _ => "Unavailable"
    };
}

public class AudioPlatformCapabilities : IAudioPlatformCapabilities
{
    private static readonly AudioOutputType[] WindowsAotOutputs = [AudioOutputType.Wasapi];
    private static readonly AudioOutputType[] WindowsJitOutputs =
    [
        AudioOutputType.Wasapi,
        AudioOutputType.Asio
    ];

    private static readonly string[] DesktopAudioExtensions =
    [
        ".mp3",
        ".wav",
        ".aif",
        ".aiff",
        ".acb"
    ];

    private static readonly string[] BrowserAudioExtensions =
    [
        ".wav",
        ".aif",
        ".aiff"
    ];

    public static AudioPlatformCapabilities Unknown { get; } =
        new(AudioPlatformProfile.Unknown, supportsVarspeed: false);

    public AudioPlatformProfile Profile { get; }

    public IReadOnlyList<AudioOutputType> SelectableOutputTypes { get; }

    public IReadOnlyList<string> SupportedAudioFileExtensions { get; }

    public bool CanSelectOutputBackend => SelectableOutputTypes.Count > 1;

    public bool SupportsVarspeed { get; }

    public AudioBackendKind DefaultBackend { get; }

    public AudioPlatformCapabilities(AudioPlatformProfile profile, bool supportsVarspeed)
    {
        Profile = profile;
        SupportsVarspeed = supportsVarspeed && profile is not AudioPlatformProfile.Browser;

        (SelectableOutputTypes, SupportedAudioFileExtensions, DefaultBackend) = profile switch
        {
            AudioPlatformProfile.WindowsNativeAot =>
                (WindowsAotOutputs, DesktopAudioExtensions, AudioBackendKind.Wasapi),
            AudioPlatformProfile.WindowsJit =>
                (WindowsJitOutputs, DesktopAudioExtensions, AudioBackendKind.Wasapi),
            AudioPlatformProfile.Browser =>
                (Array.Empty<AudioOutputType>(), BrowserAudioExtensions, AudioBackendKind.BrowserAudioWorklet),
            _ =>
                (Array.Empty<AudioOutputType>(), Array.Empty<string>(), AudioBackendKind.None)
        };
    }

    public AudioOutputResolution ResolveOutput(AudioOutputType requestedOutput)
    {
        if (Profile is AudioPlatformProfile.Browser)
        {
            return new AudioOutputResolution(
                requestedOutput,
                AudioBackendKind.BrowserAudioWorklet,
                AudioOutputFallbackReason.UnsupportedBackend);
        }

        if (Profile is AudioPlatformProfile.Unknown)
        {
            return new AudioOutputResolution(
                requestedOutput,
                AudioBackendKind.None,
                AudioOutputFallbackReason.NoOutputBackend);
        }

        if (requestedOutput is AudioOutputType.Asio &&
            Profile is AudioPlatformProfile.WindowsJit)
        {
            return new AudioOutputResolution(
                requestedOutput,
                AudioBackendKind.Asio,
                AudioOutputFallbackReason.None);
        }

        if (requestedOutput is AudioOutputType.Wasapi)
        {
            return new AudioOutputResolution(
                requestedOutput,
                AudioBackendKind.Wasapi,
                AudioOutputFallbackReason.None);
        }

        return new AudioOutputResolution(
            requestedOutput,
            AudioBackendKind.Wasapi,
            requestedOutput is AudioOutputType.WaveOut
                ? AudioOutputFallbackReason.LegacyWaveOut
                : AudioOutputFallbackReason.UnsupportedBackend);
    }
}
