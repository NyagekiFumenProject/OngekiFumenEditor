using OngekiFumenEditor.Core.Base;

namespace OngekiFumenEditor.Core.Parser.DefaultImpl.Nyageki
{
	public interface INyagekiCommandParser
	{
		string CommandName { get; }
		void ParseAndApply(OngekiFumen fumen, string[] seg);
	}
}
