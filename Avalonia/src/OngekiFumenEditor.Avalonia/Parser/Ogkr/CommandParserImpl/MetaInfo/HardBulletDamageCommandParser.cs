using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Base;

namespace OngekiFumenEditor.Avalonia.Parser.Ogkr.CommandParserImpl.MetaInfo
{
	[RegisterSingleton<ICommandParser>]
	class HardBulletDamageCommandParser : MetaInfoCommandParserBase
	{
		public override string CommandLineHeader => "HARDBULLET_DAMAGE";

		public override void ParseMetaInfo(CommandArgs args, OngekiFumen fumen)
		{
			fumen.MetaInfo.HardBulletDamage = args.GetData<double>(1);
		}
	}
}


