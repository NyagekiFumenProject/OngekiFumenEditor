using OngekiFumenEditor.Base;
using System.ComponentModel.Composition;
using System.Linq;

namespace OngekiFumenEditor.Parser.DefaultImpl.Ogkr.CommandParserImpl.MetaInfo
{
	[Export(typeof(ICommandParser))]
	class MeterDefinitionCommandParser : MetaInfoCommandParserBase
	{
		public override string CommandLineHeader => "MET_DEF";

		public override void ParseMetaInfo(CommandArgs args, OngekiFumen fumen)
		{
			var dataArr = args.GetDataArray<int>();

			fumen.MetaInfo.MeterDefinition = new FumenMetaInfo.MetDef()
			{
				Bunshi = dataArr.ElementAtOrDefault(1),
				Bunbo = dataArr.ElementAtOrDefault(2),
			};
		}
	}
}
