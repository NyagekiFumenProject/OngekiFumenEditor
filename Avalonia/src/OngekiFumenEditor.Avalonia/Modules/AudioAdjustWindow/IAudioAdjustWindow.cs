using Gekimini.Avalonia.Framework;
using System;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Avalonia.Modules.AudioAdjustWindow
{
    public interface IAudioAdjustWindow
    {
        Task<(bool isSuccess, string msg)> OffsetAudioFile(string inputWavFilePath, string saveWavFilePath, TimeSpan offset);
    }
}


