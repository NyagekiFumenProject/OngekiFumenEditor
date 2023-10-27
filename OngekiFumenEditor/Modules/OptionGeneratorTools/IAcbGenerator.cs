using Gemini.Framework;
using OngekiFumenEditor.Modules.OptionGeneratorTools.Models;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Modules.OptionGeneratorTools
{
	public interface IAcbGenerator : IWindow
	{
		Task<bool> Generate(AcbGenerateOption option);
	}
}
