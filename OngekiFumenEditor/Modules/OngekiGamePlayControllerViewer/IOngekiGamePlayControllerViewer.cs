using Gemini.Framework;
using OngekiFumenEditor.Modules.OngekiGamePlayControllerViewer.Base;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Modules.OngekiGamePlayControllerViewer
{
    public interface IOngekiGamePlayControllerViewer : ITool
    {
        Task Play();
        Task Pause();
        Task SeekTo(TimeSpan time);
        Task Reload();
        Task<bool> IsPlaying();
        Task<bool> UpdateCheckConnecting();
        Task<bool> CheckVailed();
        //Task<NotesManagerData?> GetNotesManagerData();
    }
}
