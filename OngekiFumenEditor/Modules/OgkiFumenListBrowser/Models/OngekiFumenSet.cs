using OngekiFumenEditor.Utils;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;
using System.Xml.XPath;
using Xv2CoreLib.Resource;

namespace OngekiFumenEditor.Modules.OgkiFumenListBrowser.Models
{
    public class OngekiFumenSet
    {
        public int MusicId { get; init; }

        public List<OngekiFumenDiff> Difficults { get; } = new List<OngekiFumenDiff>();
        public string Title { get; init; }
        public string Artist { get; init; }
        public string Genre { get; init; }

        public int MusicSourceId { get; init; }

        public string AudioFilePath { get; set; }
        public string JacketFilePath { get; set; }

        public override string ToString() => $"[{MusicId}] {Artist} - {Title}";

        XDocument musicXml;

        public OngekiFumenSet(string musicXmlFilePath, XDocument musicXml)
        {
            this.musicXml = musicXml;

            Title = GetString("Name");
            Artist = GetString("ArtistName");
            Genre = GetString("Genre");
            MusicId = GetId("Name");
            MusicSourceId = GetId("MusicSourceName");

            var folderPath = Path.GetDirectoryName(musicXmlFilePath);

            foreach ((var fumenDataElement, var idx) in musicXml.XPathSelectElements("/MusicData/FumenData/FumenData").WithIndex())
            {
                string fumenConstIntegerPart = fumenDataElement.Element("FumenConstIntegerPart").Value;
                string fumenConstFractionalPart = fumenDataElement.Element("FumenConstFractionalPart").Value;
                string fumenFileName = fumenDataElement.Element("FumenFile").Element("path")?.Value;

                var fumenFilePath = Path.Combine(folderPath, fumenFileName);
                if (!File.Exists(fumenFilePath))
                    continue;
                var fumenDiff = new OngekiFumenDiff(this);
                fumenDiff.DiffIdx = idx;
                fumenDiff.FilePath = fumenFilePath;
                fumenDiff.Level = (int.TryParse(fumenConstIntegerPart, out var d1) ? d1 : 0) + ((int.TryParse(fumenConstFractionalPart, out var d2) ? d2 : 0) / 100.0f);

                ParseFumenFileInfo(fumenDiff);

                Difficults.Add(fumenDiff);
            }
        }

        public string GetString(string name)
        {
            return GetPathValue<string>(name, "str");
        }

        public int GetId(string name)
        {
            return GetPathValue<int>(name, "id");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T GetPathValue<T>(params string[] names)
        {
            var expr = $"//{string.Join("/", names.Select(x => $"{x}[1]"))}";
            var element = musicXml.XPathSelectElement(expr);
            if (element?.Value is string strValue)
            {
                var obj = TypeDescriptor.GetConverter(typeof(T)).ConvertFromString(strValue);
                if (obj is T t)
                    return t;
            }
            return default;
        }

        private static Regex BpmRegex = new Regex(@"BPM_DEF\s*([\d\.]+)");
        private static Regex CreatorRegex = new Regex(@"CREATOR\s*(.+)");

        private void ParseFumenFileInfo(OngekiFumenDiff fumenDiff)
        {
            try
            {
                using var fs = File.OpenRead(fumenDiff.FilePath);
                using var reader = new StreamReader(fs);

                var isBpmSetup = false;
                var isCreatorSetup = false;

                while (!reader.EndOfStream)
                {
                    var line = reader.ReadLine();

                    if (!isBpmSetup)
                    {
                        var match = BpmRegex.Match(line);
                        if (match.Success)
                        {
                            var bpm = float.Parse(match.Groups[1].Value);
                            isBpmSetup = true;

                            fumenDiff.Bpm = bpm;
                        }
                    }

                    if (!isCreatorSetup)
                    {
                        var match = CreatorRegex.Match(line);
                        if (match.Success)
                        {
                            var creator = match.Groups[1].Value;
                            isCreatorSetup = true;

                            fumenDiff.Creator = creator;
                        }
                    }

                    if (isBpmSetup && isCreatorSetup)
                        break;
                }
            }
            catch (Exception e)
            {
                //todo
            }
        }
    }
}
