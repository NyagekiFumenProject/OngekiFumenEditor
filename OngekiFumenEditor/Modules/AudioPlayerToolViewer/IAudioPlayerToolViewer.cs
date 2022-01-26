using Gemini.Framework;
using OngekiFumenEditor.Base;
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
        public FumenVisualEditorViewModel Editor { get; set; }
        void RequestPlayOrPause();
    }
}
