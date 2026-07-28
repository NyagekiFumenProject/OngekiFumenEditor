using OngekiFumenEditor.Avalonia.Base;

namespace OngekiFumenEditor.Avalonia.Parser.DefaultImpl.Nyageki
{
	public interface INyagekiCommandParser
	{
		string CommandName { get; }
		void ParseAndApply(OngekiFumen fumen, string[] seg);
	}
}


