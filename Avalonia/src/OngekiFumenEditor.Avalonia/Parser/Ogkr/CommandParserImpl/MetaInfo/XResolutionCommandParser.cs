using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Base;

namespace OngekiFumenEditor.Avalonia.Parser.Ogkr.CommandParserImpl.MetaInfo
{
	[RegisterSingleton<ICommandParser>]
	class XResolutionCommandParser : MetaInfoCommandParserBase
	{
		public override string CommandLineHeader => "XRESOLUTION";

		public override void ParseMetaInfo(CommandArgs args, OngekiFumen fumen)
		{
			fumen.MetaInfo.XRESOLUTION = args.GetData<int>(1);
		}
	}
}


