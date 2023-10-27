using Caliburn.Micro;
using OngekiFumenEditor.Modules.OptionGeneratorTools.Models.EnumStructs;
using System.Collections.Generic;

namespace OngekiFumenEditor.Modules.OptionGeneratorTools.Models
{
	public class MusicXmlGenerateOption : PropertyChangedBase
	{
		private int musicId = -1;
		public int MusicId
		{
			get => musicId;
			set => Set(ref musicId, value);
		}

		private string title;
		public string Title
		{
			get => title;
			set => Set(ref title, value);
		}

		private string artist;
		public string Artist
		{
			get => artist;
			set => Set(ref artist, value);
		}

		private Stage stage;
		public Stage Stage
		{
			get => stage;
			set => Set(ref stage, value);
		}

		private Genre genre;
		public Genre Genre
		{
			get => genre;
			set => Set(ref genre, value);
		}

		private BossCard bossCard;
		public BossCard BossCard
		{
			get => bossCard;
			set => Set(ref bossCard, value);
		}

		private VersionID addVersion;
		public VersionID AddVersion
		{
			get => addVersion;
			set => Set(ref addVersion, value);
		}

		private int bossHp = 50000;
		public int BossHp
		{
			get => bossHp;
			set => Set(ref bossHp, value);
		}

		private int bossLevel = 10;
		public int BossLevel
		{
			get => bossLevel;
			set => Set(ref bossLevel, value);
		}

		private string musicSourceName = null;
		public string MusicSourceName
		{
			get
			{
				if (musicSourceName == null)
					return $"musicsource{MusicId}";
				return musicSourceName;
			}
			set => Set(ref musicSourceName, value);
		}

		private MusicRight musicRightName;
		public MusicRight MusicRightName
		{
			get => musicRightName;
			set => Set(ref musicRightName, value);
		}

		private int bossVoiceIdx = 1;
		public int BossVoiceIdx
		{
			get => bossVoiceIdx;
			set => Set(ref bossVoiceIdx, value);
		}

		private bool processingFromTheBeginning = true;
		public bool ProcessingFromTheBeginning
		{
			get => processingFromTheBeginning;
			set => Set(ref processingFromTheBeginning, value);
		}

		private bool isLockedAtTheBeginning = false;
		public bool IsLockedAtTheBeginning
		{
			get => isLockedAtTheBeginning;
			set => Set(ref isLockedAtTheBeginning, value);
		}

		private int finishBouns = 2;
		public int FinishBouns
		{
			get => finishBouns;
			set => Set(ref finishBouns, value);
		}

		private int costToUnlock = 0;
		public int CostToUnlock
		{
			get => costToUnlock;
			set => Set(ref costToUnlock, value);
		}

		private bool epReleaseFlag = false;
		public bool EpReleaseFlag
		{
			get => epReleaseFlag;
			set => Set(ref epReleaseFlag, value);
		}

		private int sortOrder = 112857;
		public int SortOrder
		{
			get => sortOrder;
			set => Set(ref sortOrder, value);
		}

		public Dictionary<Difficult, FumenData> FumenDatas { get; } = new()
		{
			{Difficult.Basic,new() },
			{Difficult.Advance,new() },
			{Difficult.Expert,new() },
			{Difficult.Master,new() },
			{Difficult.Lunatic,new() }
		};
	}
}
