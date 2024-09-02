using Gemini.Framework;
using System;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Modules.AudioAdjustWindow
{
    public interface IAudioAdjustWindow : IWindow
    {
        Task<(bool isSuccess, string msg)> OffsetAudioFile(string inputWavFilePath, string saveWavFilePath, TimeSpan offset);
    }
}
