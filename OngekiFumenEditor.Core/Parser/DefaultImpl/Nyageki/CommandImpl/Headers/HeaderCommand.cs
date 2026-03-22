using OngekiFumenEditor.Base;
using System;
using System.ComponentModel.Composition;

namespace OngekiFumenEditor.Parser.DefaultImpl.Nyageki.CommandImpl.Headers
{
	public abstract class HeaderCommandBase : INyagekiCommandParser
	{
		public abstract string HeaderName { get; }

		public string CommandName => "Header." + HeaderName;

		public void ParseAndApply(OngekiFumen fumen, string[] seg)
		{
			var data = seg[1];
			var hdata = data.Split(":", 1);

			ApplyHeaderValue(fumen, hdata[0].Trim());
		}

		protected abstract void ApplyHeaderValue(OngekiFumen fumen, string headerValue);
	}

	[Export(typeof(INyagekiCommandParser))]
	public class BeamDamageHeader : HeaderCommandBase
	{
		public override string HeaderName => nameof(FumenMetaInfo.BeamDamage);

		protected override void ApplyHeaderValue(OngekiFumen fumen, string headerValue)
		{
			fumen.MetaInfo.BeamDamage = float.Parse(headerValue);
		}
	}

	[Export(typeof(INyagekiCommandParser))]
	public class BulletDamageDamageHeader : HeaderCommandBase
	{
		public override string HeaderName => nameof(FumenMetaInfo.BulletDamage);

		protected override void ApplyHeaderValue(OngekiFumen fumen, string headerValue)
		{
			fumen.MetaInfo.BulletDamage = float.Parse(headerValue);
		}
	}

	[Export(typeof(INyagekiCommandParser))]
	public class MeterHeader : HeaderCommandBase
	{
		public override string HeaderName => "Meter";

		protected override void ApplyHeaderValue(OngekiFumen fumen, string headerValue)
		{
			var data = headerValue.Split("/");
			fumen.MetaInfo.MeterDefinition.Bunshi = int.Parse(data[0]);
			fumen.MetaInfo.MeterDefinition.Bunbo = int.Parse(data[1]);
		}
	}

	[Export(typeof(INyagekiCommandParser))]
	public class MinimumBpmHeader : HeaderCommandBase
	{
		public override string HeaderName => "MinimumBpm";

		protected override void ApplyHeaderValue(OngekiFumen fumen, string headerValue)
		{
			fumen.MetaInfo.BpmDefinition.Minimum = float.Parse(headerValue);
		}
	}

	[Export(typeof(INyagekiCommandParser))]
	public class MaximumBpmHeader : HeaderCommandBase
	{
		public override string HeaderName => "MaximumBpm";

		protected override void ApplyHeaderValue(OngekiFumen fumen, string headerValue)
		{
			fumen.MetaInfo.BpmDefinition.Maximum = float.Parse(headerValue);
		}
	}

	[Export(typeof(INyagekiCommandParser))]
	public class CommonBpmHeader : HeaderCommandBase
	{
		public override string HeaderName => "CommonBpm";

		protected override void ApplyHeaderValue(OngekiFumen fumen, string headerValue)
		{
			fumen.MetaInfo.BpmDefinition.Common = float.Parse(headerValue);
		}
	}

	[Export(typeof(INyagekiCommandParser))]
	public class FirstBpmHeader : HeaderCommandBase
	{
		public override string HeaderName => "FirstBpm";

		protected override void ApplyHeaderValue(OngekiFumen fumen, string headerValue)
		{
			fumen.MetaInfo.BpmDefinition.First = float.Parse(headerValue);
		}
	}

	[Export(typeof(INyagekiCommandParser))]
	public class ProgJudgeBpm : HeaderCommandBase
	{
		public override string HeaderName => nameof(FumenMetaInfo.ProgJudgeBpm);

		protected override void ApplyHeaderValue(OngekiFumen fumen, string headerValue)
		{
			fumen.MetaInfo.ProgJudgeBpm = float.Parse(headerValue);
		}
	}

	[Export(typeof(INyagekiCommandParser))]
	public class ClickDefinitionHeader : HeaderCommandBase
	{
		public override string HeaderName => nameof(FumenMetaInfo.ClickDefinition);

		protected override void ApplyHeaderValue(OngekiFumen fumen, string headerValue)
		{
			fumen.MetaInfo.ClickDefinition = int.Parse(headerValue);
		}
	}

	[Export(typeof(INyagekiCommandParser))]
	public class XRESOLUTIONHeader : HeaderCommandBase
	{
		public override string HeaderName => nameof(FumenMetaInfo.XRESOLUTION);

		protected override void ApplyHeaderValue(OngekiFumen fumen, string headerValue)
		{
			fumen.MetaInfo.XRESOLUTION = int.Parse(headerValue);
		}
	}

	[Export(typeof(INyagekiCommandParser))]
	public class TRESOLUTIONHeader : HeaderCommandBase
	{
		public override string HeaderName => nameof(FumenMetaInfo.TRESOLUTION);

		protected override void ApplyHeaderValue(OngekiFumen fumen, string headerValue)
		{
			fumen.MetaInfo.TRESOLUTION = int.Parse(headerValue);
		}
	}

	[Export(typeof(INyagekiCommandParser))]
	public class TutorialHeader : HeaderCommandBase
	{
		public override string HeaderName => nameof(FumenMetaInfo.Tutorial);

		protected override void ApplyHeaderValue(OngekiFumen fumen, string headerValue)
		{
			fumen.MetaInfo.Tutorial = bool.Parse(headerValue);
		}
	}

	[Export(typeof(INyagekiCommandParser))]
	public class HardBulletDamageDamageHeader : HeaderCommandBase
	{
		public override string HeaderName => nameof(FumenMetaInfo.HardBulletDamage);

		protected override void ApplyHeaderValue(OngekiFumen fumen, string headerValue)
		{
			fumen.MetaInfo.HardBulletDamage = float.Parse(headerValue);
		}
	}

	[Export(typeof(INyagekiCommandParser))]
	public class DangerBulletDamageDamageHeader : HeaderCommandBase
	{
		public override string HeaderName => nameof(FumenMetaInfo.DangerBulletDamage);

		protected override void ApplyHeaderValue(OngekiFumen fumen, string headerValue)
		{
			fumen.MetaInfo.DangerBulletDamage = float.Parse(headerValue);
		}
	}

	[Export(typeof(INyagekiCommandParser))]
	public class VersionHeader : HeaderCommandBase
	{
		public override string HeaderName => nameof(FumenMetaInfo.Version);

		protected override void ApplyHeaderValue(OngekiFumen fumen, string headerValue)
		{
			fumen.MetaInfo.Version = Version.Parse(headerValue);
		}
	}

	[Export(typeof(INyagekiCommandParser))]
	public class CreatorHeader : HeaderCommandBase
	{
		public override string HeaderName => nameof(FumenMetaInfo.Creator);

		protected override void ApplyHeaderValue(OngekiFumen fumen, string headerValue)
		{
			fumen.MetaInfo.Creator = headerValue;
		}
	}
}
