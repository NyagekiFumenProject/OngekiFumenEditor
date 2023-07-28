using OngekiFumenEditor.Modules.OptionGeneratorTools.Models.EnumStructs;
using OngekiFumenEditor.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Xml.Linq;
using System.Xml.XPath;

namespace OngekiFumenEditor.Modules.OptionGeneratorTools.Kernel
{
    public class EnumFetchManager
    {
        private Dictionary<int, BossCard> bossCards = new();
        public IReadOnlyCollection<BossCard> BossCards => bossCards.Values;

        private Dictionary<int, Genre> genres = new();
        public IReadOnlyCollection<Genre> Genres => genres.Values;

        private Dictionary<int, MusicRight> musicRights = new();
        public IReadOnlyCollection<MusicRight> MusicRights => musicRights.Values;

        private Dictionary<int, Stage> stages = new();
        public IReadOnlyCollection<Stage> Stages => stages.Values;

        private Dictionary<int, VersionID> versionIDs = new();
        public IReadOnlyCollection<VersionID> VersionIDs => versionIDs.Values;


        public Task<bool> Init(string gameFolder)
        {

        }

        private async Task ProcessMusicXml(string musicXmlFilePath)
        {
            using var fs = File.OpenRead(musicXmlFilePath);
            var musicXml = await XDocument.LoadAsync(fs, LoadOptions.None, default);

            (int id, string str) GetString(string name, string strKey = "str", string idKey = "id")
            {
                var str = musicXml.XPathSelectElement($@"//{name}[1]/{strKey}[1]").Value;
                var id = int.Parse(musicXml.XPathSelectElement($@"//{name}[1]/{idKey}[1]").Value);

                return (id, str);
            }

            var musicRightName = GetString("MusicRightsName");
            var right = new MusicRight(musicRightName.str, musicRightName.id);
            Add(right);


            var genreTuple = GetString("Genre");
            var genre = new Genre(genreTuple.str, genreTuple.id);
            Add(genre);

            var genreTuple = GetString("Genre");
            var genre = new Genre(genreTuple.str, genreTuple.id);
            Add(genre);
        }

        public bool Add(MusicRight right)
        {
            lock (musicRights)
            {
                if (!musicRights.ContainsKey(right.Id))
                {
                    musicRights.Add(right.Id, right);
                    return true;
                }
                return false;
            }
        }

        public bool Add(Genre genre)
        {
            lock (genres)
            {
                if (!genres.ContainsKey(genre.Id))
                {
                    genres.Add(genre.Id, genre);
                    return true;
                }
                return false;
            }
        }
    }
}
