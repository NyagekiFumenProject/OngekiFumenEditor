using Caliburn.Micro;
using OngekiFumenEditor.Base;
using System;

namespace OngekiFumenEditor.Modules.FumenMetaInfoBrowser.ViewModels
{
	public class OngekiFumenModelProxy : PropertyChangedBase
	{
		private OngekiFumen fumen;

		public OngekiFumenModelProxy(OngekiFumen fumen)
		{
			this.fumen = fumen;
		}

		public FumenMetaInfo FumenMetaInfo => fumen.MetaInfo;

		public int VersionMajor
		{
			get
			{
				return FumenMetaInfo?.Version.Major ?? 0;
			}
			set
			{
				FumenMetaInfo.Version = new Version(value, VersionMinor, VersionBuild);
				NotifyOfPropertyChange(() => VersionMajor);
			}
		}

		public int VersionMinor
		{
			get
			{
				return FumenMetaInfo?.Version.Minor ?? 0;
			}
			set
			{
				FumenMetaInfo.Version = new Version(VersionMajor, value, VersionBuild);
				NotifyOfPropertyChange(() => VersionMinor);
			}
		}

		public int VersionBuild
		{
			get
			{
				return FumenMetaInfo?.Version.Build ?? 0;
			}
			set
			{
				FumenMetaInfo.Version = new Version(VersionMajor, VersionMinor, value);
				NotifyOfPropertyChange(() => VersionBuild);
			}
		}

		public string Creator
		{
			get
			{
				return FumenMetaInfo?.Creator ?? "";
			}
			set
			{
				FumenMetaInfo.Creator = value;
				NotifyOfPropertyChange(() => Creator);
			}
		}

		public double MinBpm
		{
			get
			{
				return FumenMetaInfo?.BpmDefinition.Minimum ?? 0;
			}
			set
			{
				FumenMetaInfo.BpmDefinition.Minimum = value;
				NotifyOfPropertyChange(() => MinBpm);
			}
		}

		public double MaxBpm
		{
			get
			{
				return FumenMetaInfo?.BpmDefinition.Maximum ?? 0;
			}
			set
			{
				FumenMetaInfo.BpmDefinition.Maximum = value;
				NotifyOfPropertyChange(() => MaxBpm);
			}
		}

		public double CommonBpm
		{
			get
			{
				return FumenMetaInfo?.BpmDefinition.Common ?? 0;
			}
			set
			{
				FumenMetaInfo.BpmDefinition.Common = value;
				NotifyOfPropertyChange(() => CommonBpm);
			}
		}

		public double FirstBpm
		{
			get
			{
				return FumenMetaInfo?.BpmDefinition.First ?? 0;
			}
			set
			{
				FumenMetaInfo.BpmDefinition.First = value;
				NotifyOfPropertyChange(() => FirstBpm);
			}
		}

		public int Bunbo
		{
			get
			{
				return FumenMetaInfo?.MeterDefinition.Bunbo ?? 0;
			}
			set
			{
				FumenMetaInfo.MeterDefinition.Bunbo = value;
				NotifyOfPropertyChange(() => Bunbo);
			}
		}

		public int Bunshi
		{
			get
			{
				return FumenMetaInfo?.MeterDefinition.Bunshi ?? 0;
			}
			set
			{
				FumenMetaInfo.MeterDefinition.Bunshi = value;
				NotifyOfPropertyChange(() => Bunshi);
			}
		}

		public int TRESOLUTION
		{
			get
			{
				return FumenMetaInfo?.TRESOLUTION ?? 1920;
			}
			set
			{
				FumenMetaInfo.TRESOLUTION = value;
				NotifyOfPropertyChange(() => TRESOLUTION);
			}
		}

		public int XRESOLUTION
		{
			get
			{
				return FumenMetaInfo?.XRESOLUTION ?? 4096;
			}
			set
			{
				FumenMetaInfo.XRESOLUTION = value;
				NotifyOfPropertyChange(() => XRESOLUTION);
			}
		}

		public int ClickDefinition
		{
			get
			{
				return FumenMetaInfo?.ClickDefinition ?? 1920;
			}
			set
			{
				FumenMetaInfo.ClickDefinition = value;
				NotifyOfPropertyChange(() => ClickDefinition);
			}
		}

		public bool Tutorial
		{
			get
			{
				return FumenMetaInfo?.Tutorial ?? false;
			}
			set
			{
				FumenMetaInfo.Tutorial = value;
				NotifyOfPropertyChange(() => Tutorial);
			}
		}

		public double BulletDamage
		{
			get
			{
				return FumenMetaInfo?.BulletDamage ?? 1;
			}
			set
			{
				FumenMetaInfo.BulletDamage = value;
				NotifyOfPropertyChange(() => BulletDamage);
			}
		}

		public double HardBulletDamage
		{
			get
			{
				return FumenMetaInfo?.HardBulletDamage ?? 2;
			}
			set
			{
				FumenMetaInfo.HardBulletDamage = value;
				NotifyOfPropertyChange(() => HardBulletDamage);
			}
		}

		public double DangerBulletDamage
		{
			get
			{
				return FumenMetaInfo?.DangerBulletDamage ?? 4;
			}
			set
			{
				FumenMetaInfo.DangerBulletDamage = value;
				NotifyOfPropertyChange(() => DangerBulletDamage);
			}
		}

		public double BeamDamage
		{
			get
			{
				return FumenMetaInfo?.BeamDamage ?? 2;
			}
			set
			{
				FumenMetaInfo.BeamDamage = value;
				NotifyOfPropertyChange(() => BeamDamage);
			}
		}

		public float ProgJudgeBpm
		{
			get
			{
				return FumenMetaInfo?.ProgJudgeBpm ?? 240;
			}
			set
			{
				FumenMetaInfo.ProgJudgeBpm = value;
				NotifyOfPropertyChange(() => ProgJudgeBpm);
			}
		}
	}
}
