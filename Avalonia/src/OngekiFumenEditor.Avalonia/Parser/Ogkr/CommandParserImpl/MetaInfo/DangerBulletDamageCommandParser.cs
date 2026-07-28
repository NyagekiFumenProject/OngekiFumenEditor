using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Base;

namespace OngekiFumenEditor.Avalonia.Parser.Ogkr.CommandParserImpl.MetaInfo
{
	[RegisterSingleton<ICommandParser>]
	class DangerBulletDamageCommandParser : MetaInfoCommandParserBase
	{
		public override string CommandLineHeader => "DANGERBULLET_DAMAGE";

		public override void ParseMetaInfo(CommandArgs args, OngekiFumen fumen)
		{
			fumen.MetaInfo.DangerBulletDamage = args.GetData<double>(1);
		}
	}
}


