using OngekiFumenEditor.Modules.OptionGeneratorTools.Models;
using OngekiFumenEditor.Modules.OptionGeneratorTools.Models.EnumStructs;
using System;
using System.Linq;
using System.Xml.Linq;
using System.Xml.XPath;

namespace OngekiFumenEditor.Modules.OptionGeneratorTools.Kernel
{
	public static class MusicXmlSerialization
	{
		private static (int id, string str) GetStringAndId(XNode document, string name, string strKey = "str", string idKey = "id")
		{
			try
			{
				var str = document.XPathSelectElement($@"//{name}[1]/{strKey}[1]").Value;
				var id = int.Parse(document.XPathSelectElement($@"//{name}[1]/{idKey}[1]").Value);

				return (id, str);
			}
			catch (Exception e)
			{

				return default;
			}
		}

		private static string GetStringByPath(XNode document, params string[] fieldPaths)
		{
			try
			{
				var selectExpr = $"//{string.Join("/", fieldPaths.Select(x => $"{x}[1]"))}";
				var str = document.XPathSelectElement(selectExpr).Value;

				return str;
			}
			catch (Exception e)
			{

				return default;
			}
		}

		private static XElement GetNode(XNode document, params string[] fieldPaths)
		{
			try
			{
				var selectExpr = $"//{string.Join("/", fieldPaths.Select(x => $"{x}[1]"))}";
				return document.XPathSelectElement(selectExpr);
			}
			catch (Exception e)
			{
				return default;
			}
		}

		public static XDocument Serialize(MusicXmlGenerateOption opt, EnumFetchManager enumManager)
		{
			using var fs = typeof(MusicXmlSerialization).Assembly.GetManifestResourceStream("OngekiFumenEditor.Resources.Music.xml");
			var musicXml = XDocument.Load(fs);

			var idStr = opt.MusicId.ToString().PadLeft(4, '0');
			GetNode(musicXml, "dataName").Value = $"music{idStr}";

			GetNode(musicXml, "Name", "id").Value = opt.MusicId.ToString();
			GetNode(musicXml, "Name", "str").Value = opt.Title.ToString();

			GetNode(musicXml, "ArtistName", "id").Value = opt.MusicId.ToString();
			GetNode(musicXml, "ArtistName", "str").Value = opt.Artist.ToString();

			var right = opt.MusicRightName ?? new("-", 0);
			GetNode(musicXml, "MusicRightsName", "id").Value = right.Id.ToString();
			GetNode(musicXml, "MusicRightsName", "str").Value = right.Name.ToString();

			GetNode(musicXml, "MusicSourceName", "id").Value = opt.MusicId.ToString();
			GetNode(musicXml, "MusicSourceName", "str").Value = opt.Title.ToString();

			var genre = opt.Genre ?? enumManager.Genres.Values.FirstOrDefault();
			GetNode(musicXml, "Genre", "id").Value = genre.Id.ToString();
			GetNode(musicXml, "Genre", "str").Value = genre.Name.ToString();

			var bossCard = opt.BossCard ?? enumManager.BossCards.Values.FirstOrDefault();
			GetNode(musicXml, "BossCard", "id").Value = bossCard.Id.ToString();
			GetNode(musicXml, "BossCard", "str").Value = bossCard.Name.ToString();

			var version = opt.AddVersion ?? enumManager.Versions.Values.FirstOrDefault();
			GetNode(musicXml, "VersionID", "id").Value = version.Id.ToString();
			GetNode(musicXml, "VersionID", "str").Value = version.Name.ToString();

			var stage = opt.Stage ?? enumManager.Stages.Values.FirstOrDefault();
			GetNode(musicXml, "StageID", "id").Value = stage.Id.ToString();
			GetNode(musicXml, "StageID", "str").Value = stage.Name.ToString();

			var fumenElements = musicXml.XPathSelectElements("//FumenData//FumenData").ToArray();
			var diffLen = Enum.GetValues<Difficult>().Length;
			for (int i = 0; i < diffLen; i++)
			{
				var diff = (Difficult)i;

				var fumenElement = fumenElements.ElementAtOrDefault(i);
				if (fumenElement is not null)
				{
					var fumenData = opt.FumenDatas.TryGetValue(diff, out var fff) ? fff : new();

					fumenElement.XPathSelectElement("./FumenConstIntegerPart").Value = ((int)fumenData.Level).ToString();
					var fp = (int)Math.Round((fumenData.Level - (int)fumenData.Level) * 100 + 0.5);
					fumenElement.XPathSelectElement("./FumenConstFractionalPart").Value = fp.ToString();

					fumenElement.XPathSelectElement("./FumenFile/path").Value = string.Empty;
					if (fumenData.Enable)
						fumenElement.XPathSelectElement("./FumenFile/path").Value = fumenData.FileName ?? $"{idStr}_0{i}.ogkr";
				}
			}

			var lunaticFumenData = opt.FumenDatas.TryGetValue(Difficult.Lunatic, out var ff) ? ff : new();
			GetNode(musicXml, "IsLunatic").Value = lunaticFumenData.Enable.ToString().ToLower();

			GetNode(musicXml, "WaveAttribute", "AttributeType").Value = bossCard.Attritude.ToString();
			GetNode(musicXml, "CostToUnlock").Value = opt.CostToUnlock.ToString();
			GetNode(musicXml, "FinishBonus").Value = opt.FinishBouns.ToString();
			GetNode(musicXml, "BossLevel").Value = opt.BossLevel.ToString();
			GetNode(musicXml, "EpReleaseFlag").Value = opt.EpReleaseFlag.ToString().ToLower();
			GetNode(musicXml, "SortOrder").Value = opt.SortOrder.ToString();
			GetNode(musicXml, "PossessingFromTheBeginning").Value = opt.ProcessingFromTheBeginning.ToString().ToLower();
			GetNode(musicXml, "IsLockedAtTheBeginning").Value = opt.IsLockedAtTheBeginning.ToString().ToLower();
			GetNode(musicXml, "NameForSort").Value = opt.Title.ToString().ToUpper();
			GetNode(musicXml, "BossLockHpCoef").Value = opt.BossHp.ToString();
			GetNode(musicXml, "BossVoiceNo").Value = opt.BossVoiceIdx.ToString();

			return musicXml;
		}

