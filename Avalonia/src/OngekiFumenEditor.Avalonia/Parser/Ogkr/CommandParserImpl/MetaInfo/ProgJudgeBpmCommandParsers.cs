using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Base;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects;

namespace OngekiFumenEditor.Avalonia.Parser.Ogkr.CommandParserImpl.MetaInfo
{
	[RegisterSingleton<ICommandParser>]
	class ProgJudgeBpmCommandParsers : MetaInfoCommandParserBase
	{
		public override string CommandLineHeader => "PROGJUDGE_BPM";

		public override void ParseMetaInfo(CommandArgs args, OngekiFumen fumen)
		{
			fumen.MetaInfo.ProgJudgeBpm = HoldTickStepCalculator.CoerceProgJudgeBpm(args.GetData<float>(1));
		}
	}
}


