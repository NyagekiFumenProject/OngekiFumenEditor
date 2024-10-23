using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Kernel.ProgramUpdater
{
    public class VersionInfo
    {
        [JsonPropertyName("branch")]
        public string Branch { get; set; }

        [JsonPropertyName("time")]
        public DateTime Time { get; set; }

        [JsonPropertyName("version")]
        public Version Version { get; set; }

        [JsonPropertyName("fileSize")]
        public int FileSize { get; set; }
    }
}
