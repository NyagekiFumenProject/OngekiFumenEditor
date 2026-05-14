using OngekiFumenEditor.Core.Base;
using System.ComponentModel.Composition;

namespace OngekiFumenEditor.Core.Parser.Ogkr.CommandParserImpl.MetaInfo
{
	[Export(typeof(ICommandParser))]
	class HardBulletDamageCommandParser : MetaInfoCommandParserBase
	{
		public override string CommandLineHeader => "HARDBULLET_DAMAGE";

		public override void ParseMetaInfo(CommandArgs args, OngekiFumen fumen)
		{
			fumen.MetaInfo.HardBulletDamage = args.GetData<double>(1);
		}
	}
}
