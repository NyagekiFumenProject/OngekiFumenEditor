namespace OngekiFumenEditor.Avalonia.Kernel.Audio;

public interface IAudioPlatformCapabilities
{
    AudioPlatformProfile Profile { get; }

    IReadOnlyList<AudioOutputType> SelectableOutputTypes { get; }

    IReadOnlyList<string> SupportedAudioFileExtensions { get; }

    bool CanSelectOutputBackend { get; }

    bool SupportsVarspeed { get; }

    AudioBackendKind DefaultBackend { get; }

    AudioOutputResolution ResolveOutput(AudioOutputType requestedOutput);
}
