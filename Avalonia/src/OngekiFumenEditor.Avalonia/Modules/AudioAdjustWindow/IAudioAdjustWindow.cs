using Gekimini.Avalonia.Framework;
using OngekiFumenEditor.Avalonia.Utils.SimpleFileSystem;
using System;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Avalonia.Modules.AudioAdjustWindow
{
    public interface IAudioAdjustWindow
    {
        Task<(bool isSuccess, string msg)> OffsetAudioFile(ISimpleFile inputWavFile, ISimpleFile outputWavFile, TimeSpan offset);
    }
}


