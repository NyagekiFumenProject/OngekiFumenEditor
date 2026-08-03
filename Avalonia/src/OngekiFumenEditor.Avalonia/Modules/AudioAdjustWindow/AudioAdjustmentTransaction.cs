#nullable enable

using System;
using System.Threading.Tasks;
using OngekiFumenEditor.Avalonia.Kernel.Audio;
using OngekiFumenEditor.Avalonia.Utils.SimpleFileSystem;

namespace OngekiFumenEditor.Avalonia.Modules.AudioAdjustWindow;

internal static class AudioAdjustmentTransaction
{
    public static Task<(bool isSuccess, string msg)> ExecuteAsync(
        IWavAudioOffsetService audioOffsetService,
        string inputWavFilePath,
        string outputWavFilePath,
        TimeSpan offset,
        Action? commitOnSuccess = null)
    {
        ArgumentNullException.ThrowIfNull(audioOffsetService);
        return ExecuteCoreAsync(
            () => audioOffsetService.OffsetAsync(inputWavFilePath, outputWavFilePath, offset),
            commitOnSuccess);
    }

    public static Task<(bool isSuccess, string msg)> ExecuteAsync(
        IWavAudioOffsetService audioOffsetService,
        ISimpleFile inputWavFile,
        ISimpleFile outputWavFile,
        TimeSpan offset,
        Action? commitOnSuccess = null)
    {
        ArgumentNullException.ThrowIfNull(audioOffsetService);
        return ExecuteCoreAsync(
            () => audioOffsetService.OffsetAsync(inputWavFile, outputWavFile, offset),
            commitOnSuccess);
    }

    public static Task<(bool isSuccess, string msg)> ExecuteAsync(
        IWavAudioOffsetService audioOffsetService,
        string inputWavFilePath,
        ISimpleFile outputWavFile,
        TimeSpan offset,
        Action? commitOnSuccess = null)
    {
        ArgumentNullException.ThrowIfNull(audioOffsetService);
        return ExecuteCoreAsync(
            () => audioOffsetService.OffsetAsync(inputWavFilePath, outputWavFile, offset),
            commitOnSuccess);
    }

    private static async Task<(bool isSuccess, string msg)> ExecuteCoreAsync(
        Func<Task> offsetAudio,
        Action? commitOnSuccess)
    {
        try
        {
            await offsetAudio();
        }
        catch (Exception exception)
        {
            return (false, exception.Message);
        }

        commitOnSuccess?.Invoke();
        return (true, string.Empty);
    }
}
