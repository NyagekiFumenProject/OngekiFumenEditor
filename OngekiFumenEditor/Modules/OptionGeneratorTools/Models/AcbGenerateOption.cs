using Caliburn.Micro;
using OngekiFumenEditor.Kernel.CommandExecutor.Attributes;
using OngekiFumenEditor.Properties;

namespace OngekiFumenEditor.Modules.OptionGeneratorTools.Models
{
    public class AcbGenerateOption : PropertyChangedBase
    {
        private int musicId = -1;
        [LocalizableOptionBinding<int>("musicId", nameof(Resources.ProgramOptionMusicId), -1, true)]
        public int MusicId
        {
            get => musicId;
            set => Set(ref musicId, value);
        }

        private string inputAudioFilePath;
        [LocalizableOptionBinding<string>("inputFile", nameof(Resources.ProgramOptionInputFileAudio), "", true)]
        public string InputAudioFilePath
        {
            get => inputAudioFilePath;
            set => Set(ref inputAudioFilePath, value);
        }

        private string outputFolderPath;
        [LocalizableOptionBinding<string>("outputFolder", nameof(Resources.ProgramOptionOutputFolder), "", true)]
        public string OutputFolderPath
        {
            get => outputFolderPath;
            set => Set(ref outputFolderPath, value);
        }

        private int previewBeginTime = 60000;
        [LocalizableOptionBinding<int>("previewBegin", nameof(Resources.ProgramOptionPreviewBegin), 60000)]
        public int PreviewBeginTime
        {
            get => previewBeginTime;
            set
            {
                Set(ref previewBeginTime, value);
            }
        }

        private int previewEndTime = 80000;
        [LocalizableOptionBinding<int>("previewEnd", nameof(Resources.ProgramOptionPreviewEnd), 80000)]
        public int PreviewEndTime
        {
            get => previewEndTime;
            set
            {
                Set(ref previewEndTime, value);
            }
        }
    }
}
