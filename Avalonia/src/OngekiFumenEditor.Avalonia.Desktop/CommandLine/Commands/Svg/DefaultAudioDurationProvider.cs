using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Kernel.Audio;

namespace OngekiFumenEditor.Avalonia.Desktop.CommandLine.Commands.Svg;

[RegisterSingleton<IAudioDurationProvider>]
internal sealed class DefaultAudioDurationProvider : IAudioDurationProvider
{
    private readonly INAudioFileReaderFactory audioFileReaderFactory;

    public DefaultAudioDurationProvider(INAudioFileReaderFactory audioFileReaderFactory)
    {
        this.audioFileReaderFactory = audioFileReaderFactory;
    }

    public Task<TimeSpan> GetDurationAsync(string audioFilePath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var reader = audioFileReaderFactory.CreateAudioFileReader(audioFilePath);
        return Task.FromResult(reader.TotalTime);
    }
}
