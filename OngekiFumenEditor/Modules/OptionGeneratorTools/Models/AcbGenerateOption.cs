using Caliburn.Micro;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Modules.OptionGeneratorTools.Models
{
    public class AcbGenerateOption : PropertyChangedBase
    {
        private int musicId = -1;
        public int MusicId
        {
            get => musicId;
            set => Set(ref musicId, value);
        }

        private string inputAudioFilePath;
        public string InputAudioFilePath
        {
            get => inputAudioFilePath;
            set => Set(ref inputAudioFilePath, value);
        }

        private string outputFolderPath;
        public string OutputFolderPath
        {
            get => outputFolderPath;
            set => Set(ref outputFolderPath, value);
        }
    }
}