		public static MusicXmlGenerateOption Serialize(XDocument xml, EnumFetchManager enumManager)
		{
			var opt = new MusicXmlGenerateOption();

			(var cardBossId, var cardBossName) = GetStringAndId(xml, "BossCard");
			opt.BossCard = enumManager.BossCards.TryGetValue(cardBossId, out var card) ?
				card :
				new(cardBossName, cardBossId, default, default, default);

			(var genreId, var genreName) = GetStringAndId(xml, "Genre");
			opt.Genre = enumManager.Genres.TryGetValue(genreId, out var genre) ?
				genre :
				new(genreName, genreId);

			(var stageId, var stageName) = GetStringAndId(xml, "StageID");
			opt.Stage = enumManager.Stages.TryGetValue(stageId, out var stage) ?
				stage :
				new(stageName, stageId);

			(var versionId, var versionName) = GetStringAndId(xml, "VersionID");
			opt.AddVersion = enumManager.Versions.TryGetValue(versionId, out var version) ?
				version :
				new(versionName, versionId, "UnknownVersion:" + versionName);

			(var id, var name) = GetStringAndId(xml, "Name");
			opt.Title = name;
			opt.MusicId = id;

			(_, var artist) = GetStringAndId(xml, "ArtistName");
			opt.Artist = artist;

			(var rightId, var rightName) = GetStringAndId(xml, "MusicRightsName");
			opt.MusicRightName = enumManager.MusicRights.TryGetValue(rightId, out var right) ?
				right :
				new(rightName, rightId);

			opt.BossLevel = int.TryParse(GetStringByPath(xml, "BossLevel"), out var level) ? level : opt.BossLevel;
			opt.CostToUnlock = int.TryParse(GetStringByPath(xml, "CostToUnlock"), out var costToUnlock) ? costToUnlock : opt.CostToUnlock;
			opt.FinishBouns = int.TryParse(GetStringByPath(xml, "FinishBonus"), out var finishBonus) ? finishBonus : opt.FinishBouns;
			opt.EpReleaseFlag = bool.TryParse(GetStringByPath(xml, "FinishBonus"), out var epReleaseFlag) ? epReleaseFlag : opt.EpReleaseFlag;
			opt.BossHp = int.TryParse(GetStringByPath(xml, "BossLockHpCoef"), out var bossLockHpCoef) ? bossLockHpCoef : opt.BossHp;
			opt.BossVoiceIdx = int.TryParse(GetStringByPath(xml, "BossVoiceNo"), out var bossVoiceNo) ? bossVoiceNo : opt.BossVoiceIdx;
			opt.SortOrder = int.TryParse(GetStringByPath(xml, "SortOrder"), out var sortOrder) ? sortOrder : opt.SortOrder;
			opt.ProcessingFromTheBeginning = bool.TryParse(GetStringByPath(xml, "PossessingFromTheBeginning"), out var possessingFromTheBeginning) ? possessingFromTheBeginning : opt.ProcessingFromTheBeginning;
			opt.IsLockedAtTheBeginning = bool.TryParse(GetStringByPath(xml, "IsLockedAtTheBeginning"), out var isLockedAtTheBeginning) ? isLockedAtTheBeginning : opt.IsLockedAtTheBeginning;

			var fumenElements = xml.XPathSelectElements("//FumenData//FumenData").ToArray();
			var diffLen = Enum.GetValues<Difficult>().Length;
			for (int i = 0; i < diffLen; i++)
			{
				var diff = (Difficult)i;

				var fumenElement = fumenElements.ElementAtOrDefault(i);
				if (fumenElement is not null)
				{
					var fumenConstIntegerPart = int.TryParse(fumenElement.XPathSelectElement("./FumenConstIntegerPart").Value, out var fi) ? fi : default;
					var fumenConstFractionalPart = int.TryParse(fumenElement.XPathSelectElement("./FumenConstFractionalPart").Value, out var ff) ? ff : default;

					var fumenLevel = fumenConstIntegerPart + fumenConstFractionalPart / 100.0f;
					var fumenFileName = fumenElement.XPathSelectElement("./FumenFile/path").Value;
					var fumenEnable = !string.IsNullOrWhiteSpace(fumenFileName);

					var fumenData = new FumenData()
					{
						Enable = fumenEnable,
						Level = fumenLevel,
						FileName = fumenFileName
					};

					opt.FumenDatas[diff] = fumenData;
				}
				else
					opt.FumenDatas[diff] = new();
			}

			if (bool.TryParse(GetStringByPath(xml, "IsLunatic"), out var isLunatic) ? isLunatic : false)
				opt.FumenDatas[Difficult.Lunatic].Enable = true;

			return opt;
		}
	}
}
