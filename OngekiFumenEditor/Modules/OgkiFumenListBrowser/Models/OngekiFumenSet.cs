using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace OngekiFumenEditor.Modules.OgkiFumenListBrowser.Models
{
    public class OngekiFumenSet
    {
        public int MusicId { get; set; }

        public List<OngekiFumenDiff> Difficults { get; } = new List<OngekiFumenDiff>();
        public string Title { get; set; }
        public string Artist { get; set; }
        public string Genre { get; set; }

        public int MusicSourceId { get; set; }

        public string AudioFilePath { get; set; }
        public string JacketFilePath { get; set; }

        public override string ToString() => $"[{MusicId}] {Artist} - {Title}";
    }
}
