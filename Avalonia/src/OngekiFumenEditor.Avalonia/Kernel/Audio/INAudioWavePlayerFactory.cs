using NAudio.Wave;

namespace OngekiFumenEditor.Avalonia.Kernel.Audio;

/// <summary>
/// Creates the platform-specific NAudio output device used by the shared audio backend.
/// </summary>
public interface INAudioWavePlayerFactory
{
    Task<IWavePlayer> CreateDefaultWavePlayer();
}
