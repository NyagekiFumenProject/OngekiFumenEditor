using System;
using System.Threading;
using System.Threading.Tasks;
using OngekiFumenEditor.Avalonia.Utils.SimpleFileSystem;

namespace OngekiFumenEditor.Avalonia.Kernel.Audio;

public interface IWavAudioOffsetService
{
    Task OffsetAsync(
        ISimpleFile inputWavFile,
        ISimpleFile outputWavFile,
        TimeSpan offset,
        CancellationToken cancellationToken = default);
}
