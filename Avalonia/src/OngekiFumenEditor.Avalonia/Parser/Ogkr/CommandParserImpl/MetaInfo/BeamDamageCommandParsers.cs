using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Base;

namespace OngekiFumenEditor.Avalonia.Parser.Ogkr.CommandParserImpl.MetaInfo
{
	[RegisterSingleton<ICommandParser>]
	class BeamDamageCommandParsers : MetaInfoCommandParserBase
	{
		public override string CommandLineHeader => "BEAM_DAMAGE";

		public override void ParseMetaInfo(CommandArgs args, OngekiFumen fumen)
		{
			fumen.MetaInfo.BeamDamage = args.GetData<float>(1);
		}
	}
}


