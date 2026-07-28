using OngekiFumenEditor.Avalonia.Base;

namespace OngekiFumenEditor.Avalonia.Parser.Ogkr.CommandParserImpl.MetaInfo
{
	public abstract class MetaInfoCommandParserBase : CommandParserBase
	{
		public abstract void ParseMetaInfo(CommandArgs args, OngekiFumen fumen);

		public override OngekiObjectBase Parse(CommandArgs args, OngekiFumen fumen)
		{
			ParseMetaInfo(args, fumen);
			return null;
		}
	}
}


