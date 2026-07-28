using System;
using System.IO;
using System.Threading.Tasks;
using Gekimini.Avalonia.Modules.Window.ViewModels;
using Injectio.Attributes;

namespace OngekiFumenEditor.Avalonia.Modules.AudioAdjustWindow.ViewModels;

[RegisterSingleton<IAudioAdjustWindow>]
public class AudioAdjustWindowViewModel : WindowViewModelBase, IAudioAdjustWindow
{
    public AudioAdjustWindowViewModel() : base()
    {

    }

    public Task<(bool isSuccess, string msg)> OffsetAudioFile(string inputWavFilePath, string saveWavFilePath, TimeSpan offset)
    {
        return Task.Run<(bool isSuccess, string msg)>(() =>
        {
            try
            {
                if (!File.Exists(inputWavFilePath))
                    return (false, $"Input audio file not found: {inputWavFilePath}");

                var outputDirectory = Path.GetDirectoryName(saveWavFilePath);
                if (!string.IsNullOrWhiteSpace(outputDirectory))
                    Directory.CreateDirectory(outputDirectory);

                // Temporary migration fallback: keep original bytes when no offset is requested.
                if (offset == TimeSpan.Zero)
                {
                    File.Copy(inputWavFilePath, saveWavFilePath, overwrite: true);
                    return (true, string.Empty);
                }

                return (false, "Audio offset is not implemented in Avalonia migration yet.");
            }
            catch (Exception e)
            {
                return (false, e.Message);
            }
        });
    }
}
