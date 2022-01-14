using Caliburn.Micro;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Models
{
    public class OngekiFumenAudioMetaInfo : PropertyChangedBase
    {
        private string title = default;
        [JsonInclude]
        public string Title
        {
            get => title;
            set => Set(ref title, value);
        }

        private string artist = default;
        [JsonInclude]
        public string Artist
        {
            get => artist;
            set => Set(ref artist, value);
        }

        private TimeSpan audioDuration = default;
        [JsonInclude]
        public TimeSpan AudioDuration
        {
            get => audioDuration;
            set => Set(ref audioDuration, value);
        }
    }
}
