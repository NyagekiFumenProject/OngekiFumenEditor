using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Base;

namespace OngekiFumenEditor.Avalonia.Parser.Ogkr.CommandParserImpl.MetaInfo
{
	[RegisterSingleton<ICommandParser>]
	class CreatorCommandParser : MetaInfoCommandParserBase
	{
		public override string CommandLineHeader => "CREATOR";

		public override void ParseMetaInfo(CommandArgs args, OngekiFumen fumen)
		{
			fumen.MetaInfo.Creator = args.GetData<string>(1) ?? "";
		}
	}
}


