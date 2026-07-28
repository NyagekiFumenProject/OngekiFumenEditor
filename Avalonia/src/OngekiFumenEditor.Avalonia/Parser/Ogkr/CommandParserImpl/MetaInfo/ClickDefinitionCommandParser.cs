using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Base;

namespace OngekiFumenEditor.Avalonia.Parser.Ogkr.CommandParserImpl.MetaInfo
{
	[RegisterSingleton<ICommandParser>]
	class ClickDefinitionCommandParser : MetaInfoCommandParserBase
	{
		public override string CommandLineHeader => "CLK_DEF";

		public override void ParseMetaInfo(CommandArgs args, OngekiFumen fumen)
		{
			fumen.MetaInfo.ClickDefinition = args.GetData<int>(1);
		}
	}
}


