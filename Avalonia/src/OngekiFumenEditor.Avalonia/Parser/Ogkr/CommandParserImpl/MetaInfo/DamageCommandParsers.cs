using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Base;

namespace OngekiFumenEditor.Avalonia.Parser.Ogkr.CommandParserImpl.MetaInfo
{
	[RegisterSingleton<ICommandParser>]
	class BulletDamageCommandParser : MetaInfoCommandParserBase
	{
		public override string CommandLineHeader => "BULLET_DAMAGE";

		public override void ParseMetaInfo(CommandArgs args, OngekiFumen fumen)
		{
			fumen.MetaInfo.BulletDamage = args.GetData<double>(1);
		}
	}
}


