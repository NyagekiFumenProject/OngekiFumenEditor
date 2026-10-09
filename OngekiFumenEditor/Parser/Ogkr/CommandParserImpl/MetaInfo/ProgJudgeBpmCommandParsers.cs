using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects;
using System.ComponentModel.Composition;

namespace OngekiFumenEditor.Parser.Ogkr.CommandParserImpl.MetaInfo
{
    [Export(typeof(ICommandParser))]
    class ProgJudgeBpmCommandParsers : MetaInfoCommandParserBase
    {
        public override string CommandLineHeader => "PROGJUDGE_BPM";

        public override void ParseMetaInfo(CommandArgs args, OngekiFumen fumen)
        {
            var rawValue = args.GetData<string>(1);
            if (!HoldTickStepCalculator.TryParseProgJudgeBpm(rawValue, out var value))
            {
                fumen.ReportParseIssue(new FumenParseIssue
                {
                    Kind = FumenParseIssueKind.InvalidProgJudgeBpm,
                    Tag = CommandLineHeader,
                    Line = args.Line?.Trim() ?? string.Empty,
                    Detail = rawValue ?? string.Empty,
                });
            }

            fumen.MetaInfo.ProgJudgeBpm = value;
        }
    }
}
