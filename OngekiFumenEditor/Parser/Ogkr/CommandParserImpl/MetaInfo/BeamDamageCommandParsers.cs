using OngekiFumenEditor.Base;
using System.ComponentModel.Composition;

namespace OngekiFumenEditor.Parser.DefaultImpl.Ogkr.CommandParserImpl.MetaInfo
{
	[Export(typeof(ICommandParser))]
	class BeamDamageCommandParsers : MetaInfoCommandParserBase
	{
		public override string CommandLineHeader => "BEAM_DAMAGE";

		public override void ParseMetaInfo(CommandArgs args, OngekiFumen fumen)
		{
			fumen.MetaInfo.BeamDamage = args.GetData<float>(1);
		}
	}
}
