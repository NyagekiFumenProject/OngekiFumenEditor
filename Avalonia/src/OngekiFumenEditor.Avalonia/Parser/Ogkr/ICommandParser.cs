using OngekiFumenEditor.Avalonia.Base;

namespace OngekiFumenEditor.Avalonia.Parser.Ogkr
{
	public interface ICommandParser
	{
		public string CommandLineHeader { get; }
		public OngekiObjectBase Parse(CommandArgs args, OngekiFumen fumen);
		public void AfterParse(OngekiObjectBase obj, OngekiFumen fumen);
	}
}


