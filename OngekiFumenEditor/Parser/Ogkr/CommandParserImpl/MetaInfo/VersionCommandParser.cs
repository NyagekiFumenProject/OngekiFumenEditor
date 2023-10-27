using OngekiFumenEditor.Base;
using System;
using System.ComponentModel.Composition;
using System.Linq;

namespace OngekiFumenEditor.Parser.DefaultImpl.Ogkr.CommandParserImpl.MetaInfo
{
	[Export(typeof(ICommandParser))]
	class VersionCommandParser : MetaInfoCommandParserBase
	{
		public override string CommandLineHeader => "VERSION";

		public override void ParseMetaInfo(CommandArgs args, OngekiFumen fumen)
		{
			var dataArr = args.GetDataArray<int>();
			fumen.MetaInfo.Version = new Version(dataArr.ElementAtOrDefault(1), dataArr.ElementAtOrDefault(2), dataArr.ElementAtOrDefault(3));
		}
	}
}
