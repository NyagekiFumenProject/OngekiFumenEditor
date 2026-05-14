using OngekiFumenEditor.Core.Base;
using System.ComponentModel.Composition;

namespace OngekiFumenEditor.Core.Parser.Ogkr.CommandParserImpl.MetaInfo
{
	[Export(typeof(ICommandParser))]
	class DangerBulletDamageCommandParser : MetaInfoCommandParserBase
	{
		public override string CommandLineHeader => "DANGERBULLET_DAMAGE";

		public override void ParseMetaInfo(CommandArgs args, OngekiFumen fumen)
		{
			fumen.MetaInfo.DangerBulletDamage = args.GetData<double>(1);
		}
	}
}
