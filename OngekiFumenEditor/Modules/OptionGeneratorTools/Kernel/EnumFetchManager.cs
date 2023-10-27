using OngekiFumenEditor.Modules.OptionGeneratorTools.Models.EnumStructs;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;
using System.Xml.XPath;

namespace OngekiFumenEditor.Modules.OptionGeneratorTools.Kernel
{
	public class EnumFetchManager
	{
		private Dictionary<int, BossCard> bossCards = new();
		public IReadOnlyDictionary<int, BossCard> BossCards => bossCards;

		private Dictionary<int, Genre> genres = new();
		public IReadOnlyDictionary<int, Genre> Genres => genres;

		private Dictionary<int, MusicRight> musicRights = new();
		public IReadOnlyDictionary<int, MusicRight> MusicRights => musicRights;

		private Dictionary<int, Stage> stages = new();
		public IReadOnlyDictionary<int, Stage> Stages => stages;

		private Dictionary<int, VersionID> versions = new();
		public IReadOnlyDictionary<int, VersionID> Versions => versions;

		private Regex cardNameRegex = new Regex(@"ui_card_(\d+)$", RegexOptions.Compiled | RegexOptions.Multiline);

		public async Task<bool> Init(string gameFolder)
		{
			bossCards.Clear();
			genres.Clear();
			musicRights.Clear();
			stages.Clear();
			versions.Clear();

			var cardFileMap = new Dictionary<int, string>();
			foreach (var cardFilePath in Directory.GetFiles(gameFolder, "ui_card_*", SearchOption.AllDirectories))
			{
				var match = cardNameRegex.Match(cardFilePath);
				if (!match.Success)
					continue;
				var cardId = int.Parse(match.Groups[1].Value);
				cardFileMap[cardId] = cardFilePath;
			}

			var xmlFiles = Directory.GetFiles(gameFolder, "*.xml", SearchOption.AllDirectories);

			await Parallel.ForEachAsync(xmlFiles, async (filePath, cancelToken) =>
			{
				var fileName = Path.GetFileName(filePath);
				switch (fileName.ToLower())
				{
					case "music.xml":
						await ProcessMusicXml(filePath);
						break;
					case "version.xml":
						await ProcessVersionXml(filePath);
						break;
					case "card.xml":
						await ProcessCardXml(filePath, cardFileMap);
						break;
					default:
						break;
				}
			});

			return true;
		}

		private (int id, string str) GetStringAndId(XDocument document, string name, string strKey = "str", string idKey = "id")
		{
			var str = document.XPathSelectElement($@"//{name}[1]/{strKey}[1]").Value;
			var id = int.Parse(document.XPathSelectElement($@"//{name}[1]/{idKey}[1]").Value);

			return (id, str);
		}

		private string GetStringByPath(XDocument document, params string[] fieldPaths)
		{
			var selectExpr = $"//{string.Join("/", fieldPaths.Select(x => $"{x}[1]"))}";
			var str = document.XPathSelectElement(selectExpr).Value;

			return str;
		}

		private async Task ProcessMusicXml(string musicXmlFilePath)
		{
			using var fs = File.OpenRead(musicXmlFilePath);
			var musicXml = await XDocument.LoadAsync(fs, LoadOptions.None, default);

			var musicRightName = GetStringAndId(musicXml, "MusicRightsName");
			var right = new MusicRight(musicRightName.str, musicRightName.id);
			Add(right);

			var genreTuple = GetStringAndId(musicXml, "Genre");
			var genre = new Genre(genreTuple.str, genreTuple.id);
			Add(genre);

			var stageTuple = GetStringAndId(musicXml, "StageID");
			var stage = new Stage(stageTuple.str, stageTuple.id);
			Add(stage);
		}

		private async Task ProcessVersionXml(string versionXmlFilePath)
		{
			using var fs = File.OpenRead(versionXmlFilePath);
			var versionXml = await XDocument.LoadAsync(fs, LoadOptions.None, default);

			(var id, var name) = GetStringAndId(versionXml, "Name");
			var title = GetStringByPath(versionXml, "Title");

			var version = new VersionID(name, id, title);
			Add(version);
		}

		private async Task ProcessCardXml(string cardXmlFilePath, Dictionary<int, string> cardFileMap)
		{
			using var fs = File.OpenRead(cardXmlFilePath);
			var cardXml = await XDocument.LoadAsync(fs, LoadOptions.None, default);

			(var id, var name) = GetStringAndId(cardXml, "Name");
			var attr = Enum.Parse<BossAttritude>(GetStringByPath(cardXml, "Attribute"));
			var rare = Enum.Parse<Rarity>(GetStringByPath(cardXml, "Rarity"));

			var card = new BossCard(name, id, attr, rare, cardFileMap.TryGetValue(id, out var f) ? f : default);
			Add(card);
		}

		#region Add()

		public bool Add(Stage stage)
		{
			lock (stages)
			{
				if (!stages.ContainsKey(stage.Id))
				{
					stages.Add(stage.Id, stage);
					return true;
				}
				return false;
			}
		}

		public bool Add(BossCard card)
		{
			lock (bossCards)
			{
				if (!bossCards.ContainsKey(card.Id))
				{
					bossCards.Add(card.Id, card);
					return true;
				}
				return false;
			}
		}

		public bool Add(VersionID version)
		{
			lock (versions)
			{
				if (!versions.ContainsKey(version.Id))
				{
					versions.Add(version.Id, version);
					return true;
				}
				return false;
			}
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

		#endregion
	}
}
