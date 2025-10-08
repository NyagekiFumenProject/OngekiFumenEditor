using OngekiFumenEditor.Base;

namespace OngekiFumenEditor.Parser.DefaultImpl.Nyageki
{
	public interface INyagekiCommandParser
	{
		string CommandName { get; }
		void ParseAndApply(OngekiFumen fumen, string[] seg);
	}
}
