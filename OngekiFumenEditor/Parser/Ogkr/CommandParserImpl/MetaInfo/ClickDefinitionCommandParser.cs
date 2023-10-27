using OngekiFumenEditor.Base;
using System.ComponentModel.Composition;

namespace OngekiFumenEditor.Parser.DefaultImpl.Ogkr.CommandParserImpl.MetaInfo
{
	[Export(typeof(ICommandParser))]
	class ClickDefinitionCommandParser : MetaInfoCommandParserBase
	{
		public override string CommandLineHeader => "CLK_DEF";

		public override void ParseMetaInfo(CommandArgs args, OngekiFumen fumen)
		{
			fumen.MetaInfo.ClickDefinition = args.GetData<int>(1);
		}
	}
}
