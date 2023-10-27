using OngekiFumenEditor.Base;
using System.ComponentModel.Composition;

namespace OngekiFumenEditor.Parser.DefaultImpl.Ogkr.CommandParserImpl.MetaInfo
{
	[Export(typeof(ICommandParser))]
	class TResolutionCommandParser : MetaInfoCommandParserBase
	{
		public override string CommandLineHeader => "TRESOLUTION";

		public override void ParseMetaInfo(CommandArgs args, OngekiFumen fumen)
		{
			fumen.MetaInfo.TRESOLUTION = args.GetData<int>(1);
		}
	}
}
