using OngekiFumenEditor.Base;
using System.ComponentModel.Composition;

namespace OngekiFumenEditor.Parser.DefaultImpl.Ogkr.CommandParserImpl.MetaInfo
{
	[Export(typeof(ICommandParser))]
	class CreatorCommandParser : MetaInfoCommandParserBase
	{
		public override string CommandLineHeader => "CREATOR";

		public override void ParseMetaInfo(CommandArgs args, OngekiFumen fumen)
		{
			fumen.MetaInfo.Creator = args.GetData<string>(1) ?? "";
		}
	}
}
