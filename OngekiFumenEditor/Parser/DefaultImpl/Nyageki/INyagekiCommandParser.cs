using OngekiFumenEditor.Base;

namespace OngekiFumenEditor.Parser.DefaultImpl
{
	public interface INyagekiCommandParser
	{
		string CommandName { get; }
		void ParseAndApply(OngekiFumen fumen, string[] seg);
	}
}
