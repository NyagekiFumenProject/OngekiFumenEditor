using System;

namespace OngekiFumenEditor.Modules.OngekiGamePlayControllerViewer.Base
{
    public struct NotesManagerData
    {
        public TimeSpan PlayEndTime { get; set; }
        public TimeSpan NoteEndTime { get; set; }
        public TimeSpan PlayStartTime { get; set; }
        public TimeSpan NoteStartTime { get; set; }
        public TimeSpan VisibleTime { get; set; }
        public TimeSpan InvisibleTime { get; set; }
        public TimeSpan CurrentTime { get; set; }

        public float PlayProgress { get; set; }
        public bool IsPlaying { get; set; }
        public bool IsPlayEnd { get; set; }

        public string OgkrFilePath { get; set; }

        public bool IsAutoPlay { get; set; }
        public bool IsPauseIfMissBellOrDamaged { get; set; }
    }
}
