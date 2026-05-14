using OngekiFumenEditor.Core.Base;
using System.ComponentModel.Composition;
using System.Linq;

namespace OngekiFumenEditor.Core.Parser.Ogkr.CommandParserImpl.MetaInfo
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
