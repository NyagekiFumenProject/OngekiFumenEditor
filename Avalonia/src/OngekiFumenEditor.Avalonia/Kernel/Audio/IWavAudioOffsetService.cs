using System;
using System.Threading;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Avalonia.Kernel.Audio;

public interface IWavAudioOffsetService
{
    Task OffsetAsync(
        string inputWavFilePath,
        string outputWavFilePath,
        TimeSpan offset,
        CancellationToken cancellationToken = default);
}
