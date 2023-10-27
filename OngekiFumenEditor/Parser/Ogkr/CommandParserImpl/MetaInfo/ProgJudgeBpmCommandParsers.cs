using OngekiFumenEditor.Base;
using System.ComponentModel.Composition;

namespace OngekiFumenEditor.Parser.DefaultImpl.Ogkr.CommandParserImpl.MetaInfo
{
	[Export(typeof(ICommandParser))]
	class ProgJudgeBpmCommandParsers : MetaInfoCommandParserBase
	{
		public override string CommandLineHeader => "PROGJUDGE_BPM";

		public override void ParseMetaInfo(CommandArgs args, OngekiFumen fumen)
		{
			fumen.MetaInfo.ProgJudgeBpm = args.GetData<float>(1);
		}
	}
}
