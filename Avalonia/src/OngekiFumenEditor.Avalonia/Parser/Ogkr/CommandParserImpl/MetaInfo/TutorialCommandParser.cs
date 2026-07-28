using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Base;

namespace OngekiFumenEditor.Avalonia.Parser.Ogkr.CommandParserImpl.MetaInfo
{
	[RegisterSingleton<ICommandParser>]
	class TutorialCommandParser : MetaInfoCommandParserBase
	{
		public override string CommandLineHeader => "TUTORIAL";

		public override void ParseMetaInfo(CommandArgs args, OngekiFumen fumen)
		{
			fumen.MetaInfo.Tutorial = args.GetData<int>(1) == 1;
		}
	}
}


