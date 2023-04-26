using Gemini.Framework;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Kernel.Audio;
using OngekiFumenEditor.Modules.AudioPlayerToolViewer.Graphics;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Modules.AudioPlayerToolViewer
{
    public interface IAudioPlayerToolViewer : ITool
    {
        IAudioPlayer AudioPlayer { get; }
        float SoundVolume { get; set; }
        FumenVisualEditorViewModel Editor { get; }
        void RequestPlayOrPause();
    }
}
