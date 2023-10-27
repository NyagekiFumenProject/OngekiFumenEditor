using OngekiFumenEditor.Base;
using System.ComponentModel.Composition;

namespace OngekiFumenEditor.Parser.DefaultImpl.Ogkr.CommandParserImpl.MetaInfo
{
	[Export(typeof(ICommandParser))]
	class TutorialCommandParser : MetaInfoCommandParserBase
	{
		public override string CommandLineHeader => "TUTORIAL";

		public override void ParseMetaInfo(CommandArgs args, OngekiFumen fumen)
		{
			fumen.MetaInfo.Tutorial = args.GetData<int>(1) == 1;
		}
	}
}
