using OngekiFumenEditor.Core.Base;
using System.ComponentModel.Composition;

namespace OngekiFumenEditor.Core.Parser.Ogkr.CommandParserImpl.MetaInfo
{
	[Export(typeof(ICommandParser))]
	class XResolutionCommandParser : MetaInfoCommandParserBase
	{
		public override string CommandLineHeader => "XRESOLUTION";

		public override void ParseMetaInfo(CommandArgs args, OngekiFumen fumen)
		{
			fumen.MetaInfo.XRESOLUTION = args.GetData<int>(1);
		}
	}
}
