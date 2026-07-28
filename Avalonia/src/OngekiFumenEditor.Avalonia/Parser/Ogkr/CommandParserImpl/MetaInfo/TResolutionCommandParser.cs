using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Base;

namespace OngekiFumenEditor.Avalonia.Parser.Ogkr.CommandParserImpl.MetaInfo
{
	[RegisterSingleton<ICommandParser>]
	class TResolutionCommandParser : MetaInfoCommandParserBase
	{
		public override string CommandLineHeader => "TRESOLUTION";

		public override void ParseMetaInfo(CommandArgs args, OngekiFumen fumen)
		{
			fumen.MetaInfo.TRESOLUTION = args.GetData<int>(1);
		}
	}
}


