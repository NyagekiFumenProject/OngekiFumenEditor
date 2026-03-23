using OngekiFumenEditor.Core.Base;
using System.ComponentModel.Composition;

namespace OngekiFumenEditor.Core.Parser.Ogkr.CommandParserImpl.MetaInfo
{
	[Export(typeof(ICommandParser))]
	class BulletDamageCommandParser : MetaInfoCommandParserBase
	{
		public override string CommandLineHeader => "BULLET_DAMAGE";

		public override void ParseMetaInfo(CommandArgs args, OngekiFumen fumen)
		{
			fumen.MetaInfo.BulletDamage = args.GetData<double>(1);
		}
	}
}
