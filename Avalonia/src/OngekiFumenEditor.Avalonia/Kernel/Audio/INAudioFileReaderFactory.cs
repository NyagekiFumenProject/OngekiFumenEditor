using NAudio.Wave;

namespace OngekiFumenEditor.Avalonia.Kernel.Audio;

public interface INAudioFileReaderFactory
{
    IReadOnlyList<(string fileExt, string extDesc)> SupportAudioFileExtensionList { get; }

    WaveStream CreateAudioFileReader(string filePath);
}
