using OngekiFumenEditor.Base;
using System.ComponentModel.Composition;
using System.Linq;

namespace OngekiFumenEditor.Parser.DefaultImpl.Ogkr.CommandParserImpl.MetaInfo
{
	[Export(typeof(ICommandParser))]
	class BpmDefinitionCommandParser : MetaInfoCommandParserBase
	{
		public override string CommandLineHeader => "BPM_DEF";

		public override void ParseMetaInfo(CommandArgs args, OngekiFumen fumen)
		{
			var dataArr = args.GetDataArray<double>();

			fumen.MetaInfo.BpmDefinition = new FumenMetaInfo.BpmDef()
			{
				First = dataArr.ElementAtOrDefault(1),
				Common = dataArr.ElementAtOrDefault(2),
				Minimum = dataArr.ElementAtOrDefault(3),
				Maximum = dataArr.ElementAtOrDefault(4),
			};
		}
	}
}
