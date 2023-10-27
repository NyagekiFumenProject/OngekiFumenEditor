using OngekiFumenEditor.Base;

namespace OngekiFumenEditor.Parser.DefaultImpl.Ogkr
{
	public abstract class CommandParserBase : ICommandParser
	{
		public abstract string CommandLineHeader { get; }

		public virtual void AfterParse(OngekiObjectBase obj, OngekiFumen fumen) { }

		public abstract OngekiObjectBase Parse(CommandArgs args, OngekiFumen fumen);
	}
}
