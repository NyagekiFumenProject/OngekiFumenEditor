#nullable enable

using System;
using System.Threading.Tasks;
using OngekiFumenEditor.Avalonia.Kernel.Audio;

namespace OngekiFumenEditor.Avalonia.Modules.AudioAdjustWindow;

internal static class AudioAdjustmentTransaction
{
    public static async Task<(bool isSuccess, string msg)> ExecuteAsync(
        IWavAudioOffsetService audioOffsetService,
        string inputWavFilePath,
        string outputWavFilePath,
        TimeSpan offset,
        Action? commitOnSuccess = null)
    {
        ArgumentNullException.ThrowIfNull(audioOffsetService);

        try
        {
            await audioOffsetService.OffsetAsync(inputWavFilePath, outputWavFilePath, offset);
        }
        catch (Exception exception)
        {
            return (false, exception.Message);
        }

        commitOnSuccess?.Invoke();
        return (true, string.Empty);
    }
}
